using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Services;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Services;

public class LeagueInfoServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 18, 0, 0, TimeSpan.Zero);
    private static readonly Team Besiktas = new("1895", "Besiktas", "Besiktas", null);
    private static readonly LeagueStandings Table = new("tur.1", "Turkish Super Lig",
        [new StandingsGroup("2026/2027", [new StandingsRow(1, Besiktas, 6, 4, 0, 2, 14, 7, 7, 12, null, null)])]);
    private static readonly TeamProfile Profile = new("tur.1", Besiktas, false, "3rd in Turkish Super Lig", "Vodafone Park", "Istanbul", [], []);

    private readonly FakeFootballDataProvider _provider = new();
    private readonly FakeMatchStore _store = new();
    private readonly FakeTimeProvider _time = new(Now);

    private LeagueInfoService CreateService() => new(
        _provider,
        _store,
        new OnDemandFetchGate(),
        TestOptions.Leagues("tur.1", "eng.1"),
        _time,
        NullLogger<LeagueInfoService>.Instance);

    [Fact]
    public async Task Loads_standings_once_and_serves_them_from_the_cache()
    {
        _provider.Returns(Table);
        var service = CreateService();

        var first = await service.GetStandingsAsync("tur.1");
        _time.Advance(LeagueInfoService.StandingsRefreshAfter - TimeSpan.FromSeconds(1));
        var second = await service.GetStandingsAsync("tur.1");

        Assert.Same(Table, first?.Value);
        Assert.Equal(Now, first?.FetchedAtUtc);
        Assert.Same(first, second);
        Assert.Equal(["standings:tur.1"], _provider.InfoRequests);
    }

    [Fact]
    public async Task Standings_are_refetched_once_they_are_old()
    {
        _provider.Returns(Table);
        var service = CreateService();
        await service.GetStandingsAsync("tur.1");

        _time.Advance(LeagueInfoService.StandingsRefreshAfter);
        var refreshed = await service.GetStandingsAsync("tur.1");

        Assert.Equal(2, _provider.InfoRequests.Count);
        Assert.Equal(_time.GetUtcNow(), refreshed?.FetchedAtUtc);
    }

    [Fact]
    public async Task Loads_a_team_and_caches_it_separately_from_the_standings()
    {
        _provider.Returns(Table);
        _provider.Returns(Profile);
        var service = CreateService();

        await service.GetStandingsAsync("tur.1");
        var team = await service.GetTeamAsync("tur.1", "1895");
        await service.GetTeamAsync("tur.1", "1895");

        Assert.Same(Profile, team?.Value);
        Assert.Equal(["standings:tur.1", "team:tur.1:1895"], _provider.InfoRequests);
    }

    [Fact]
    public async Task Unknown_team_and_league_without_data_are_null()
    {
        var service = CreateService();

        Assert.Null(await service.GetTeamAsync("tur.1", "404"));
        Assert.Null(await service.GetStandingsAsync("eng.1"));
    }

    [Fact]
    public async Task League_that_is_not_followed_never_reaches_the_provider()
    {
        var service = CreateService();

        Assert.Null(await service.GetStandingsAsync("ger.1"));
        Assert.Null(await service.GetTeamAsync("ger.1", "1895"));
        Assert.Empty(_provider.InfoRequests);
    }

    [Fact]
    public async Task Old_copy_is_served_when_the_provider_fails()
    {
        _provider.Returns(Table);
        var service = CreateService();
        var first = await service.GetStandingsAsync("tur.1");

        _time.Advance(LeagueInfoService.StandingsRefreshAfter);
        _provider.FailInfo = true;

        Assert.Same(first, await service.GetStandingsAsync("tur.1"));
    }

    [Fact]
    public async Task Provider_failure_with_nothing_cached_is_thrown()
    {
        _provider.FailInfo = true;

        await Assert.ThrowsAsync<FootballDataProviderException>(() => CreateService().GetTeamAsync("tur.1", "1895"));
    }

    [Fact]
    public async Task Leagues_take_their_names_from_todays_scoreboards()
    {
        var today = IstanbulTime.DateOf(Now);
        await _store.SetAsync(new ScoreboardSnapshot(new LeagueScoreboard(new League("tur.1", "Turkish Super Lig"), today, []), Now));

        var leagues = await CreateService().GetLeaguesAsync();

        // eng.1 hasn't been polled yet: its code stands in.
        Assert.Equal([new League("tur.1", "Turkish Super Lig"), new League("eng.1", "eng.1")], leagues);
    }
}
