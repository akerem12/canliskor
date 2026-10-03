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

/// <param name="Stats">Empty before kickoff.</param>
/// <param name="Lineups">Null until both line-ups are announced.</param>
/// <param name="LastUpdatedUtc">When the detail was fetched from the data source.</param>
public sealed record MatchDetailResponse(
    MatchResponse Match,
    IReadOnlyList<MatchEventResponse> Events,
    IReadOnlyList<MatchStatResponse> Stats,
    MatchLineupsResponse? Lineups,
    DateTimeOffset LastUpdatedUtc);

/// <param name="Side">"Home" or "Away": the team the event counts for (an own goal counts for the team that benefits).</param>
/// <param name="Player">Scorer, booked player, or the player coming on.</param>
/// <param name="RelatedPlayer">Assist provider, or the player going off.</param>
public sealed record MatchEventResponse(MatchEventType Type, string Clock, TeamSide Side, string? Player, string? RelatedPlayer);

public sealed record MatchStatResponse(MatchStatType Type, double Home, double Away);

public sealed record MatchLineupsResponse(TeamLineupResponse Home, TeamLineupResponse Away);

/// <param name="Formation">E.g. "4-2-3-1": the number of outfield players per row, defence first.</param>
/// <param name="ShirtColor">"#rrggbb", or null if unknown.</param>
/// <param name="Rows">Starting eleven: the goalkeeper's row first, then defence to attack; each row from the team's own left to right.</param>
/// <param name="Bench">Substitutes, whether they came on or not.</param>
public sealed record TeamLineupResponse(
    string Formation,
    string? ShirtColor,
    IReadOnlyList<IReadOnlyList<LineupPlayerResponse>> Rows,
    IReadOnlyList<LineupPlayerResponse> Bench);

/// <param name="Position">A substitute who came on has the position of the player they replaced; null if they stayed on the bench.</param>
/// <param name="CameOnAt">Match minute, e.g. "76'"; null for starters and unused substitutes.</param>
/// <param name="WentOffAt">Match minute the player was substituted off, else null.</param>
/// <param name="SentOffAt">Match minute of the player's red card, else null.</param>
/// <param name="MinutesPlayed">Null for a substitute who hasn't come on, and before kickoff.</param>
public sealed record LineupPlayerResponse(
    string Id,
    string Name,
    string ShortName,
    string? Jersey,
    PlayerPosition? Position,
    string? CameOnAt,
    string? WentOffAt,
    string? SentOffAt,
    int? MinutesPlayed,
    PlayerStatsResponse Stats);

/// <param name="GoalsConceded">Goals the team conceded while the player was on the pitch.</param>
public sealed record PlayerStatsResponse(
    int Goals,
    int Assists,
    int Shots,
    int ShotsOnTarget,
    int FoulsCommitted,
    int FoulsSuffered,
    int Offsides,
    int YellowCards,
    int RedCards,
    int OwnGoals,
    int Saves,
    int GoalsConceded);

/// <param name="Players">Goalkeepers first, then defenders, midfielders and forwards; by shirt number within each.</param>
/// <param name="LastUpdatedUtc">When the squad was fetched from the data source.</param>
public sealed record SquadResponse(string TeamId, string TeamName, IReadOnlyList<SquadPlayerResponse> Players, DateTimeOffset LastUpdatedUtc);

public sealed record SquadPlayerResponse(string Id, string Name, string? Jersey, PlayerPosition? Position, int? Age, string? Nationality);

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

    public static MatchDetailResponse ToResponse(this MatchDetailSnapshot snapshot) => new(
        snapshot.Detail.Match.ToResponse(),
        snapshot.Detail.Events.Select(e => new MatchEventResponse(e.Type, e.Clock, e.Side, e.Player, e.RelatedPlayer)).ToList(),
        snapshot.Detail.Stats.Select(s => new MatchStatResponse(s.Type, s.Home, s.Away)).ToList(),
        snapshot.Detail.Lineups is { } lineups ? new MatchLineupsResponse(ToResponse(lineups.Home), ToResponse(lineups.Away)) : null,
        snapshot.FetchedAtUtc);

    public static SquadResponse ToResponse(this SquadSnapshot snapshot) => new(
        snapshot.Squad.TeamId,
        snapshot.Squad.TeamName,
        snapshot.Squad.Players.Select(p => new SquadPlayerResponse(p.Id, p.Name, p.Jersey, p.Position, p.Age, p.Nationality)).ToList(),
        snapshot.FetchedAtUtc);

    public static MatchUpdatedMessage ToMessage(this MatchChange change) => new(
        change.Match.ToResponse(),
        change.Kinds.HasFlag(MatchChangeKind.Score),
        change.Kinds.HasFlag(MatchChangeKind.Status));

    private static TeamResponse ToResponse(Team team) => new(team.Id, team.Name, team.ShortName, team.LogoUrl);

    private static TeamLineupResponse ToResponse(TeamLineup lineup) => new(
        lineup.Formation,
        lineup.ShirtColor,
        lineup.Rows.Select(row => (IReadOnlyList<LineupPlayerResponse>)row.Select(ToResponse).ToList()).ToList(),
        lineup.Bench.Select(ToResponse).ToList());

    private static LineupPlayerResponse ToResponse(LineupPlayer player) => new(
        player.Id,
        player.Name,
        player.ShortName,
        player.Jersey,
        player.Position,
        player.CameOnAt,
        player.WentOffAt,
        player.SentOffAt,
        player.MinutesPlayed,
        ToResponse(player.Stats));

    private static PlayerStatsResponse ToResponse(PlayerMatchStats s) => new(
        s.Goals, s.Assists, s.Shots, s.ShotsOnTarget, s.FoulsCommitted, s.FoulsSuffered,
        s.Offsides, s.YellowCards, s.RedCards, s.OwnGoals, s.Saves, s.GoalsConceded);
}
