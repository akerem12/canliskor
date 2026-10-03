using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Abstractions;

/// <summary>
/// Latest known data: scoreboards keyed by league and date (written by the poller and the on-demand loader),
/// and match details keyed by league and match id (written by <see cref="Services.MatchDetailService"/>).
/// Async on purpose: the in-memory version doesn't need it, but a Redis version will.
/// </summary>
public interface IMatchStore
{
    Task<ScoreboardSnapshot?> GetAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default);

    Task SetAsync(ScoreboardSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<MatchDetailSnapshot?> GetDetailAsync(string leagueCode, string matchId, CancellationToken cancellationToken = default);

    Task SetDetailAsync(MatchDetailSnapshot snapshot, CancellationToken cancellationToken = default);
}
