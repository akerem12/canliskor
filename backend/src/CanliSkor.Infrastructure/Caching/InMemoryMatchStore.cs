using System.Globalization;
using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace CanliSkor.Infrastructure.Caching;

/// <summary>
/// IMemoryCache-backed store. Snapshots are immutable records, so readers can safely share them
/// while the poller replaces entries — no locking needed.
/// </summary>
internal sealed class InMemoryMatchStore(IMemoryCache cache) : IMatchStore
{
    // Old days fall out on their own; we only ever need today and yesterday.
    private static readonly TimeSpan Retention = TimeSpan.FromDays(2);

    public Task<ScoreboardSnapshot?> GetAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(cache.Get<ScoreboardSnapshot>(Key(leagueCode, date)));

    public Task SetAsync(ScoreboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cache.Set(Key(snapshot.Scoreboard.League.Code, snapshot.Scoreboard.Date), snapshot, Retention);
        return Task.CompletedTask;
    }

    private static string Key(string leagueCode, DateOnly date) =>
        $"scoreboard:{leagueCode}:{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
}
