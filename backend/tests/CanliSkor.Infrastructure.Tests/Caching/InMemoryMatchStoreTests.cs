using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace CanliSkor.Infrastructure.Tests.Caching;

public class InMemoryMatchStoreTests
{
    private readonly InMemoryMatchStore _store = new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task Stores_snapshots_per_league_and_date()
    {
        var date = new DateOnly(2026, 10, 9);
        var tur = Snapshot("tur.1", date);
        var eng = Snapshot("eng.1", date);

        await _store.SetAsync(tur);
        await _store.SetAsync(eng);

        Assert.Same(tur, await _store.GetAsync("tur.1", date));
        Assert.Same(eng, await _store.GetAsync("eng.1", date));
        Assert.Null(await _store.GetAsync("tur.1", date.AddDays(1)));
    }

    [Fact]
    public async Task Newer_snapshot_replaces_older()
    {
        var date = new DateOnly(2026, 10, 9);
        var newer = Snapshot("tur.1", date);

        await _store.SetAsync(Snapshot("tur.1", date));
        await _store.SetAsync(newer);

        Assert.Same(newer, await _store.GetAsync("tur.1", date));
    }

    private static ScoreboardSnapshot Snapshot(string leagueCode, DateOnly date) =>
        new(new LeagueScoreboard(new League(leagueCode, leagueCode), date, []), DateTimeOffset.UtcNow);
}
