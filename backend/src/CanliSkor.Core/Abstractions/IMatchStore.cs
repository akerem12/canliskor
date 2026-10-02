using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Abstractions;

/// <summary>
/// Latest known scoreboards, keyed by league and date. Written only by the poller, read by everyone else.
/// Async on purpose: the in-memory version doesn't need it, but a Redis version will.
/// </summary>
public interface IMatchStore
{
    Task<ScoreboardSnapshot?> GetAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default);

    Task SetAsync(ScoreboardSnapshot snapshot, CancellationToken cancellationToken = default);
}
