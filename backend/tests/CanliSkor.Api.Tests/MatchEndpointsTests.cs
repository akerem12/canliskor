using System.Net;
using System.Text.Json;
using CanliSkor.Api.Workers;
using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace CanliSkor.Api.Tests;

/// <summary>
/// Boots the real app in memory (real DI, endpoints, JSON settings) with the poller switched off,
/// a fixed clock and a seeded store — so we assert exactly what clients receive.
/// </summary>
public class MatchEndpointsTests : IClassFixture<MatchEndpointsTests.Factory>
{
    // 17:20 UTC = 20:20 in Istanbul.
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 17, 20, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 9);

    private readonly HttpClient _client;

    public MatchEndpointsTests(Factory factory)
    {
        _client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IMatchStore>();
        store.SetAsync(new ScoreboardSnapshot(
            new LeagueScoreboard(new League("tur.1", "Turkish Super Lig"), Today,
            [
                new Match("1", "tur.1", Now.AddMinutes(-20), MatchStatus.Live, "20'",
                    new Team("432", "Galatasaray", "Galatasaray", "https://logo/432.png"),
                    new Team("6870", "Kasimpasa", "Kasimpasa", null),
                    new Score(1, 0)),
                new Match("2", "tur.1", Now.AddHours(2), MatchStatus.Scheduled, null,
                    new Team("436", "Fenerbahce", "Fenerbahce", null),
                    new Team("1895", "Besiktas", "Besiktas", null),
                    null),
            ]),
            Now)).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Get_matches_returns_today_grouped_by_league_in_istanbul_time()
    {
        using var json = await GetJson("/api/matches");
        var root = json.RootElement;

        Assert.Equal("2026-10-09", root.GetProperty("date").GetString());
        var league = Assert.Single(root.GetProperty("leagues").EnumerateArray());
        Assert.Equal("tur.1", league.GetProperty("code").GetString());

        var matches = league.GetProperty("matches").EnumerateArray().ToList();
        Assert.Equal(2, matches.Count);

        var live = matches[0];
        Assert.Equal("Live", live.GetProperty("status").GetString());
        Assert.Equal("2026-10-09T20:00:00+03:00", live.GetProperty("kickoff").GetString());
        Assert.Equal("20'", live.GetProperty("clock").GetString());
        Assert.Equal("Galatasaray", live.GetProperty("homeTeam").GetProperty("name").GetString());
        Assert.Equal(1, live.GetProperty("score").GetProperty("home").GetInt32());
        Assert.Equal(0, live.GetProperty("score").GetProperty("away").GetInt32());

        var scheduled = matches[1];
        Assert.Equal("Scheduled", scheduled.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, scheduled.GetProperty("score").ValueKind);
    }

    [Fact]
    public async Task Get_live_returns_only_in_play_matches()
    {
        using var json = await GetJson("/api/matches/live");

        var league = Assert.Single(json.RootElement.EnumerateArray());
        var match = Assert.Single(league.GetProperty("matches").EnumerateArray());
        Assert.Equal("1", match.GetProperty("id").GetString());
    }

    private async Task<JsonDocument> GetJson(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
            {
                // No background polling (and no calls to ESPN) during tests.
                var worker = services.Single(d => d.ImplementationType == typeof(ScoreboardPollingWorker));
                services.Remove(worker);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
            });
    }
}
