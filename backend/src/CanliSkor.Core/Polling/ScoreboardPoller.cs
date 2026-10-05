using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Polling;

/// <summary>
/// One polling round: fetch every followed league that is due, update the store, push what changed, refresh the
/// details of watched live matches, and decide when to poll next. A league is due every few seconds while it has
/// a match in play or about to kick off, and rarely otherwise, so quiet leagues cost next to nothing.
/// Kept separate from the hosted service so it can be unit-tested without timers or a host.
/// </summary>
public sealed partial class ScoreboardPoller(
    IFootballDataProvider provider,
    IMatchStore store,
    IMatchUpdatePublisher publisher,
    LiveDetailRefresher detailRefresher,
    PollSchedule schedule,
    IOptionsMonitor<FootballOptions> footballOptions,
    IOptionsMonitor<PollingOptions> pollingOptions,
    TimeProvider timeProvider,
    ILogger<ScoreboardPoller> logger)
{
    /// <summary>However late a round ends, the next one waits at least this long.</summary>
    public static readonly TimeSpan MinDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// A league due this soon is polled in the current round already, so leagues that are live at the same
    /// time are fetched together instead of each waking the poller for itself.
    /// </summary>
    public static readonly TimeSpan DueSlack = TimeSpan.FromSeconds(2);

    /// <returns>How long to wait before the next round.</returns>
    public async Task<TimeSpan> PollAsync(CancellationToken cancellationToken)
    {
        var options = pollingOptions.CurrentValue;
        var today = IstanbulTime.Today(timeProvider);
        var knownMatches = new List<Match>();
        DateTimeOffset? nextDue = null;

        schedule.ForgetBefore(today.AddDays(-1));

        // Sequential on purpose: a handful of requests per round, and it is gentler on an unofficial API.
        foreach (var leagueCode in footballOptions.CurrentValue.Leagues)
        {
            foreach (var date in await GetDatesToPollAsync(leagueCode, today, cancellationToken))
            {
                var now = timeProvider.GetUtcNow();
                if (!schedule.IsDue(leagueCode, date, now + DueSlack))
                {
                    // Nothing in play here: the last snapshot is good until the league's own next poll.
                    var current = await store.GetAsync(leagueCode, date, cancellationToken);
                    knownMatches.AddRange(current?.Scoreboard.Matches ?? []);
                }
                else
                {
                    try
                    {
                        var scoreboard = await provider.GetScoreboardAsync(leagueCode, date, cancellationToken);
                        var previous = await store.GetAsync(leagueCode, date, cancellationToken);
                        now = timeProvider.GetUtcNow();
                        await store.SetAsync(new ScoreboardSnapshot(scoreboard, now), cancellationToken);
                        knownMatches.AddRange(scoreboard.Matches);
                        schedule.Set(leagueCode, date, now + PollingIntervalCalculator.Calculate(scoreboard.Matches, now, options));

                        // Store first, then push: a client that reacts by calling the REST API sees the new state.
                        await PublishChangesAsync(MatchChangeDetector.Detect(previous?.Scoreboard, scoreboard), cancellationToken);
                    }
                    catch (FootballDataProviderException ex)
                    {
                        // Keep serving the last good snapshot; still use it to plan the next poll, which comes sooner.
                        LogFetchFailed(ex, leagueCode, date);
                        var cached = await store.GetAsync(leagueCode, date, cancellationToken);
                        knownMatches.AddRange(cached?.Scoreboard.Matches ?? []);

                        now = timeProvider.GetUtcNow();
                        var retry = PollingIntervalCalculator.Calculate(cached?.Scoreboard.Matches ?? [], now, options);
                        schedule.Set(leagueCode, date, now + (retry > options.ErrorRetryInterval ? options.ErrorRetryInterval : retry));
                    }
                }

                var dueAt = schedule.DueAt(leagueCode, date);
                if (dueAt is not null && (nextDue is null || dueAt < nextDue))
                {
                    nextDue = dueAt;
                }
            }
        }

        // After the scoreboards, so the details pushed to open match pages are never behind the scores.
        await detailRefresher.RefreshAsync(knownMatches, cancellationToken);

        // Counted from when each league was fetched, so a slow round doesn't stretch the rhythm of the live ones.
        var delay = (nextDue ?? timeProvider.GetUtcNow() + options.IdleInterval) - timeProvider.GetUtcNow();
        delay = delay < MinDelay ? MinDelay : delay > options.IdleInterval ? options.IdleInterval : delay;

        LogPollCompleted(knownMatches.Count, knownMatches.Count(m => m.Status.IsInPlay()), delay);
        return delay;
    }

    /// <summary>
    /// Push is best effort: the store is already updated, so a failed push only delays clients
    /// until their next refresh. It must never fail the poll or affect the polling schedule.
    /// </summary>
    private async Task PublishChangesAsync(IReadOnlyList<MatchChange> changes, CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            return;
        }

        try
        {
            await publisher.PublishAsync(changes, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPublishFailed(ex, changes.Count);
        }
    }

    /// <summary>
    /// Always today (Istanbul). Also yesterday while it still has active matches,
    /// so a Champions League game running past midnight keeps updating until full time.
    /// Yesterday is also polled when we know nothing about it yet (e.g. right after a restart at 00:30),
    /// otherwise such a game would never be noticed.
    /// </summary>
    private async Task<IReadOnlyList<DateOnly>> GetDatesToPollAsync(string leagueCode, DateOnly today, CancellationToken cancellationToken)
    {
        var yesterday = today.AddDays(-1);
        var yesterdaySnapshot = await store.GetAsync(leagueCode, yesterday, cancellationToken);
        var now = timeProvider.GetUtcNow();

        return yesterdaySnapshot is null || yesterdaySnapshot.Scoreboard.Matches.Any(m => PollingIntervalCalculator.IsActive(m, now))
            ? [yesterday, today]
            : [today];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fetching {LeagueCode} for {Date} failed; serving cached data")]
    private partial void LogFetchFailed(Exception exception, string leagueCode, DateOnly date);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing {ChangeCount} match changes failed")]
    private partial void LogPublishFailed(Exception exception, int changeCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Poll completed: {MatchCount} matches, {LiveCount} live. Next poll in {Delay}")]
    private partial void LogPollCompleted(int matchCount, int liveCount, TimeSpan delay);
}
