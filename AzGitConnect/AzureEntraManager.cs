using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AzGitConnect;

internal class AzureEntraManager : IEntraManager
{
    private readonly ArmClient _armClient;
    private readonly HttpClient _httpClient;

    public AzureEntraManager()
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            Retry =
            {
                MaxRetries = 5,
                Delay = TimeSpan.FromSeconds(2),
                Mode = RetryMode.Exponential
            }
        });

        _armClient = new ArmClient(credential);
        _httpClient = new HttpClient(new AuthenticationHandler(credential) { InnerHandler = new SocketsHttpHandler() })
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };
    }

    public async Task<GitHubSecretData> CreateAzureApplicationForGitHubAsync(string appName, string subscriptionId, string owner, string repo, string branch)
    {
        var subscription = await _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
        var tenantId = subscription.Value.Data.TenantId ?? throw new Exception("Failed to retrieve Tenant ID.");

        Console.WriteLine($"Tenant ID: {tenantId}");

        Console.WriteLine("Creating Azure AD Application...");

        var existingApps = await GetExistingApplicationsAsync(appName);

        Application? createdApp;

        if (existingApps != null && existingApps.Any())
        {
            createdApp = existingApps.First();
            Console.WriteLine($"Application with name '{appName}' already exists. Using existing application with App ID: {createdApp.AppId}");
        }
        else
        {
            createdApp = await CreateApplicationAsync(appName);

            if (createdApp == null || string.IsNullOrEmpty(createdApp.AppId))
            {
                throw new Exception("Failed to create Azure AD Application.");
            }

            Console.WriteLine($"Application created with App ID: {createdApp.AppId}");
        }

        Console.WriteLine("Creating Service Principal...");
        var createdSp = await CreateServicePrincipalAsync(createdApp.AppId!);
        Console.WriteLine("Service Principal created.");

        Console.WriteLine("Adding Federated Identity Credentials...");
        await AddFederatedIdentityCredentialsAsync(createdApp.Id!, owner, repo, branch);

        Console.WriteLine("Assigning Contributor Role at Subscription Level...");
        // Role assignment logic here...

        return new GitHubSecretData
        {
            AppId = createdApp.AppId!,
            TenantId = tenantId.ToString()!,
            SubscriptionId = subscriptionId
        };
    }

    private async Task<List<Application>> GetExistingApplicationsAsync(string appName)
    {
        var filter = Uri.EscapeDataString($"displayName eq '{appName}'");
        var response = await _httpClient.GetAsync($"applications?$filter={filter}");
        var result = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.GraphApiResponseApplication);
        return result?.Value ?? [];
    }

    private async Task<Application?> CreateApplicationAsync(string appName)
    {
        var application = new Application { DisplayName = appName };
        var response = await _httpClient.PostAsJsonAsync("applications", application, AppJsonContext.Default.Application);
        return await response.Content.ReadFromJsonAsync(AppJsonContext.Default.Application);
    }

    private async Task<ServicePrincipal?> CreateServicePrincipalAsync(string appId)
    {
        // Get the service principal to for this app

        // GET /servicePrincipals(appId='{appId}')

        var filter = Uri.EscapeDataString($"appId='{appId}'");
        var response = await _httpClient.GetAsync($"servicePrincipals({filter})");

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ServicePrincipal);
            return result;
        }

        var servicePrincipal = new ServicePrincipal { AppId = appId };
        response = await _httpClient.PostAsJsonAsync("servicePrincipals", servicePrincipal, AppJsonContext.Default.ServicePrincipal);
        return await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ServicePrincipal);
    }

    private async Task AddFederatedIdentityCredentialsAsync(string appId, string owner, string repo, string branch)
    {
        var existingCredentials = await GetFederatedIdentityCredentialsAsync(appId);
        var existingCredentialNames = new HashSet<string>(existingCredentials?.Select(c => c.Name!) ?? []);

        if (!existingCredentialNames.Contains($"gh-{branch}"))
        {
            var branchCredential = new FederatedIdentityCredential
            {
                Name = $"gh-{branch}",
                Issuer = "https://token.actions.githubusercontent.com",
                Subject = $"repo:{owner}/{repo}:ref:refs/heads/{branch}",
                Audiences = ["api://AzureADTokenExchange"]
            };

            Console.WriteLine($"Adding federated identity credential for branch '{branch}'...");
            await AddFederatedIdentityCredentialAsync(appId, branchCredential);
            Console.WriteLine("Branch federated identity credential added successfully.");
        }
        else
        {
            Console.WriteLine($"Federated identity credential for branch '{branch}' already exists.");
        }

        if (!existingCredentialNames.Contains("gh-pr"))
        {
            var prCredential = new FederatedIdentityCredential
            {
                Name = "gh-pr",
                Issuer = "https://token.actions.githubusercontent.com",
                Subject = $"repo:{owner}/{repo}:pull_request",
                Audiences = ["api://AzureADTokenExchange"]
            };

            Console.WriteLine("Adding federated identity credential for pull requests...");
            await AddFederatedIdentityCredentialAsync(appId, prCredential);
            Console.WriteLine("Pull request federated identity credential added successfully.");
        }
        else
        {
            Console.WriteLine("Federated identity credential for pull requests already exists.");
        }
    }

    private async Task<List<FederatedIdentityCredential>> GetFederatedIdentityCredentialsAsync(string appId)
    {
        var response = await _httpClient.GetAsync($"applications/{appId}/federatedIdentityCredentials");
        var result = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.GraphApiResponseFederatedIdentityCredential);
        return result?.Value ?? [];
    }

    private async Task AddFederatedIdentityCredentialAsync(string appId, FederatedIdentityCredential credential)
    {
        await _httpClient.PostAsJsonAsync($"applications/{appId}/federatedIdentityCredentials", credential, AppJsonContext.Default.FederatedIdentityCredential);
    }

    internal class AuthenticationHandler(TokenCredential credential) : DelegatingHandler
    {
        private string? _cachedToken;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _cachedToken ??= (await credential.GetTokenAsync(new TokenRequestContext(["https://graph.microsoft.com/.default"]), cancellationToken)).Token;

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);

            var response = await base.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await response.Content.LoadIntoBufferAsync();

                Console.WriteLine(request.RequestUri);
                var errorResponse = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.GraphApiError, cancellationToken: cancellationToken);
                Console.WriteLine(JsonSerializer.Serialize(errorResponse, AppJsonContext.Default.GraphApiError));

                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            return response;
        }
    }

}
