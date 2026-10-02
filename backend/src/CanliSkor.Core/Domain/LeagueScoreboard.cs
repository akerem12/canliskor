namespace CanliSkor.Core.Domain;

/// <summary>All matches of one league on one day — the unit we fetch, cache and diff.</summary>
public sealed record LeagueScoreboard(League League, DateOnly Date, IReadOnlyList<Match> Matches);
