using Npgsql;

namespace CanliSkor.Infrastructure.Storage;

/// <summary>
/// Hosts hand out a database as one address (<c>postgresql://user:password@host/database?sslmode=require</c>);
/// the driver wants its own "Host=...;Username=..." form. This turns the one into the other.
/// </summary>
internal static class DatabaseUrl
{
    /// <summary>The name hosts use for it, as an environment variable.</summary>
    public const string SettingName = "DATABASE_URL";

    /// <exception cref="FormatException">Not a postgres address. The message never repeats the address: it holds the password.</exception>
    public static string ToConnectionString(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("postgresql" or "postgres"))
        {
            throw new FormatException($"{SettingName} must look like postgresql://user:password@host/database.");
        }

        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            // A handful is plenty, and the free database plan counts connections.
            MaxPoolSize = 5,
            // Idle connections are closed soon, so a quiet site lets the database go to sleep.
            ConnectionIdleLifetime = 30,
            ConnectionPruningInterval = 10,
            Timeout = 15,
            CommandTimeout = 15,
        };
        if (uri.Port > 0)
        {
            builder.Port = uri.Port;
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]).Replace("-", "", StringComparison.Ordinal) : "";

            // "verify-full" is VerifyFull to the driver. Other options (there are dozens) aren't ours to guess at.
            if (parts[0] == "sslmode" && Enum.TryParse<SslMode>(value, ignoreCase: true, out var sslMode))
            {
                builder.SslMode = sslMode;
            }
            else if (parts[0] == "channel_binding" && Enum.TryParse<ChannelBinding>(value, ignoreCase: true, out var channelBinding))
            {
                builder.ChannelBinding = channelBinding;
            }
        }

        return builder.ConnectionString;
    }
}
