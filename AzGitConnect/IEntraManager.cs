
namespace AzGitConnect;

internal interface IEntraManager
{
    Task<GitHubSecretData> CreateAzureApplicationForGitHubAsync(string appName, string subscriptionId, string owner, string repo, string branch);
}