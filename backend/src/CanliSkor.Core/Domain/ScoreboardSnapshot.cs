namespace CanliSkor.Core.Domain;

/// <summary>A scoreboard plus when we fetched it, so clients can tell how fresh (or stale) the data is.</summary>
public sealed record ScoreboardSnapshot(LeagueScoreboard Scoreboard, DateTimeOffset FetchedAtUtc);
