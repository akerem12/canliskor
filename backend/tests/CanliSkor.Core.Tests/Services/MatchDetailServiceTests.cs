using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Services;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Services;

public class MatchDetailServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 18, 0, 0, TimeSpan.Zero);

    private readonly FakeFootballDataProvider _provider = new();
    private readonly FakeMatchStore _store = new();
    private readonly FakeTimeProvider _time = new(Now);

    private MatchDetailService CreateService() => new(
        _provider,
        _store,
        new OnDemandFetchGate(),
        TestOptions.Leagues("tur.1"),
        new StaticOptionsMonitor<PollingOptions>(TestOptions.Polling()),
        _time,
        NullLogger<MatchDetailService>.Instance);

    private MatchDetail Detail(MatchStatus status, DateTimeOffset kickoffUtc)
    {
        var detail = new MatchDetail(Match(status, kickoffUtc), [], []);
        _provider.Returns(detail);
        return detail;
    }

    [Fact]
    public async Task Loads_a_match_from_the_provider_and_caches_it()
    {
        var detail = Detail(MatchStatus.Finished, Now.AddHours(-3));

        var snapshot = await CreateService().GetAsync("tur.1", detail.Match.Id);

        Assert.Same(detail, snapshot?.Detail);
        Assert.Same(snapshot, await _store.GetDetailAsync("tur.1", detail.Match.Id));
    }

    [Fact]
    public async Task Unknown_match_is_null()
    {
        Assert.Null(await CreateService().GetAsync("tur.1", "404"));
    }

    [Fact]
    public async Task League_that_is_not_followed_never_reaches_the_provider()
    {
        Assert.Null(await CreateService().GetAsync("ger.1", "1"));
        Assert.Empty(_provider.DetailRequests);
    }

    [Fact]
    public async Task Live_match_is_shared_for_one_poll_interval()
    {
        var detail = Detail(MatchStatus.Live, Now.AddMinutes(-30));
        var service = CreateService();

        await service.GetAsync("tur.1", detail.Match.Id);
        _time.Advance(TimeSpan.FromSeconds(29));
        await service.GetAsync("tur.1", detail.Match.Id);
        Assert.Single(_provider.DetailRequests);

        _time.Advance(TimeSpan.FromSeconds(1));
        await service.GetAsync("tur.1", detail.Match.Id);
        Assert.Equal(2, _provider.DetailRequests.Count);
    }

    [Fact]
    public async Task Scheduled_match_past_its_kickoff_is_treated_as_live()
    {
        var detail = Detail(MatchStatus.Scheduled, Now.AddMinutes(-1));
        var service = CreateService();

        await service.GetAsync("tur.1", detail.Match.Id);
        _time.Advance(TimeSpan.FromSeconds(30));
        await service.GetAsync("tur.1", detail.Match.Id);

        Assert.Equal(2, _provider.DetailRequests.Count);
    }

    [Fact]
    public async Task Goal_seen_by_the_poller_makes_the_cached_detail_stale()
    {
        var detail = Detail(MatchStatus.Live, Now.AddMinutes(-30));
        var service = CreateService();
        await service.GetAsync("tur.1", detail.Match.Id);

        // Well within the live interval, but the scoreboard has moved on.
        _time.Advance(MatchDetailService.MinRefreshInterval);
        await PollerSees(detail.Match with { Score = new Score(1, 0) });
        await service.GetAsync("tur.1", detail.Match.Id);

        Assert.Equal(2, _provider.DetailRequests.Count);
    }

    [Fact]
    public async Task Detail_that_lags_the_scoreboard_is_not_refetched_on_every_request()
    {
        var detail = Detail(MatchStatus.Live, Now.AddMinutes(-30));
        var service = CreateService();
        await service.GetAsync("tur.1", detail.Match.Id);

        _time.Advance(TimeSpan.FromSeconds(1));
        await PollerSees(detail.Match with { Score = new Score(1, 0) });
        await service.GetAsync("tur.1", detail.Match.Id);

        Assert.Single(_provider.DetailRequests);
    }

    private Task PollerSees(Match match) =>
        _store.SetAsync(new ScoreboardSnapshot(Scoreboard(match.LeagueCode, IstanbulTime.DateOf(match.KickoffUtc), match), _time.GetUtcNow()));

    [Fact]
    public async Task Finished_match_is_reused_for_hours()
    {
        var detail = Detail(MatchStatus.Finished, Now.AddHours(-3));
        var service = CreateService();

        await service.GetAsync("tur.1", detail.Match.Id);
        _time.Advance(MatchDetailService.FinalRefreshAfter - TimeSpan.FromMinutes(1));
        await service.GetAsync("tur.1", detail.Match.Id);

        Assert.Single(_provider.DetailRequests);
    }

    [Fact]
    public async Task Serves_the_stale_copy_when_the_provider_fails()
    {
        var detail = Detail(MatchStatus.Live, Now.AddMinutes(-30));
        var service = CreateService();
        var first = await service.GetAsync("tur.1", detail.Match.Id);

        _time.Advance(TimeSpan.FromMinutes(1));
        _provider.FailDetails = true;

        Assert.Same(first, await service.GetAsync("tur.1", detail.Match.Id));
    }

    [Fact]
    public async Task Provider_failure_without_a_cached_copy_propagates()
    {
        _provider.FailDetails = true;

        await Assert.ThrowsAsync<FootballDataProviderException>(() => CreateService().GetAsync("tur.1", "1"));
    }

    [Fact]
    public async Task Refresh_loads_a_new_detail_even_if_the_cached_one_is_still_good()
    {
        var detail = Detail(MatchStatus.Finished, Now.AddHours(-3));
        var service = CreateService();
        var first = await service.GetAsync("tur.1", detail.Match.Id);

        _time.Advance(MatchDetailService.MinRefreshInterval);
        var refreshed = await service.RefreshAsync("tur.1", detail.Match.Id);

        Assert.NotSame(first, refreshed);
        Assert.Equal(2, _provider.DetailRequests.Count);
    }

    [Fact]
    public async Task Refresh_right_after_a_load_reuses_it()
    {
        var detail = Detail(MatchStatus.Live, Now.AddMinutes(-30));
        var service = CreateService();
        var first = await service.GetAsync("tur.1", detail.Match.Id);

        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Same(first, await service.RefreshAsync("tur.1", detail.Match.Id));
        Assert.Single(_provider.DetailRequests);
    }
}
