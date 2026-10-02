using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Polling;
using Microsoft.Extensions.Logging;

namespace CanliSkor.Core.Services;

/// <summary>
/// Loads scoreboards for dates the poller doesn't cover (browsing other days), caching them in the same store.
/// The only path where a client request can reach the provider, so it is deliberately conservative:
/// finished days are fetched once, fixture lists at most every <see cref="RefreshAfter"/>,
/// and only one fetch runs at a time across the whole app (<see cref="OnDemandFetchGate"/>).
/// </summary>
public sealed partial class OnDemandScoreboardLoader(
    IFootballDataProvider provider,
    IMatchStore store,
    OnDemandFetchGate gate,
    TimeProvider timeProvider,
    ILogger<OnDemandScoreboardLoader> logger)
{
    /// <summary>Fixtures (kickoff times, postponements) can still change; results of finished days can't.</summary>
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(30);

    /// <returns>The cached or freshly loaded snapshot; the stale one if loading fails; null if there is nothing.</returns>
    public async Task<ScoreboardSnapshot?> GetAsync(string leagueCode, DateOnly date, DateOnly today, CancellationToken cancellationToken = default)
    {
        var cached = await store.GetAsync(leagueCode, date, cancellationToken);
        if (cached is not null && IsFresh(cached, today))
        {
            return cached;
        }

        using (await gate.AcquireAsync(cancellationToken))
        {
            // Another request may have loaded it while we waited.
            cached = await store.GetAsync(leagueCode, date, cancellationToken);
            if (cached is not null && IsFresh(cached, today))
            {
                return cached;
            }

            try
            {
                var scoreboard = await provider.GetScoreboardAsync(leagueCode, date, cancellationToken);
                var snapshot = new ScoreboardSnapshot(scoreboard, timeProvider.GetUtcNow());
                await store.SetAsync(snapshot, cancellationToken);
                return snapshot;
            }
            catch (FootballDataProviderException ex)
            {
                LogLoadFailed(ex, leagueCode, date);
                return cached;
            }
        }
    }

    private bool IsFresh(ScoreboardSnapshot snapshot, DateOnly today)
    {
        var matches = snapshot.Scoreboard.Matches;
        var final = matches.All(m => m.Status is MatchStatus.Finished or MatchStatus.Postponed or MatchStatus.Cancelled);

        // A past day whose matches are all over never changes again (also true for a past day without matches).
        // Anything else may still change — but a live game on yesterday's board is kept fresh by the poller.
        if (final && snapshot.Scoreboard.Date < today)
        {
            return true;
        }

        var now = timeProvider.GetUtcNow();
        return now - snapshot.FetchedAtUtc < RefreshAfter || matches.Any(m => PollingIntervalCalculator.IsActive(m, now));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "On-demand load of {LeagueCode} for {Date} failed")]
    private partial void LogLoadFailed(Exception exception, string leagueCode, DateOnly date);
}

/// <summary>
/// App-wide limit of one on-demand provider call at a time. Browsing traffic waits a little instead of
/// bursting requests at an unofficial API. Singleton; the loader that uses it is scoped.
/// </summary>
public sealed class OnDemandFetchGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
