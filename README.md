# AzGitConnect

**AzGitConnect** is a CLI tool that simplifies the integration of **Azure** and **GitHub Actions** for seamless CI/CD workflows. It automates the configuration of GitHub secrets, Azure resources, and federated identity credentials to enable secure and efficient deployments.

## Features

- **Federated Identity Setup**:
  - Automatically configures Azure AD applications with federated identity for GitHub Actions.
- **GitHub Secrets Management**:
  - Encrypt and set repository secrets for use in GitHub Actions.

## Getting Started

### Prerequisites

1. **Azure CLI**:
   - Install Azure CLI from [Microsoft Docs](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli).
2. **GitHub Account**:
   - Ensure access to your GitHub repositories.
3. **Dotnet SDK**:
   - Install the [.NET SDK](https://dotnet.microsoft.com/download).

### Run the tool

1. Run `az login` to setup your Azure account.
1. Run `azgh -s [YOUR-AZURE-SUBSCRIPTION-ID] -r [YOUR-GITHUB-REPOSITORY]` to create the default resources to connect Azure and GitHub Actions.  For example, `azgh -s 3c12b179-d7f9-4970-bb63-825995a1955c -r davidfowl/AzGitConnect`.