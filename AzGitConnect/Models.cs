using System.Text.Json.Serialization;

namespace AzGitConnect;

public class GitHubPublicKey
{
    [JsonPropertyName("key_id")]
    public required string KeyId { get; set; }

    [JsonPropertyName("key")]
    public required string Key { get; set; }
}

public class GithubSecret
{
    [JsonPropertyName("encrypted_value")]
    public required string EncryptedValue { get; set; }

    [JsonPropertyName("key_id")]
    public required string KeyId { get; set; }
}


public class DeviceCodeResponse
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

public class AccessTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

public class Application
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

public class ServicePrincipal
{
    [JsonPropertyName("appId")]
    public string? AppId { get; set; }
}

public class FederatedIdentityCredential
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("audiences")]
    public string[]? Audiences { get; set; }
}

public class GraphApiResponse<T>
{
    [JsonPropertyName("value")]
    public List<T>? Value { get; set; }
}

public class GraphApiError
{
    [JsonPropertyName("error")]
    public GraphApiErrorDetails? Error { get; set; }
}

public class GraphApiErrorDetails
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    [JsonPropertyName("innerError")]
    public GraphApiInnerError? InnerError { get; set; }
}

public class GraphApiInnerError
{
    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; set; }
    [JsonPropertyName("request-id")]
    public string? RequestId { get; set; }
    [JsonPropertyName("client-request-id")]
    public string? ClientRequestId { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GitHubPublicKey))]
[JsonSerializable(typeof(DeviceCodeResponse))]
[JsonSerializable(typeof(AccessTokenResponse))]
[JsonSerializable(typeof(Application))]
[JsonSerializable(typeof(FederatedIdentityCredential[]))]
[JsonSerializable(typeof(ServicePrincipal))]
[JsonSerializable(typeof(ServicePrincipal))]
[JsonSerializable(typeof(GithubSecret))]
[JsonSerializable(typeof(GraphApiResponse<Application>))]
[JsonSerializable(typeof(GraphApiResponse<FederatedIdentityCredential>))]
[JsonSerializable(typeof(GraphApiError))]
public partial class AppJsonContext : JsonSerializerContext
{
}
