using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Sodium;

namespace AzGitConnect;

internal class GitHubSecretManager(string repo, string accessToken)
{
    private readonly HttpClient _httpClient = InitializeHttpClient(accessToken);

    private static HttpClient InitializeHttpClient(string accessToken)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var an = typeof(Program).Assembly.GetName();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(an.Name!, $"{an.Version}"));
        return httpClient;
    }

    public async Task<GitHubPublicKey> GetGitHubPublicKeyAsync()
    {
        return (await _httpClient.GetFromJsonAsync($"https://api.github.com/repos/{repo}/actions/secrets/public-key", AppJsonContext.Default.GitHubPublicKey))!;
    }

    public async Task SetGitHubSecretAsync(string secretName, string secretValue, GitHubPublicKey publicKey)
    {
        // Encrypt the secret value
        string encryptedValue = EncryptSecret(secretValue, publicKey.Key);

        // Prepare the API request payload
        var payload = new GithubSecret
        {
            EncryptedValue = encryptedValue,
            KeyId = publicKey.KeyId
        };

        string url = $"https://api.github.com/repos/{repo}/actions/secrets/{secretName}";
        var response = await _httpClient.PutAsJsonAsync(url, payload, AppJsonContext.Default.GithubSecret);

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
