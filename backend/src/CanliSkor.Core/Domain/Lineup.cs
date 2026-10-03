namespace CanliSkor.Core.Domain;

/// <summary>Both teams' line-ups. Only exists once both have been announced.</summary>
public sealed record MatchLineups(TeamLineup Home, TeamLineup Away);

/// <summary>How the two teams may line up in a match that hasn't announced its line-ups. A side is null if nothing is known.</summary>
public sealed record ExpectedLineups(ExpectedLineup? Home, ExpectedLineup? Away);

/// <param name="Lineup">The starting eleven only, without anything that happened in a match.</param>
/// <param name="BasedOn">The team's earlier match this line-up was taken from.</param>
public sealed record ExpectedLineup(TeamLineup Lineup, Match BasedOn);

/// <param name="Formation">E.g. "4-2-3-1": the number of outfield players per row, defence first.</param>
/// <param name="ShirtColor">Colour of the shirt worn in this match as "#rrggbb". Null if unknown.</param>
/// <param name="Rows">
/// The starting eleven as they line up: the goalkeeper's row first, then defence to attack;
/// each row from the team's own left to right.
/// </param>
/// <param name="Bench">Substitutes, whether they came on or not.</param>
public sealed record TeamLineup(
    string Formation,
    string? ShirtColor,
    IReadOnlyList<IReadOnlyList<LineupPlayer>> Rows,
    IReadOnlyList<LineupPlayer> Bench);

/// <param name="ShortName">E.g. "G. Orban".</param>
/// <param name="Position">
/// A substitute who came on has the position of the player they replaced; null for one who stayed on the bench.
/// </param>
/// <param name="CameOnAt">Match minute as displayed, e.g. "76'". Null for starters and unused substitutes.</param>
/// <param name="WentOffAt">Match minute the player was substituted off. Null if they weren't.</param>
/// <param name="SentOffAt">Match minute of the player's red card. Null if they weren't sent off.</param>
/// <param name="MinutesPlayed">Null for a substitute who hasn't come on, and before kickoff.</param>
public sealed record LineupPlayer(
    string Id,
    string Name,
    string ShortName,
    string? Jersey,
    PlayerPosition? Position,
    string? CameOnAt,
    string? WentOffAt,
    PlayerMatchStats Stats,
    string? SentOffAt = null,
    int? MinutesPlayed = null);

public enum PlayerPosition
{
    Goalkeeper,
    Defender,
    Midfielder,
    Forward,
}

/// <summary>What one player did in one match. All counts.</summary>
/// <param name="GoalsConceded">Goals the team conceded while this player was on the pitch.</param>
public sealed record PlayerMatchStats(
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
