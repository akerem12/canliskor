using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Polling;
using CanliSkor.Core.Ratings;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Services;

/// <summary>
/// Match details (events, statistics), loaded on request and cached in the store. How long a cached detail is
/// good for depends on the match: one poll interval while it is live, so any number of viewers cost one provider
/// call per interval; longer before kickoff and after full time. A cached detail is also stale as soon as the
/// poller's scoreboard shows a different score or status for the match, so a pushed goal is followed by a detail
/// that has the scorer. Player ratings are computed once, when a detail is loaded. Shares <see cref="OnDemandFetchGate"/> with
/// browsing, so all request-driven provider calls together still run one at a time.
/// </summary>
public sealed partial class MatchDetailService(
    IFootballDataProvider provider,
    IMatchStore store,
    OnDemandFetchGate gate,
    LineupRater rater,
    IOptionsMonitor<FootballOptions> footballOptions,
    IOptionsMonitor<PollingOptions> pollingOptions,
    TimeProvider timeProvider,
    ILogger<MatchDetailService> logger)
{
    /// <summary>Kickoff times and line-ups can still change, but nothing happens on the pitch.</summary>
    public static readonly TimeSpan ScheduledRefreshAfter = TimeSpan.FromMinutes(30);

    /// <summary>Results are settled; this only picks up late corrections (e.g. a goal credited to another player).</summary>
    public static readonly TimeSpan FinalRefreshAfter = TimeSpan.FromHours(6);

    /// <summary>
    /// A detail younger than this is used even if the scoreboard is ahead of it: ESPN's summary can lag its
    /// scoreboard by a few seconds, and refetching it on every request until it catches up would gain nothing.
    /// </summary>
    public static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(5);

    /// <returns>
    /// The cached or freshly loaded detail; the stale one if loading fails; null if the league isn't followed
    /// or the provider doesn't know the match.
    /// </returns>
    /// <exception cref="FootballDataProviderException">Loading failed and nothing is cached.</exception>
    public async Task<MatchDetailSnapshot?> GetAsync(string leagueCode, string matchId, CancellationToken cancellationToken = default)
    {
        // Only followed leagues: requests can't make us fetch anything else from the provider.
        if (!footballOptions.CurrentValue.Leagues.Contains(leagueCode))
        {
            return null;
        }

        var cached = await store.GetDetailAsync(leagueCode, matchId, cancellationToken);
        if (cached is not null && await IsFreshAsync(cached, cancellationToken))
        {
            return cached;
        }

        using (await gate.AcquireAsync(cancellationToken))
        {
            // Another request may have loaded it while we waited (typical for a live match with many viewers).
            cached = await store.GetDetailAsync(leagueCode, matchId, cancellationToken);
            if (cached is not null && await IsFreshAsync(cached, cancellationToken))
            {
                return cached;
            }

            try
            {
                var detail = await provider.GetMatchDetailAsync(leagueCode, matchId, cancellationToken);
                if (detail is null)
                {
                    return null;
                }

                var snapshot = new MatchDetailSnapshot(rater.Rate(detail), timeProvider.GetUtcNow());
                await store.SetDetailAsync(snapshot, cancellationToken);
                return snapshot;
            }
            catch (FootballDataProviderException ex) when (cached is not null)
            {
                LogLoadFailed(ex, leagueCode, matchId);
                return cached;
            }
        }
    }

    private async Task<bool> IsFreshAsync(MatchDetailSnapshot snapshot, CancellationToken cancellationToken)
    {
        var match = snapshot.Detail.Match;
        var now = timeProvider.GetUtcNow();
        var age = now - snapshot.FetchedAtUtc;
        if (age >= MinRefreshInterval && await ScoreboardIsAheadAsync(match, snapshot.FetchedAtUtc, cancellationToken))
        {
            return false;
        }

        var maxAge = match.Status switch
        {
            MatchStatus.Finished or MatchStatus.Postponed or MatchStatus.Cancelled => FinalRefreshAfter,
            // Also covers a match past kickoff that isn't flagged live yet.
            _ when PollingIntervalCalculator.IsActive(match, now) => pollingOptions.CurrentValue.LiveInterval,
            _ => ScheduledRefreshAfter,
        };

        return age < maxAge;
    }

    /// <summary>True if a scoreboard fetched after the detail shows a different score or status.</summary>
    private async Task<bool> ScoreboardIsAheadAsync(Match match, DateTimeOffset detailFetchedAtUtc, CancellationToken cancellationToken)
    {
        var scoreboard = await store.GetAsync(match.LeagueCode, IstanbulTime.DateOf(match.KickoffUtc), cancellationToken);
        var current = scoreboard?.Scoreboard.Matches.FirstOrDefault(m => m.Id == match.Id);

        return current is not null
            && scoreboard!.FetchedAtUtc > detailFetchedAtUtc
            && (current.Score != match.Score || current.Status != match.Status);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading match {MatchId} ({LeagueCode}) failed; serving the cached copy")]
    private partial void LogLoadFailed(Exception exception, string leagueCode, string matchId);
}
