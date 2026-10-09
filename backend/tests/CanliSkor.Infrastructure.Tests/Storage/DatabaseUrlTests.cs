using CanliSkor.Infrastructure.Storage;
using Npgsql;

namespace CanliSkor.Infrastructure.Tests.Storage;

public class DatabaseUrlTests
{
    [Fact]
    public void A_hosts_address_becomes_the_drivers_connection_string()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToConnectionString(
            "postgresql://owner:p%40ss%3Aword@ep-example-pooler.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"));

        Assert.Equal("ep-example-pooler.eu-central-1.aws.neon.tech", connection.Host);
        Assert.Equal(5432, connection.Port);
        Assert.Equal("owner", connection.Username);
        Assert.Equal("p@ss:word", connection.Password);
        Assert.Equal("neondb", connection.Database);
        Assert.Equal(SslMode.Require, connection.SslMode);
        Assert.Equal(ChannelBinding.Require, connection.ChannelBinding);
    }

    [Fact]
    public void Port_and_the_other_spelling_of_the_scheme_are_understood()
    {
        var connection = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToConnectionString("postgres://u:p@localhost:6543/db?sslmode=verify-full"));

        Assert.Equal(6543, connection.Port);
        Assert.Equal(SslMode.VerifyFull, connection.SslMode);
    }

    [Theory]
    [InlineData("Host=localhost;Username=u;Password=secret")]
    [InlineData("https://user:secret@example.org/db")]
    [InlineData("")]
    public void Anything_else_is_refused_without_repeating_it(string url)
    {
        var error = Assert.Throws<FormatException>(() => DatabaseUrl.ToConnectionString(url));

        Assert.DoesNotContain("secret", error.Message);
    }
}
