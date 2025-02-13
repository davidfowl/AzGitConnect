﻿using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AzGitConnect;

// This is a github app that supports device flow
const string GitHubClientId = "Ov23liBhP6pOLo4HJgKO";

var subscriptionIdOption = new Option<string>(["-s", "--subscription-id"], "Azure Subscription ID") { IsRequired = true };
var repoOption = new Option<string>(["-r", "--repository"], "GitHub repository in the format owner/repo") { IsRequired = true };
var appName = new Option<string>(["-n", "--name"], "Name of the Azure AD Application to create");
var useAzCli = new Option<bool>("--use-az-cli", () => true, "Use the Azure CLI for management operations");

var rootCommand = new RootCommand("CLI tool for configuring GitHub Actions with Azure authentication.")
{
    subscriptionIdOption,
    repoOption,
    appName,
    useAzCli
};

rootCommand.SetHandler(RunAsync, subscriptionIdOption, repoOption, appName, useAzCli);

await rootCommand.InvokeAsync(args);

static async Task RunAsync(string subscriptionId, string fullRepo, string appName, bool useAzCli)
{
    var (owner, repo) = fullRepo.Split('/') switch
    {
    [var r, var b] => (r, b),
        _ => throw new ArgumentException("Invalid repository format. Please use the format 'owner/repo'.")
    };

    appName ??= $"gh-{fullRepo.Replace('/', '-')}";

    IEntraManager azureManager = useAzCli ? new AzCliAzureEntraManager() : new AzureEntraManager();
    var githubSecretData = await azureManager.CreateAzureApplicationForGitHubAsync(appName, subscriptionId, owner, repo, "main");

    Console.WriteLine("Starting GitHub OAuth flow...");
    var accessTokenManager = new GithubAccessTokenManager(GitHubClientId);
    string githubToken = await accessTokenManager.GetAccessTokenAsync();

    Console.WriteLine("Fetching GitHub repository public key...");
    var secretManager = new GitHubSecretManager(fullRepo, githubToken);
    var publicKey = await secretManager.GetGitHubPublicKeyAsync();

    Console.WriteLine("Encrypting and setting secrets...");
    await secretManager.SetGitHubSecretAsync("AZURE_CLIENT_ID", githubSecretData.AppId, publicKey);
    await secretManager.SetGitHubSecretAsync("AZURE_TENANT_ID", githubSecretData.TenantId, publicKey);
    await secretManager.SetGitHubSecretAsync("AZURE_SUBSCRIPTION_ID", subscriptionId, publicKey);

    Console.WriteLine("Secrets configured successfully.");
}
