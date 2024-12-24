using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzGitConnect;

public class GithubAccessTokenManager(string clientId)
{
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string AccessTokenUrl = "https://github.com/login/oauth/access_token";

    // TODO: Cache the access token
    public async Task<string> GetAccessTokenAsync()
    {
        // Step 1: Request the device and user codes
        var deviceCodeResponse = await RequestDeviceCodeAsync(clientId);

        // Step 2: Display instructions to the user
        DisplayUserInstructions(deviceCodeResponse);

        // Step 3: Poll for access token
        var accessToken = await PollForAccessTokenAsync(clientId, deviceCodeResponse.DeviceCode, deviceCodeResponse.Interval, deviceCodeResponse.ExpiresIn + 100);

        return accessToken;
    }

    private async Task<DeviceCodeResponse> RequestDeviceCodeAsync(string clientId)
    {
        using var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("scope", "repo read:org")
        ]);

        var response = await httpClient.PostAsync(DeviceCodeUrl, content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DeviceCodeResponse>()
            ?? throw new Exception("Failed to retrieve device code.");
    }

    private void DisplayUserInstructions(DeviceCodeResponse deviceCodeResponse)
    {
        Console.WriteLine("To authenticate, please visit the following URL in your browser:");
        Console.WriteLine(deviceCodeResponse.VerificationUri);
        Console.WriteLine($"Enter the following code: {deviceCodeResponse.UserCode}");
    }

    private async Task<string> PollForAccessTokenAsync(string clientId, string deviceCode, int interval, int expiresIn)
    {
        using var httpClient = new HttpClient();

        // JSON
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var content = new FormUrlEncodedContent(
        [
            KeyValuePair.Create("client_id", clientId),
            KeyValuePair.Create("device_code", deviceCode),
            KeyValuePair.Create("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
        ]);

        var startTime = DateTime.UtcNow;

        while (true)
        {
            var elapsedTime = DateTime.UtcNow - startTime;
            if (elapsedTime.TotalSeconds >= expiresIn)
            {
                throw new Exception("Device code has expired. Please restart the authentication process.");
            }

            await Task.Delay(interval * 1000);

            var response = await httpClient.PostAsync(AccessTokenUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var accessTokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(responseBody);
                if (accessTokenResponse?.AccessToken is string accessToken)
                {
                    return accessToken;
                }
            }

            if (responseBody.Contains("authorization_pending"))
            {
                continue;
            }

            if (responseBody.Contains("slow_down"))
            {
                interval += 5;
                continue;
            }
        }
    }

    private class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public required string DeviceCode { get; set; }

        [JsonPropertyName("user_code")]
        public required string UserCode { get; set; }

        [JsonPropertyName("verification_uri")]
        public required string VerificationUri { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }

    private class AccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }
}
