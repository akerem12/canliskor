using System.Net;

namespace CanliSkor.Api.Tests;

public class MobileAppCorsTests(MatchEndpointsTests.Factory factory) : IClassFixture<MatchEndpointsTests.Factory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("https://localhost")]
    [InlineData("http://localhost")]
    [InlineData("capacitor://localhost")]
    public async Task The_app_is_allowed_to_read_the_api(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/leagues");
        request.Headers.Add("Origin", origin);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task The_app_is_allowed_to_connect_to_the_hub()
    {
        // What the browser asks before SignalR's first request, which carries a header of its own.
        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/live-scores/negotiate");
        request.Headers.Add("Origin", "https://localhost");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-requested-with,x-signalr-user-agent");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://localhost", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://localhost.example.com")]
    [InlineData("https://localhost:8443")]
    public async Task Other_sites_are_not(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/leagues");
        request.Headers.Add("Origin", origin);

        using var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
