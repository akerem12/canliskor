namespace CanliSkor.Core.Polling;

/// <summary>
/// Which matches have someone looking at their page right now, by connection. The poller refreshes the details
/// of exactly these matches while they are in play, so details nobody is watching cost no provider calls.
/// Thread-safe: connections come and go on request threads while the poller reads.
/// </summary>
public sealed class MatchViewerRegistry
{
    /// <summary>Upper bound per connection, so one client can't make the poller refresh every live match.</summary>
    public const int MaxMatchesPerConnection = 4;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, HashSet<(string LeagueCode, string MatchId)>> _byConnection = new();

    /// <returns>False if the connection already watches <see cref="MaxMatchesPerConnection"/> other matches.</returns>
    public bool Watch(string connectionId, string leagueCode, string matchId)
    {
        lock (_lock)
        {
            if (!_byConnection.TryGetValue(connectionId, out var matches))
            {
                _byConnection[connectionId] = matches = [];
            }

            if (matches.Contains((leagueCode, matchId)))
            {
                return true;
            }

            if (matches.Count >= MaxMatchesPerConnection)
            {
                return false;
            }

            matches.Add((leagueCode, matchId));
            return true;
        }
    }

    public void Unwatch(string connectionId, string leagueCode, string matchId)
    {
        lock (_lock)
        {
            if (_byConnection.TryGetValue(connectionId, out var matches) && matches.Remove((leagueCode, matchId)) && matches.Count == 0)
            {
                _byConnection.Remove(connectionId);
            }
        }
    }

    /// <summary>The connection is gone: it no longer watches anything.</summary>
    public void Disconnect(string connectionId)
    {
        lock (_lock)
        {
            _byConnection.Remove(connectionId);
        }
    }

    /// <summary>Every match with at least one viewer, each once.</summary>
    public IReadOnlyList<(string LeagueCode, string MatchId)> Watched()
    {
        lock (_lock)
        {
            return _byConnection.Values.SelectMany(matches => matches).Distinct().ToList();
        }
    }
}
