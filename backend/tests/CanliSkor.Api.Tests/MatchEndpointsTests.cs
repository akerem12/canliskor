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

    [Fact]
    public async Task Get_matches_for_another_day_loads_it_on_demand()
    {
        using var json = await GetJson("/api/matches?date=2026-10-04");

        Assert.Equal("2026-10-04", json.RootElement.GetProperty("date").GetString());
        var league = Assert.Single(json.RootElement.GetProperty("leagues").EnumerateArray());
        var match = Assert.Single(league.GetProperty("matches").EnumerateArray());
        Assert.Equal("Finished", match.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_matches_rejects_dates_more_than_a_week_away()
    {
        var response = await _client.GetAsync("/api/matches?date=2026-12-25");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("date", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_match_detail_returns_events_and_stats()
    {
        using var json = await GetJson("/api/leagues/tur.1/matches/77");
        var root = json.RootElement;

        Assert.Equal("77", root.GetProperty("match").GetProperty("id").GetString());
        Assert.Equal("2026-10-09T20:00:00+03:00", root.GetProperty("match").GetProperty("kickoff").GetString());

        var goal = Assert.Single(root.GetProperty("events").EnumerateArray());
        Assert.Equal("PenaltyGoal", goal.GetProperty("type").GetString());
        Assert.Equal("Home", goal.GetProperty("side").GetString());
        Assert.Equal("12'", goal.GetProperty("clock").GetString());
        Assert.Equal("Icardi", goal.GetProperty("player").GetString());
        Assert.Equal(JsonValueKind.Null, goal.GetProperty("relatedPlayer").ValueKind);

        var stat = Assert.Single(root.GetProperty("stats").EnumerateArray());
        Assert.Equal("Possession", stat.GetProperty("type").GetString());
        Assert.Equal(61.5, stat.GetProperty("home").GetDouble());
        Assert.Equal(Now, root.GetProperty("lastUpdatedUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task Get_match_detail_returns_lineups()
    {
        using var json = await GetJson("/api/leagues/tur.1/matches/77");
        var home = json.RootElement.GetProperty("lineups").GetProperty("home");

        Assert.Equal("4-4-2", home.GetProperty("formation").GetString());
        Assert.Equal("#fdb912", home.GetProperty("shirtColor").GetString());
        Assert.Equal(2, home.GetProperty("rows").GetArrayLength());

        var striker = home.GetProperty("rows")[1][0];
        Assert.Equal("Icardi", striker.GetProperty("name").GetString());
        Assert.Equal("9", striker.GetProperty("jersey").GetString());
        Assert.Equal("Forward", striker.GetProperty("position").GetString());
        Assert.Equal("80'", striker.GetProperty("wentOffAt").GetString());
        Assert.Equal(1, striker.GetProperty("stats").GetProperty("goals").GetInt32());

        // The match is live at 20'.
        Assert.Equal(20, striker.GetProperty("minutesPlayed").GetInt32());

        var unused = Assert.Single(home.GetProperty("bench").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, unused.GetProperty("position").ValueKind);
        Assert.Equal(JsonValueKind.Null, unused.GetProperty("minutesPlayed").ValueKind);
    }

    [Theory]
    [InlineData("/api/leagues/tur.1/matches/404")] // unknown to the provider
    [InlineData("/api/leagues/ger.1/matches/77")]  // league not followed
    [InlineData("/api/leagues/tur.1/matches/abc")] // not an ESPN id
    public async Task Get_match_detail_returns_404_for_unknown_matches(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_squad_returns_the_players()
    {
        using var json = await GetJson("/api/leagues/tur.1/teams/432/squad");
        var root = json.RootElement;

        Assert.Equal("432", root.GetProperty("teamId").GetString());
        Assert.Equal("Galatasaray", root.GetProperty("teamName").GetString());
        Assert.Equal(Now, root.GetProperty("lastUpdatedUtc").GetDateTimeOffset());

        var players = root.GetProperty("players");
        Assert.Equal(3, players.GetArrayLength());
        Assert.Equal("Icardi", players[1].GetProperty("name").GetString());
        Assert.Equal("9", players[1].GetProperty("jersey").GetString());
        Assert.Equal("Forward", players[1].GetProperty("position").GetString());
        Assert.Equal(33, players[1].GetProperty("age").GetInt32());
        Assert.Equal("Argentina", players[1].GetProperty("nationality").GetString());
        Assert.Equal(JsonValueKind.Null, players[2].GetProperty("position").ValueKind);
    }

    [Theory]
    [InlineData("/api/leagues/tur.1/teams/404/squad")] // unknown to the provider
    [InlineData("/api/leagues/ger.1/teams/432/squad")] // league not followed
    [InlineData("/api/leagues/tur.1/teams/abc/squad")] // not an ESPN id
    public async Task Get_squad_returns_404_for_unknown_teams(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_leagues_returns_followed_league_codes_in_order()
    {
        using var json = await GetJson("/api/leagues");

        Assert.Equal(
            ["tur.1", "eng.1", "esp.1", "uefa.champions", "uefa.europa", "uefa.nations", "arg.1", "bra.1", "col.1", "chi.1", "fifa.friendly"],
            json.RootElement.EnumerateArray().Select(e => e.GetString()));
    }

    private async Task<JsonDocument> GetJson(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    public class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
            {
                // No background polling (and no calls to ESPN) during tests.
                var worker = services.Single(d => d.ImplementationType == typeof(ScoreboardPollingWorker));
                services.Remove(worker);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
                // Browsing other days may call the provider: never let tests reach ESPN.
                services.AddScoped<IFootballDataProvider, StubFootballDataProvider>();
            });
    }
}

/// <summary>
/// Any day: one finished match for tur.1, nothing for the other leagues. Details: only match 77 of tur.1.
/// Squads: only team 432 of tur.1.
/// </summary>
internal sealed class StubFootballDataProvider : IFootballDataProvider
{
    public Task<MatchDetail?> GetMatchDetailAsync(string leagueCode, string matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(leagueCode != "tur.1" || matchId != "77" ? null : new MatchDetail(
            new Match("77", leagueCode, new DateTimeOffset(2026, 10, 9, 17, 0, 0, TimeSpan.Zero), MatchStatus.Live, "20'",
                new Team("432", "Galatasaray", "Galatasaray", null),
                new Team("436", "Fenerbahce", "Fenerbahce", null),
                new Score(1, 0)),
            [new MatchEvent(MatchEventType.PenaltyGoal, "12'", TeamSide.Home, "Icardi", null)],
            [new MatchStat(MatchStatType.Possession, 61.5, 38.5)],
            new MatchLineups(Lineup("Icardi", "#fdb912"), Lineup("Dzeko", null))));

    private static TeamLineup Lineup(string striker, string? shirtColor) => new("4-4-2", shirtColor,
        [[Player("1", "Keeper", PlayerPosition.Goalkeeper, goals: 0)], [Player("9", striker, PlayerPosition.Forward, goals: 1)]],
        [Player("20", "Unused", position: null, goals: 0)]);

    private static LineupPlayer Player(string id, string name, PlayerPosition? position, int goals) =>
        new(id, name, name, Jersey: id, position, CameOnAt: null, WentOffAt: position == PlayerPosition.Forward ? "80'" : null,
            new PlayerMatchStats(goals, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

    public Task<Squad?> GetSquadAsync(string leagueCode, string teamId, CancellationToken cancellationToken = default) =>
        Task.FromResult(leagueCode != "tur.1" || teamId != "432" ? null : new Squad(leagueCode, teamId, "Galatasaray",
        [
            new SquadPlayer("1", "Keeper", "1", PlayerPosition.Goalkeeper, 30, "Türkiye"),
            new SquadPlayer("9", "Icardi", "9", PlayerPosition.Forward, 33, "Argentina"),
            new SquadPlayer("50", "Youngster", null, null, null, null),
        ]));

    public Task<LeagueScoreboard> GetScoreboardAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LeagueScoreboard(new League(leagueCode, leagueCode), date, leagueCode != "tur.1" ? [] :
        [
            new Match($"stub-{date:yyyyMMdd}", leagueCode, new DateTimeOffset(date, new TimeOnly(17, 0), TimeSpan.Zero),
                MatchStatus.Finished, "FT",
                new Team("432", "Galatasaray", "Galatasaray", null),
                new Team("436", "Fenerbahce", "Fenerbahce", null),
                new Score(2, 1)),
        ]));
}
