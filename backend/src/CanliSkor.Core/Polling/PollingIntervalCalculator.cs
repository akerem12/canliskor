using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;

namespace CanliSkor.Core.Polling;

/// <summary>
/// Decides how long to wait before the next poll. Pure function of (matches, now, options) — no I/O, easy to test.
/// </summary>
public static class PollingIntervalCalculator
{
    /// <summary>
    /// A match still "Scheduled" this long after kickoff is treated as possibly live (providers often flip
    /// the status a little late). After that we assume it's stale data, e.g. an unannounced postponement.
    /// </summary>
    public static readonly TimeSpan OverdueKickoffWindow = TimeSpan.FromHours(3);

    public static TimeSpan Calculate(IEnumerable<Match> matches, DateTimeOffset nowUtc, PollingOptions options)
    {
        DateTimeOffset? nextKickoff = null;

        foreach (var match in matches)
        {
            if (IsActive(match, nowUtc))
            {
                return options.LiveInterval;
            }

            if (match.Status == MatchStatus.Scheduled && match.KickoffUtc > nowUtc &&
                (nextKickoff is null || match.KickoffUtc < nextKickoff))
            {
                nextKickoff = match.KickoffUtc;
            }
        }

        if (nextKickoff is null)
        {
            return options.IdleInterval;
        }

        // Sleep until shortly before the next kickoff, but never longer than the idle interval
        // (fixtures can change) and never shorter than the live interval.
        var untilWake = nextKickoff.Value - options.KickoffLeadTime - nowUtc;
        return Clamp(untilWake, options.LiveInterval, options.IdleInterval);
    }

    /// <summary>True when a match is in play, or should have kicked off but the provider hasn't caught up yet.</summary>
    public static bool IsActive(Match match, DateTimeOffset nowUtc) =>
        match.Status.IsInPlay() ||
        (match.Status == MatchStatus.Scheduled && match.KickoffUtc <= nowUtc && nowUtc - match.KickoffUtc < OverdueKickoffWindow);

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) =>
        value < min ? min : value > max ? max : value;
}
