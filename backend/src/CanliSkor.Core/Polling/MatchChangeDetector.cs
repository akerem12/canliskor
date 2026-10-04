using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Polling;

/// <summary>
/// Compares two polls of the same scoreboard and reports what changed — the input for real-time push.
/// Pure (no I/O, no clock), so every case is a plain unit test.
/// </summary>
public static class MatchChangeDetector
{
    private static readonly Score NoGoals = new(0, 0);

    /// <remarks>
    /// No previous scoreboard (e.g. first poll after startup) means no changes: there is nothing to compare
    /// against, and clients load the full state over REST anyway. Matches that appear for the first time are
    /// skipped for the same reason; matches that disappear produce nothing.
    /// </remarks>
    public static IReadOnlyList<MatchChange> Detect(LeagueScoreboard? previous, LeagueScoreboard current)
    {
        if (previous is null)
        {
            return [];
        }

        var previousById = previous.Matches.ToDictionary(m => m.Id);
        var changes = new List<MatchChange>();

        foreach (var match in current.Matches)
        {
            if (!previousById.TryGetValue(match.Id, out var before))
            {
                continue;
            }

            var kinds = MatchChangeKind.None;
            // Before kick-off there is no score and at kick-off it turns 0-0: nobody scored, so that is no score change.
            if ((match.Score ?? NoGoals) != (before.Score ?? NoGoals)) kinds |= MatchChangeKind.Score;
            if (match.Status != before.Status) kinds |= MatchChangeKind.Status;
            if (match.Clock != before.Clock) kinds |= MatchChangeKind.Clock;

            if (kinds != MatchChangeKind.None)
            {
                changes.Add(new MatchChange(match, kinds));
            }
        }

        return changes;
    }
}
