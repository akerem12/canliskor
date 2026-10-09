using System.Collections.Concurrent;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// What has been sent already, so no browser is told the same thing twice however often the notifier runs, and
/// when each match's line-ups were last looked for.
/// </summary>
public sealed class NotificationLog
{
    /// <summary>Entries older than this are about matches long over.</summary>
    private static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<(string Notification, string Endpoint), DateTimeOffset> _sent = new();
    private readonly ConcurrentDictionary<string, LineupCheck> _lineupChecks = new();

    public bool WasSent(string notification, string endpoint) => _sent.ContainsKey((notification, endpoint));

    public void MarkSent(string notification, string endpoint, DateTimeOffset now) => _sent[(notification, endpoint)] = now;

    /// <summary>The last look at a match's line-ups; null if nobody has looked yet.</summary>
    public LineupCheck? LastLineupCheck(string matchId) => _lineupChecks.GetValueOrDefault(matchId);

    public void RecordLineupCheck(string matchId, LineupCheck check) => _lineupChecks[matchId] = check;

    public void ForgetOld(DateTimeOffset now)
    {
        foreach (var (key, sentAt) in _sent)
        {
            if (now - sentAt > ForgetAfter)
            {
                _sent.TryRemove(key, out _);
            }
        }

        foreach (var (matchId, check) in _lineupChecks)
        {
            if (now - check.At > ForgetAfter)
            {
                _lineupChecks.TryRemove(matchId, out _);
            }
        }
    }
}

/// <param name="Announced">Both teams' starting elevens were there.</param>
public sealed record LineupCheck(DateTimeOffset At, bool Announced);
