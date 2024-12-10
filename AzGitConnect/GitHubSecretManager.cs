using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Sodium;

namespace GithubAzureUtility;

internal class GitHubSecretManager(string repo, string accessToken)
{
    public async Task<GitHubPublicKey> GetGitHubPublicKeyAsync()
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Azure-GitHub-CLI");

        return (await httpClient.GetFromJsonAsync<GitHubPublicKey>($"https://api.github.com/repos/{repo}/actions/secrets/public-key"))!;
    }

    public async Task SetGitHubSecretAsync(string secretName, string secretValue, GitHubPublicKey publicKey)
    {
        // Encrypt the secret value
        string encryptedValue = EncryptSecret(secretValue, publicKey.Key);

        // Prepare the API request payload
        var payload = new
        {
            encrypted_value = encryptedValue,
            key_id = publicKey.KeyId
        };

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Azure-GitHub-CLI");

        string url = $"https://api.github.com/repos/{repo}/actions/secrets/{secretName}";
        var response = await httpClient.PutAsJsonAsync(url, payload);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Secret '{secretName}' set successfully.");
        }
        else
        {
            Console.WriteLine($"Failed to set secret '{secretName}': {response.StatusCode}");
            string errorResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error Details: {errorResponse}");
        }
    }

    static string EncryptSecret(string secretValue, string publicKeyBase64)
    {
        byte[] secretBytes = Encoding.UTF8.GetBytes(secretValue);
        byte[] publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
        byte[] sealedPublicKeyBox = SealedPublicKeyBox.Create(secretBytes, publicKeyBytes);

        return Convert.ToBase64String(sealedPublicKeyBox);
    }
}

public class GitHubPublicKey
{
    [JsonPropertyName("key_id")]
    public required string KeyId { get; set; }

    [JsonPropertyName("key")]
    public required string Key { get; set; }
}
