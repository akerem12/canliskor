namespace CanliSkor.Core.Domain;

/// <param name="KickoffUtc">Always UTC. Conversion to Europe/Istanbul happens at the edges (API/UI).</param>
/// <param name="Clock">Display text for the match minute, e.g. "67'" or "90'+4'". Null before kickoff.</param>
/// <param name="Score">Null until the match has started.</param>
public sealed record Match(
    string Id,
    string LeagueCode,
    DateTimeOffset KickoffUtc,
    MatchStatus Status,
    string? Clock,
    Team HomeTeam,
    Team AwayTeam,
    Score? Score);
