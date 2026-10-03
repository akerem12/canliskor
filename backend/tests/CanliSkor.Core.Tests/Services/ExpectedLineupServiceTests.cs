using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CanliSkor.Core.Tests.Services;

public class ExpectedLineupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Team Home = new("1", "Home FC", "Home", null);
    private static readonly Team Away = new("2", "Away FC", "Away", null);
    private static readonly Team Other = new("3", "Other FC", "Other", null);

    private readonly FakeFootballDataProvider _provider = new();
    private readonly FakeMatchStore _store = new();
    private readonly FakeTimeProvider _time = new(Now);

    private ExpectedLineupService CreateService()
    {
        var gate = new OnDemandFetchGate();
        var followed = TestOptions.Leagues("tur.1", "uefa.champions");
        return new ExpectedLineupService(
            new MatchDetailService(_provider, _store, gate, followed, new StaticOptionsMonitor<PollingOptions>(TestOptions.Polling()), _time, NullLogger<MatchDetailService>.Instance),
            new LeagueInfoService(_provider, _store, gate, followed, _time, NullLogger<LeagueInfoService>.Instance),
            NullLogger<ExpectedLineupService>.Instance);
    }

    private static Match Game(string id, Team home, Team away, MatchStatus status, int daysAgo, string league = "tur.1") =>
        new(id, league, Now.AddDays(-daysAgo), status, null, home, away, status == MatchStatus.Finished ? new Score(1, 0) : null);

    private static TeamLineup Lineup(string formation, params string[] starters) => new(
        formation,
        "#aa0031",
        [starters.Select(name => new LineupPlayer(name, name, name, "9", PlayerPosition.Forward, null, "70'",
            new PlayerMatchStats(2, 1, 5, 3, 0, 0, 0, 1, 0, 0, 0, 0), MinutesPlayed: 70)).ToList()],
        [new LineupPlayer("Sub", "Sub", "Sub", "20", PlayerPosition.Forward, "70'", null, new PlayerMatchStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))]);

    /// <summary>The match to be played, and what each team has played before it (newest first).</summary>
    private Match Upcoming(Match[] homeHistory, Match[] awayHistory, MatchLineups? announced = null)
    {
        var upcoming = Game("100", Home, Away, MatchStatus.Scheduled, daysAgo: 0);
        _provider.Returns(new MatchDetail(upcoming, [], [], announced));
        _provider.Returns(new TeamProfile("tur.1", Home, false, null, null, null, homeHistory, [upcoming], []));
        _provider.Returns(new TeamProfile("tur.1", Away, false, null, null, null, awayHistory, [upcoming], []));
        return upcoming;
    }

    private void Played(Match match, TeamLineup? home, TeamLineup? away) =>
        _provider.Returns(new MatchDetail(match, [], [], home is not null && away is not null ? new MatchLineups(home, away) : null));

    [Fact]
    public async Task Each_team_is_shown_as_it_started_its_last_match()
    {
        var homeLast = Game("90", Home, Other, MatchStatus.Finished, daysAgo: 6);
        var awayLast = Game("91", Other, Away, MatchStatus.Finished, daysAgo: 5);
        Upcoming([homeLast], [awayLast]);
        Played(homeLast, Lineup("4-2-3-1", "Home striker"), Lineup("4-4-2", "Someone"));
        Played(awayLast, Lineup("4-4-2", "Someone"), Lineup("3-5-2", "Away striker"));

        var expected = await CreateService().GetAsync("tur.1", "100");

        Assert.Equal("4-2-3-1", expected!.Home!.Lineup.Formation);
        Assert.Equal("Home striker", expected.Home.Lineup.Rows[0][0].Name);
        Assert.Equal(homeLast, expected.Home.BasedOn);
        // The away team played away in its last match: its own side of that match is taken.
        Assert.Equal("3-5-2", expected.Away!.Lineup.Formation);
        Assert.Equal("Away striker", expected.Away.Lineup.Rows[0][0].Name);
        Assert.Equal(awayLast, expected.Away.BasedOn);
    }

    [Fact]
    public async Task What_happened_in_that_match_is_left_behind()
    {
        var last = Game("90", Home, Other, MatchStatus.Finished, daysAgo: 6);
        Upcoming([last], []);
        Played(last, Lineup("4-4-2", "Starter"), Lineup("4-4-2", "Someone"));

        var lineup = (await CreateService().GetAsync("tur.1", "100"))!.Home!.Lineup;
        var starter = lineup.Rows[0][0];

        Assert.Empty(lineup.Bench);
        Assert.Equal(new PlayerMatchStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), starter.Stats);
        Assert.Null(starter.WentOffAt);
        Assert.Null(starter.MinutesPlayed);
        Assert.Equal("9", starter.Jersey);
        Assert.Equal(PlayerPosition.Forward, starter.Position);
    }

    [Fact]
    public async Task A_match_without_a_line_up_or_in_a_competition_not_followed_is_skipped_for_the_one_before()
    {
        var cup = Game("95", Home, Other, MatchStatus.Finished, daysAgo: 2, league: "tur.cup");
        var friendly = Game("94", Home, Other, MatchStatus.Finished, daysAgo: 4);
        var league = Game("93", Home, Other, MatchStatus.Finished, daysAgo: 8);
        Upcoming([cup, friendly, league], []);
        Played(friendly, null, null);
        Played(league, Lineup("4-3-3", "Regular"), Lineup("4-4-2", "Someone"));

        var expected = await CreateService().GetAsync("tur.1", "100");

        Assert.Equal(league, expected!.Home!.BasedOn);
        Assert.Null(expected.Away);
    }

    [Fact]
    public async Task Only_the_latest_few_matches_are_tried()
    {
        var history = Enumerable.Range(1, ExpectedLineupService.MatchesToTry + 1)
            .Select(i => Game($"8{i}", Home, Other, MatchStatus.Finished, daysAgo: i))
            .ToArray();
        Upcoming(history, []);
        foreach (var match in history.Take(ExpectedLineupService.MatchesToTry))
        {
            Played(match, null, null);
        }
        Played(history[^1], Lineup("4-4-2", "Long ago"), Lineup("4-4-2", "Someone"));

        Assert.Null((await CreateService().GetAsync("tur.1", "100"))!.Home);
    }

    [Fact]
    public async Task Nothing_is_guessed_once_the_real_line_ups_are_announced()
    {
        var last = Game("90", Home, Other, MatchStatus.Finished, daysAgo: 6);
        Upcoming([last], [], announced: new MatchLineups(Lineup("4-4-2", "Real"), Lineup("4-4-2", "Real")));
        Played(last, Lineup("4-4-2", "Starter"), Lineup("4-4-2", "Someone"));

        var expected = await CreateService().GetAsync("tur.1", "100");

        Assert.Equal(new ExpectedLineups(null, null), expected);
        Assert.Empty(_provider.InfoRequests);
    }

    [Fact]
    public async Task One_teams_history_failing_leaves_both_sides_empty_but_does_not_throw()
    {
        Upcoming([], []);
        _provider.FailInfo = true;

        Assert.Equal(new ExpectedLineups(null, null), await CreateService().GetAsync("tur.1", "100"));
    }

    [Fact]
    public async Task Unknown_match_or_league_that_is_not_followed_is_null()
    {
        Assert.Null(await CreateService().GetAsync("tur.1", "404"));
        Assert.Null(await CreateService().GetAsync("ger.1", "100"));
    }
}
