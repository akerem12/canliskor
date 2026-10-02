using CanliSkor.Core.Domain;
using CanliSkor.Core.Time;

namespace CanliSkor.Api.Contracts;

// Public API shape. Kept separate from domain models so the domain can evolve
// without silently breaking clients (and vice versa).

public sealed record MatchDayResponse(DateOnly Date, IReadOnlyList<LeagueMatchesResponse> Leagues);

public sealed record LeagueMatchesResponse(
    string Code,
    string Name,
    DateTimeOffset LastUpdatedUtc,
    IReadOnlyList<MatchResponse> Matches);

/// <param name="Kickoff">Kickoff in Istanbul time, e.g. "2026-10-09T20:00:00+03:00" — an exact instant, ready to display.</param>
public sealed record MatchResponse(
    string Id,
    string LeagueCode,
    DateTimeOffset Kickoff,
    MatchStatus Status,
    string? Clock,
    TeamResponse HomeTeam,
    TeamResponse AwayTeam,
    ScoreResponse? Score);

public sealed record TeamResponse(string Id, string Name, string ShortName, string? LogoUrl);

public sealed record ScoreResponse(int Home, int Away);

/// <summary>SignalR "MatchUpdated" payload: the full new match state plus what changed (e.g. to animate a goal).</summary>
/// <remarks>Both flags false means only the clock moved.</remarks>
public sealed record MatchUpdatedMessage(MatchResponse Match, bool ScoreChanged, bool StatusChanged);

public static class ContractMappings
{
    public static LeagueMatchesResponse ToResponse(this ScoreboardSnapshot snapshot) => new(
        snapshot.Scoreboard.League.Code,
        snapshot.Scoreboard.League.Name,
        snapshot.FetchedAtUtc,
        snapshot.Scoreboard.Matches.Select(ToResponse).ToList());

    public static MatchResponse ToResponse(this Match match) => new(
        match.Id,
        match.LeagueCode,
        IstanbulTime.ToIstanbul(match.KickoffUtc),
        match.Status,
        match.Clock,
        ToResponse(match.HomeTeam),
        ToResponse(match.AwayTeam),
        match.Score is { } s ? new ScoreResponse(s.Home, s.Away) : null);

    public static MatchUpdatedMessage ToMessage(this MatchChange change) => new(
        change.Match.ToResponse(),
        change.Kinds.HasFlag(MatchChangeKind.Score),
        change.Kinds.HasFlag(MatchChangeKind.Status));

    private static TeamResponse ToResponse(Team team) => new(team.Id, team.Name, team.ShortName, team.LogoUrl);
}
