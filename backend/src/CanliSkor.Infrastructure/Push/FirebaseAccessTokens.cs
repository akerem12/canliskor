using System.Buffers.Text;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanliSkor.Infrastructure.Push;

/// <summary>
/// Firebase only takes messages from a caller holding a short-lived access token. Google hands one out in
/// exchange for a note signed with the service account's private key; this class does that exchange and keeps
/// the token until shortly before it runs out (they last an hour).
/// </summary>
internal sealed partial class FirebaseAccessTokens(
    IHttpClientFactory httpClientFactory,
    IOptions<FirebaseOptions> options,
    TimeProvider timeProvider,
    ILogger<FirebaseAccessTokens> logger)
{
    public const string HttpClientName = "firebase-token";

    // Fixed, never taken from the key file: the signed note must only ever go to Google.
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    private readonly Lazy<ServiceAccount?> _account = new(() => Parse(options.Value.ServiceAccount, logger));
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expires;

    /// <summary>The Firebase project messages are sent in; null if no (usable) service account is configured.</summary>
    public string? ProjectId => _account.Value?.ProjectId;

    /// <returns>Null if nothing is configured.</returns>
    /// <exception cref="HttpRequestException">Google refused the exchange or couldn't be reached.</exception>
    public async Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        if (_account.Value is not { } account)
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (_token is null || now >= _expires - Margin)
            {
                using var client = httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = SignedNote(account, now),
                }), cancellationToken);
                response.EnsureSuccessStatusCode();

                var granted = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                    ?? throw new HttpRequestException("Google answered the token request with nothing.");
                _token = granted.AccessToken;
                _expires = now.AddSeconds(granted.ExpiresIn);
            }

            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Firebase said the token is no good: the next call asks for a new one.</summary>
    public void Forget() => _token = null;

    /// <summary>A JWT: who we are, what we want to do and until when, signed with the account's key (RS256).</summary>
    internal static string SignedNote(ServiceAccount account, DateTimeOffset now)
    {
        var header = Base64Url.EncodeToString("""{"alg":"RS256","typ":"JWT"}"""u8);
        var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iss"] = account.ClientEmail,
            ["scope"] = Scope,
            ["aud"] = TokenUrl,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(30).ToUnixTimeSeconds(),
        }));

        using var key = RSA.Create();
        key.ImportFromPem(account.PrivateKey);
        var signature = key.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{Base64Url.EncodeToString(signature)}";
    }

    private static ServiceAccount? Parse(string? json, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            LogNotConfigured(logger);
            return null;
        }

        try
        {
            var account = JsonSerializer.Deserialize<ServiceAccount>(json);
            if (account is { ProjectId.Length: > 0, ClientEmail.Length: > 0, PrivateKey.Length: > 0 })
            {
                // Fails here, once, rather than on every send, if the key isn't one.
                using var key = RSA.Create();
                key.ImportFromPem(account.PrivateKey);
                return account;
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or CryptographicException)
        {
            // Only the kind of error is logged: the text is a secret.
            LogUnusable(logger, ex.GetType().Name);
            return null;
        }

        LogUnusable(logger, "missing project_id, client_email or private_key");
        return null;
    }

    internal sealed record ServiceAccount(
        [property: JsonPropertyName("project_id")] string ProjectId,
        [property: JsonPropertyName("client_email")] string ClientEmail,
        [property: JsonPropertyName("private_key")] string PrivateKey);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    [LoggerMessage(Level = LogLevel.Information, Message = "No Firebase service account configured: the Android app gets no notifications")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The Firebase service account can't be used ({Problem}): the Android app gets no notifications")]
    private static partial void LogUnusable(ILogger logger, string problem);
}
