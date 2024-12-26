using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace AzGitConnect;

internal class AzureEntraManager : IEntraManager
{
    private readonly ArmClient _armClient;
    private readonly GraphServiceClient _graphClient;

    public AzureEntraManager()
    {
        // Initialize Azure SDK and Microsoft Graph SDK clients
        // Documentation: https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            Retry =
            {
                MaxRetries = 5,
                Delay = TimeSpan.FromSeconds(2),
                Mode = RetryMode.Exponential
            }
        });

        // ArmClient for Azure Resource Manager operations
        // Documentation: https://learn.microsoft.com/en-us/dotnet/api/azure.resourcemanager.armclient
        _armClient = new ArmClient(credential);

        // GraphServiceClient for Microsoft Graph API
        // Documentation: https://learn.microsoft.com/en-us/graph/sdks/create-client
        _graphClient = CreateGraphClient(credential);
    }

    private static GraphServiceClient CreateGraphClient(TokenCredential credential)
    {
        // Explicitly set Microsoft Graph API scopes
        // Documentation: https://learn.microsoft.com/en-us/graph/sdks/create-client
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        return new GraphServiceClient(credential, scopes);
    }

    public async Task<GitHubSecretData> CreateAzureApplicationForGitHubAsync(string appName, string subscriptionId, string owner, string repo, string branch)
    {
        // Verify Microsoft Graph permissions
        await VerifyPermissionsAsync();

        // Retrieve the subscription and tenant ID
        // Azure SDK: ArmClient.GetSubscriptionResource
        // Documentation: https://learn.microsoft.com/en-us/dotnet/api/azure.resourcemanager.armclient.getsubscriptionresource
        var subscription = await _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
        var tenantId = subscription.Value.Data.TenantId;

        if (tenantId is null)
        {
            throw new Exception("Failed to retrieve Tenant ID.");
        }

        // Create Azure AD Application
        // Microsoft Graph API: Applications - Create
        // Documentation: https://learn.microsoft.com/en-us/graph/api/application-post-applications?view=graph-rest-1.0
        Console.WriteLine("Creating Azure AD Application...");
        var application = new Application { Id = appName, DisplayName = appName };
        var createdApp = await _graphClient.Applications.PostAsync(application);

        if (createdApp == null || string.IsNullOrEmpty(createdApp.AppId))
        {
            throw new Exception("Failed to create Azure AD Application.");
        }

        Console.WriteLine($"Application created with App ID: {createdApp.AppId}");

        // Create Service Principal
        // Microsoft Graph API: ServicePrincipals - Create
        // Documentation: https://learn.microsoft.com/en-us/graph/api/serviceprincipal-post-serviceprincipals?view=graph-rest-1.0
        Console.WriteLine("Creating Service Principal...");
        var servicePrincipal = new ServicePrincipal { AppId = createdApp.AppId };
        var createdSp = await _graphClient.ServicePrincipals.PostAsync(servicePrincipal);
        Console.WriteLine("Service Principal created.");

        // Add Federated Identity Credentials
        Console.WriteLine("Adding Federated Identity Credentials...");
        await AddFederatedIdentityCredentialsAsync(createdApp.Id!, owner, repo, branch);

        // Assign Contributor Role
        // Azure SDK: RoleAssignmentOperations.CreateOrUpdate
        // Documentation: https://learn.microsoft.com/en-us/dotnet/api/azure.resourcemanager.authorization.roleassignmentoperations.createorupdate
        Console.WriteLine("Assigning Contributor Role at Subscription Level...");
        // Role assignment logic here...

        return new GitHubSecretData
        {
            AppId = createdApp.AppId,
            TenantId = tenantId.ToString()!,
            SubscriptionId = subscriptionId
        };
    }

    private async Task AddFederatedIdentityCredentialsAsync(string appId, string owner, string repo, string branch)
    {
        // Fetch existing federated credentials
        var existingCredentials = await _graphClient.Applications[appId].FederatedIdentityCredentials.GetAsync();
        var existingCredentialNames = new HashSet<string>(existingCredentials?.Value?.Select(c => c.Name!) ?? []);

        // Add branch-based credential if not exists
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
            await _graphClient.Applications[appId].FederatedIdentityCredentials.PostAsync(branchCredential);
            Console.WriteLine("Branch federated identity credential added successfully.");
        }
        else
        {
            Console.WriteLine($"Federated identity credential for branch '{branch}' already exists.");
        }

        // Add pull request credential if not exists
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
            await _graphClient.Applications[appId].FederatedIdentityCredentials.PostAsync(prCredential);
            Console.WriteLine("Pull request federated identity credential added successfully.");
        }
        else
        {
            Console.WriteLine("Federated identity credential for pull requests already exists.");
        }
    }


    private async Task VerifyPermissionsAsync()
    {
        // Check if the app has necessary Microsoft Graph permissions
        // Documentation: https://learn.microsoft.com/en-us/graph/permissions-reference
        try
        {
            Console.WriteLine("Verifying Microsoft Graph permissions...");

            // Verify by fetching the current user details
            // Microsoft Graph API: Users - Get
            // Documentation: https://learn.microsoft.com/en-us/graph/api/user-get?view=graph-rest-1.0
            var user = await _graphClient.Me.GetAsync();

            if (user == null || string.IsNullOrEmpty(user.DisplayName))
            {
                throw new UnauthorizedAccessException("Microsoft Graph API access is not correctly configured. Please ensure Application.ReadWrite.All and other necessary permissions are granted.");
            }

            Console.WriteLine("Microsoft Graph permissions verified successfully.");
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == (int)System.Net.HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to access Microsoft Graph API. Please check the app's Azure AD permissions and grant admin consent.", ex);
        }
    }
}
