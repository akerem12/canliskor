using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Services;
using Microsoft.Extensions.Logging;

namespace CanliSkor.Core.Polling;

/// <summary>
/// Part of a polling round: reloads the detail of every match that is in play and has viewers, and pushes it to
/// them. The scoreboard only tells score and status, so this is what brings cards, substitutions, statistics and
/// ratings to an open match page without the page asking.
/// </summary>
public sealed partial class LiveDetailRefresher(
    MatchViewerRegistry viewers,
    MatchDetailService details,
    IMatchStore store,
    IMatchUpdatePublisher publisher,
    ILogger<LiveDetailRefresher> logger)
{
    /// <param name="knownMatches">The matches of the scoreboards just polled.</param>
    public async Task RefreshAsync(IReadOnlyCollection<Match> knownMatches, CancellationToken cancellationToken)
    {
        foreach (var (leagueCode, matchId) in viewers.Watched())
        {
            // Before kickoff and after full time nothing changes from poll to poll.
            var match = knownMatches.FirstOrDefault(m => m.LeagueCode == leagueCode && m.Id == matchId);
            if (match is null || !match.Status.IsInPlay())
            {
                continue;
            }

            // Best effort, like the score push: a failure for one match must not fail the poll or the other matches.
            try
            {
                var before = await store.GetDetailAsync(leagueCode, matchId, cancellationToken);
                var snapshot = await details.RefreshAsync(leagueCode, matchId, cancellationToken);

                // The same snapshot again means nothing new was loaded (just refreshed, or the provider failed).
                if (snapshot is not null && !ReferenceEquals(snapshot, before))
                {
                    await publisher.PublishDetailAsync(snapshot, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRefreshFailed(ex, leagueCode, matchId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refreshing the detail of watched match {MatchId} ({LeagueCode}) failed")]
    private partial void LogRefreshFailed(Exception exception, string leagueCode, string matchId);
}
