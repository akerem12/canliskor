using CanliSkor.Core.Domain;
using CanliSkor.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Services;

public class OnDemandScoreboardLoaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 18, 0, 0, TimeSpan.Zero); // 21:00 Istanbul
    private static readonly DateOnly Today = new(2026, 10, 9);
    private static readonly DateOnly LastSunday = new(2026, 10, 4);
    private static readonly DateOnly NextSaturday = new(2026, 10, 10);

    private readonly FakeFootballDataProvider _provider = new();
    private readonly FakeMatchStore _store = new();
    private readonly FakeTimeProvider _time = new(Now);

    private OnDemandScoreboardLoader CreateLoader() =>
        new(_provider, _store, new OnDemandFetchGate(), _time, NullLogger<OnDemandScoreboardLoader>.Instance);

    [Fact]
    public async Task Loads_an_uncached_day_from_the_provider_and_caches_it()
    {
        _provider.Returns(Scoreboard("tur.1", LastSunday, Match(MatchStatus.Finished, Now.AddDays(-5))));

        var snapshot = await CreateLoader().GetAsync("tur.1", LastSunday, Today);

        Assert.NotNull(snapshot);
        Assert.Same(snapshot, await _store.GetAsync("tur.1", LastSunday));
    }

    [Fact]
    public async Task Finished_past_day_is_never_fetched_again()
    {
        await _store.SetAsync(new ScoreboardSnapshot(Scoreboard("tur.1", LastSunday, Match(MatchStatus.Finished, Now.AddDays(-5))), Now.AddDays(-3)));

        await CreateLoader().GetAsync("tur.1", LastSunday, Today);

        Assert.Empty(_provider.Requests);
    }

    [Fact]
    public async Task Past_day_without_matches_is_also_final()
    {
        await _store.SetAsync(new ScoreboardSnapshot(Scoreboard("tur.1", LastSunday), Now.AddDays(-3)));

        await CreateLoader().GetAsync("tur.1", LastSunday, Today);

        Assert.Empty(_provider.Requests);
    }

    [Fact]
    public async Task Fixtures_are_reused_until_the_refresh_interval_has_passed()
    {
        _provider.Returns(Scoreboard("tur.1", NextSaturday, Match(MatchStatus.Scheduled, Now.AddDays(1))));
        var loader = CreateLoader();

        await loader.GetAsync("tur.1", NextSaturday, Today);
        _time.Advance(OnDemandScoreboardLoader.RefreshAfter - TimeSpan.FromMinutes(1));
        await loader.GetAsync("tur.1", NextSaturday, Today);
        Assert.Single(_provider.Requests);

        _time.Advance(TimeSpan.FromMinutes(2));
        await loader.GetAsync("tur.1", NextSaturday, Today);
        Assert.Equal(2, _provider.Requests.Count);
    }

    [Fact]
    public async Task Provider_failure_falls_back_to_the_stale_snapshot()
    {
        var stale = new ScoreboardSnapshot(Scoreboard("tur.1", NextSaturday, Match(MatchStatus.Scheduled, Now.AddDays(1))), Now.AddHours(-2));
        await _store.SetAsync(stale);
        // _provider has no data configured, so the fetch fails.

        Assert.Same(stale, await CreateLoader().GetAsync("tur.1", NextSaturday, Today));
    }

    [Fact]
    public async Task Provider_failure_without_cache_returns_null() =>
        Assert.Null(await CreateLoader().GetAsync("tur.1", NextSaturday, Today));
}
