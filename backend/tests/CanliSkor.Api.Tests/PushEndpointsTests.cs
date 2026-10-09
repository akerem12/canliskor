using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanliSkor.Core.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace CanliSkor.Api.Tests;

public class PushEndpointsTests(MatchEndpointsTests.Factory factory) : IClassFixture<MatchEndpointsTests.Factory>
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/abc";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly PushSubscriberRegistry _registry = factory.Services.GetRequiredService<PushSubscriberRegistry>();

    [Fact]
    public async Task Key_is_a_public_key_browsers_can_subscribe_with()
    {
        using var json = JsonDocument.Parse(await _client.GetStringAsync("/api/push/key"));

        // 65 bytes in base64url.
        Assert.Equal(87, json.RootElement.GetProperty("publicKey").GetString()!.Length);
    }

    [Fact]
    public async Task Subscribing_registers_the_browser_and_subscribing_again_updates_it()
    {
        var first = await _client.PutAsJsonAsync("/api/push/subscription", Registration(teamIds: ["432"]));
        var second = await _client.PutAsJsonAsync("/api/push/subscription", Registration(teamIds: ["432", "436"], language: "tr"));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var subscriber = Assert.Single(_registry.All(), s => s.Endpoint == Endpoint);
        Assert.Equal(["432", "436"], subscriber.TeamIds.Order());
        Assert.Equal("tr", subscriber.Language);
        Assert.True(subscriber.KickoffReminder);
        Assert.False(subscriber.LineupAlerts);
    }

    [Fact]
    public async Task Unsubscribing_forgets_the_browser()
    {
        const string endpoint = "https://fcm.googleapis.com/fcm/send/leaving";
        await _client.PutAsJsonAsync("/api/push/subscription", Registration(endpoint: endpoint));

        var response = await _client.DeleteAsync($"/api/push/subscription?endpoint={Uri.EscapeDataString(endpoint)}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(_registry.All(), s => s.Endpoint == endpoint);
    }

    [Theory]
    [InlineData("https://example.org/push")]
    [InlineData("http://localhost:5272/health")]
    [InlineData("")]
    public async Task An_endpoint_that_is_no_push_service_is_refused(string endpoint)
    {
        var response = await _client.PutAsJsonAsync("/api/push/subscription", Registration(endpoint: endpoint));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_subscription_without_keys_is_refused()
    {
        var response = await _client.PutAsJsonAsync("/api/push/subscription", new { endpoint = Endpoint, kickoffReminder = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object Registration(string endpoint = Endpoint, string[]? teamIds = null, string language = "en") => new
    {
        endpoint,
        keys = new { p256dh = "BPublicKey", auth = "secret" },
        language,
        teamIds = teamIds ?? [],
        matchIds = Array.Empty<string>(),
        kickoffReminder = true,
        lineupAlerts = false,
    };
}
