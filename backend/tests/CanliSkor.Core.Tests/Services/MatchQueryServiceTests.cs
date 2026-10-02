using CanliSkor.Core.Domain;
using CanliSkor.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Services;

public class MatchQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 18, 0, 0, TimeSpan.Zero); // 21:00 Istanbul
    private static readonly DateOnly Today = new(2026, 10, 9);

    private readonly FakeMatchStore _store = new();
    private readonly FakeFootballDataProvider _provider = new();

    private MatchQueryService CreateService()
    {
        var time = new FakeTimeProvider(Now);
        var loader = new OnDemandScoreboardLoader(_provider, _store, new OnDemandFetchGate(), time, NullLogger<OnDemandScoreboardLoader>.Instance);
        return new(_store, loader, TestOptions.Leagues("tur.1", "eng.1", "esp.1"), time);
    }

    private Task Store(LeagueScoreboard scoreboard) => _store.SetAsync(new ScoreboardSnapshot(scoreboard, Now));

    [Fact]
    public async Task Today_returns_leagues_in_configured_order_without_empty_ones()
    {
        await Store(Scoreboard("esp.1", Today, Match(MatchStatus.Scheduled, Now.AddHours(1), "esp.1")));
        await Store(Scoreboard("eng.1", Today)); // no matches today
        await Store(Scoreboard("tur.1", Today, Match(MatchStatus.Live, Now.AddMinutes(-20))));

        var result = await CreateService().GetDayAsync();

        Assert.Equal(["tur.1", "esp.1"], result.Select(s => s.Scoreboard.League.Code));
    }

    [Fact]
    public async Task Live_returns_only_in_play_matches_including_yesterday()
    {
        var live = Match(MatchStatus.Live, Now.AddMinutes(-20));
        var halfTime = Match(MatchStatus.HalfTime, Now.AddMinutes(-50), "eng.1");
        var lateGame = Match(MatchStatus.Live, Now.AddHours(-3), "esp.1");
        await Store(Scoreboard("tur.1", Today, live, Match(MatchStatus.Finished, Now.AddHours(-3))));
        await Store(Scoreboard("eng.1", Today, halfTime));
        await Store(Scoreboard("esp.1", Today.AddDays(-1), lateGame));
        await Store(Scoreboard("esp.1", Today, Match(MatchStatus.Scheduled, Now.AddHours(1), "esp.1")));

        var result = await CreateService().GetLiveAsync();

        Assert.Equal([lateGame, live, halfTime], result.SelectMany(s => s.Scoreboard.Matches));
    }

    [Fact]
    public async Task Returns_empty_before_first_poll()
    {
        Assert.Empty(await CreateService().GetDayAsync());
        Assert.Empty(_provider.Requests); // today belongs to the poller, never loaded on demand
    }

    [Fact]
    public async Task Other_days_are_loaded_on_demand()
    {
        var lastSunday = Today.AddDays(-5);
        _provider.Returns(Scoreboard("tur.1", lastSunday, Match(MatchStatus.Finished, Now.AddDays(-5))));
        _provider.Returns(Scoreboard("eng.1", lastSunday));
        // esp.1 fails: the other leagues are still returned.

        var result = await CreateService().GetDayAsync(lastSunday);

        Assert.Equal(["tur.1"], result.Select(s => s.Scoreboard.League.Code));
    }

    [Theory]
    [InlineData(-7, true)]
    [InlineData(7, true)]
    [InlineData(-8, false)]
    [InlineData(8, false)]
    public void Browsing_is_limited_to_a_week_around_today(int daysFromToday, bool browsable) =>
        Assert.Equal(browsable, CreateService().IsBrowsable(Today.AddDays(daysFromToday)));

    [Fact]
    public async Task Dates_out_of_range_are_rejected() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateService().GetDayAsync(Today.AddDays(30)));
}
