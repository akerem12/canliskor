using System.Collections.Concurrent;
using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// What has been sent already, so no browser is told the same thing twice however often the notifier runs, when
/// each match's line-ups were last looked for, and which events of the matches in play have been seen. Kept in
/// memory: after a restart, what happened before is taken as known (<see cref="StartLiveMatch"/>), so nothing is
/// sent twice; what happens while the server is down is not sent at all.
/// </summary>
public sealed class NotificationLog
{
    /// <summary>Entries older than this are about matches long over.</summary>
    private static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<(string Notification, string Endpoint), DateTimeOffset> _sent = new();
    private readonly ConcurrentDictionary<string, LineupCheck> _lineupChecks = new();
    private readonly ConcurrentDictionary<string, LiveMatchLog> _liveMatches = new();

    public bool WasSent(string notification, string endpoint) => _sent.ContainsKey((notification, endpoint));

    public void MarkSent(string notification, string endpoint, DateTimeOffset now) => _sent[(notification, endpoint)] = now;

    /// <summary>The last look at a match's line-ups; null if nobody has looked yet.</summary>
    public LineupCheck? LastLineupCheck(string matchId) => _lineupChecks.GetValueOrDefault(matchId);

    public void RecordLineupCheck(string matchId, LineupCheck check) => _lineupChecks[matchId] = check;

    /// <summary>What is known of a match in play; null if it hasn't been looked at since the server started.</summary>
    public LiveMatchLog? LiveMatch(string matchId) => _liveMatches.GetValueOrDefault(matchId);

    /// <param name="alreadyHappened">Events from before the server was watching, which are no news any more.</param>
    /// <param name="sawKickoff">The match was seen in its first minutes, so its kickoff is news.</param>
    public LiveMatchLog StartLiveMatch(string matchId, IEnumerable<string> alreadyHappened, DateTimeOffset now, bool sawKickoff) =>
        _liveMatches[matchId] = new LiveMatchLog(now, alreadyHappened, sawKickoff);

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

        foreach (var (matchId, live) in _liveMatches)
        {
            if (now - live.StartedAt > ForgetAfter)
            {
                _liveMatches.TryRemove(matchId, out _);
            }
        }
    }
}

/// <param name="Announced">Both teams' starting elevens were there.</param>
public sealed record LineupCheck(DateTimeOffset At, bool Announced);

/// <summary>
/// A match in play as the notifier has seen it: when each of its events was first seen, so every event is news
/// only once and only for a while. Used by one notifier round at a time.
/// </summary>
public sealed class LiveMatchLog
{
    private readonly Dictionary<string, DateTimeOffset> _firstSeen = [];

    public LiveMatchLog(DateTimeOffset startedAt, IEnumerable<string> alreadyHappened, bool sawKickoff)
    {
        StartedAt = startedAt;
        SawKickoff = sawKickoff;
        foreach (var key in alreadyHappened)
        {
            _firstSeen[key] = DateTimeOffset.MinValue;
        }
    }

    /// <summary>When the notifier first saw the match in play.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>The match was first seen in its opening minutes, so its kickoff is announced.</summary>
    public bool SawKickoff { get; }

    /// <summary>The final whistle was dealt with; nothing more is sent for this match.</summary>
    public bool Over { get; set; }

    /// <summary>The status in the previous round; null in the first.</summary>
    public MatchStatus? LastStatus { get; set; }

    /// <summary>When the match was seen going from play to half time; null if it wasn't (yet).</summary>
    public DateTimeOffset? HalfTimeAt { get; set; }

    /// <summary>When the match was seen going from half time back to play; null if it wasn't (yet).</summary>
    public DateTimeOffset? SecondHalfAt { get; set; }

    /// <summary>When the match was seen going from play to finished; null if it wasn't (yet).</summary>
    public DateTimeOffset? FullTimeAt { get; set; }

    /// <summary>
    /// Goals that were announced, by the key of the entry that was announced, so one taken back can be announced
    /// too. A goal the provider entered more than once is one goal: see <see cref="GoalEntries"/>.
    /// </summary>
    public Dictionary<string, MatchEvent> AnnouncedGoals { get; } = [];

    /// <summary>Every entry of an announced goal, by event key: the key of the entry that was announced.</summary>
    public Dictionary<string, string> GoalEntries { get; } = [];

    /// <summary>Announced goals that were taken back, by the announced entry's key: the goal and when that was seen.</summary>
    public Dictionary<string, (MatchEvent Goal, DateTimeOffset At)> DisallowedGoals { get; } = [];

    /// <summary>When the event was first seen; now, if this is the first time.</summary>
    public DateTimeOffset FirstSeen(string eventKey, DateTimeOffset now)
    {
        if (!_firstSeen.TryGetValue(eventKey, out var seen))
        {
            _firstSeen[eventKey] = seen = now;
        }

        return seen;
    }
}
