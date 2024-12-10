using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GithubAzureUtility;

internal class AzureEntraManager
{
    public async Task<GitHubSecretData> CreateAzureApplicationForGitHubAsync(string appName, string subscriptionId, string owner, string repo, string branch)
    {
        string tenantId = await GetTenantIdAsync();

        Console.WriteLine("Creating Azure AD Application...");
        string appCreateOutput = await ExecuteCommand("az", $"ad app create --display-name \"{appName}\"");
        var appDetails = JsonSerializer.Deserialize<AzureApplication>(appCreateOutput)!;
        Console.WriteLine($"Application created with App ID: {appDetails.AppId}");

        Console.WriteLine("Creating Service Principal...");
        await EnsureServicePrincipalExists(appDetails.AppId);

        await AddFederatedIdentityCredential(appDetails.AppId, owner, repo, branch);

        Console.WriteLine("Assigning Contributor Role at Subscription Level...");
        await ExecuteCommand("az", $"role assignment create --assignee \"{appDetails.AppId}\" --role \"Contributor\" --scope \"/subscriptions/{subscriptionId}\"");
        Console.WriteLine("Role assignment successful.");

        return new GitHubSecretData
        {
            AppId = appDetails.AppId,
            TenantId = tenantId,
            SubscriptionId = subscriptionId
        };
    }

    public static async Task EnsureServicePrincipalExists(string appId)
    {
        try
        {
            var output = await ExecuteCommand("az", $"ad sp show --id \"{appId}\"");

            // If the command succeeds, the Service Principal exists
            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine($"Service principal exists for application {appId}");
                return;
            }
        }
        catch
        {

        }

        await ExecuteCommand("az", $"ad sp create --id \"{appId}\"");
    }

    static async Task AddFederatedIdentityCredential(string appId, string owner, string repo, string branch)
    {
        // Get the list of federated credentials
        static async Task<Dictionary<string, FederatedIdentityCredential>> GetFederatedIdentityCredentials(string appId)
        {
            var output = await ExecuteCommand("az", $"ad app federated-credential list --id \"{appId}\"");
            return (JsonSerializer.Deserialize<FederatedIdentityCredential[]>(output) ?? []).ToDictionary(c => c.Name);
        }

        Console.WriteLine("Adding Federated Identity Credential...");

        var credentials = await GetFederatedIdentityCredentials(appId);

        if (!credentials.ContainsKey($"gh-{branch}"))
        {
            await CreateFederatedIdentityCredential(appId, $"gh-{branch}", $"repo:{owner}/{repo}:ref:refs/heads/{branch}");
        }
        else
        {
            Console.WriteLine($"Federated Identity Credential for {branch} already exists.");
        }

        if (!credentials.ContainsKey("gh-pr"))
        {
            await CreateFederatedIdentityCredential(appId, "gh-pr", $"repo:{owner}/{repo}:pull_request");
        }
        else
        {
            Console.WriteLine("Federated Identity Credential for pull requests already exists.");
        }

        static async Task CreateFederatedIdentityCredential(string appId, string credentialName, string subject)
        {
            Console.WriteLine($"Creating Federated Identity Credential {credentialName}");

            string parameters =
                $$"""
                {
                    "name": "{{credentialName}}",
                    "issuer": "https://token.actions.githubusercontent.com",
                    "subject": "{{subject}}",
                    "audiences": ["api://AzureADTokenExchange"]
                }
                """;

            var dir = Directory.CreateTempSubdirectory("azure-github-cli");
            var parametersFile = Path.Combine(dir.FullName, "parameters.json");

            File.WriteAllText(parametersFile, parameters);

            await ExecuteCommand("az", $"ad app federated-credential create --id \"{appId}\" --parameters \"{parametersFile}\"");
            
            Console.WriteLine("Federated Identity Credential added successfully.");

            dir.Delete(true);
        }
    }

    static async Task<string> GetTenantIdAsync()
    {
        Console.WriteLine("Fetching Tenant ID...");
        string tenantId = await ExecuteCommand("az", "account show --query tenantId -o tsv");
        Console.WriteLine($"Tenant ID: {tenantId}");
        return tenantId.Trim();
    }

    static async Task<string> ExecuteCommand(string command, string args)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Executing: {command} {args}");
        Console.ResetColor();

        var fullCommand = ExecutableUtil.FindFullPathFromPath(command) ?? throw new Exception($"Command '{command}' not found on the PATH.");

        var sb = new StringBuilder();

        var spec = new ProcessSpec(fullCommand)
        {
            Arguments = args,
            OnErrorData = s =>
            {
                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(s);
                    Console.ResetColor();
                }
            },
            OnOutputData = s =>
            {
                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(s);
                    Console.ResetColor();
                }

                sb.AppendLine(s);
            }
        };

        var (task, disposable) = ProcessUtil.Run(spec);

        var result = await task;

        await disposable.DisposeAsync();

        return sb.ToString();
    }

    private class AzureApplication
    {
        [JsonPropertyName("appId")]
        public required string AppId { get; set; }
    }

    private class FederatedIdentityCredential
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
    }
}

public class GitHubSecretData
{
    public required string TenantId { get; set; }
    public required string SubscriptionId { get; set; }
    public required string AppId { get; set; }
}
