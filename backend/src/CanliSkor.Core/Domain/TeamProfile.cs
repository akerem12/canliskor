namespace CanliSkor.Core.Domain;

/// <summary>A league table. Cup-style competitions have several groups; a competition without a table has none.</summary>
public sealed record LeagueStandings(string LeagueCode, string LeagueName, IReadOnlyList<StandingsGroup> Groups);

/// <param name="Name">E.g. "Group A1". For a plain league, the season's name.</param>
/// <param name="Rows">In table order.</param>
public sealed record StandingsGroup(string Name, IReadOnlyList<StandingsRow> Rows);

/// <param name="Note">What the position means, e.g. "Champions League" or "Relegation". Null for most rows.</param>
/// <param name="NoteColor">Colour the provider marks that zone with, "#rrggbb". Null if none.</param>
public sealed record StandingsRow(
    int Rank,
    Team Team,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points,
    string? Note,
    string? NoteColor);

/// <summary>One team: who they are, how they have been doing and what comes next, across every competition they play in.</summary>
/// <param name="LeagueCode">The competition the team was looked up in.</param>
/// <param name="StandingSummary">E.g. "3rd in Turkish Super Lig" (the team's main league). Null if it has no table.</param>
/// <param name="Stadium">Where the team plays its home matches. Null if unknown, and for national teams.</param>
/// <param name="RecentMatches">This season's played matches in all competitions, newest first.</param>
/// <param name="UpcomingMatches">Every scheduled match in all competitions, soonest first.</param>
/// <param name="Competitions">The competitions those matches belong to, with their names; not all of them are followed.</param>
public sealed record TeamProfile(
    string LeagueCode,
    Team Team,
    bool IsNationalTeam,
    string? StandingSummary,
    string? Stadium,
    string? StadiumCity,
    IReadOnlyList<Match> RecentMatches,
    IReadOnlyList<Match> UpcomingMatches,
    IReadOnlyList<League> Competitions);

/// <summary>Something loaded from the provider plus when we fetched it.</summary>
public sealed record Timestamped<T>(T Value, DateTimeOffset FetchedAtUtc)
    where T : class;
