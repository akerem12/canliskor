using System.Collections.Concurrent;

namespace CanliSkor.Core.Polling;

/// <summary>
/// When each league's day is due for its next poll: every few seconds while it has a match in play or about to
/// kick off, rarely otherwise. Kept in memory only, and nothing depends on it surviving: after a restart every
/// league is simply due at once.
/// </summary>
public sealed class PollSchedule
{
    private readonly ConcurrentDictionary<(string LeagueCode, DateOnly Date), DateTimeOffset> _dueAt = new();

    /// <summary>True for a league's day that was never polled, too.</summary>
    public bool IsDue(string leagueCode, DateOnly date, DateTimeOffset nowUtc) =>
        !_dueAt.TryGetValue((leagueCode, date), out var dueAt) || dueAt <= nowUtc;

    public void Set(string leagueCode, DateOnly date, DateTimeOffset dueAtUtc) => _dueAt[(leagueCode, date)] = dueAtUtc;

    /// <returns>Null if the league's day was never polled.</returns>
    public DateTimeOffset? DueAt(string leagueCode, DateOnly date) =>
        _dueAt.TryGetValue((leagueCode, date), out var dueAt) ? dueAt : null;

    /// <summary>Drops the days nobody polls any more.</summary>
    public void ForgetBefore(DateOnly date)
    {
        foreach (var key in _dueAt.Keys.Where(k => k.Date < date))
        {
            _dueAt.TryRemove(key, out _);
        }
    }
}
