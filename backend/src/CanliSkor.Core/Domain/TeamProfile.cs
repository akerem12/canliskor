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

/// <summary>One team in one competition: who they are, how they have been doing and what comes next.</summary>
/// <param name="StandingSummary">E.g. "3rd in Turkish Super Lig". Null if the competition has no table.</param>
/// <param name="Stadium">Where the team plays its home matches. Null if unknown, and for national teams.</param>
/// <param name="RecentMatches">Played matches in this competition, newest first.</param>
/// <param name="UpcomingMatches">Fixtures in this competition, soonest first.</param>
public sealed record TeamProfile(
    string LeagueCode,
    Team Team,
    bool IsNationalTeam,
    string? StandingSummary,
    string? Stadium,
    string? StadiumCity,
    IReadOnlyList<Match> RecentMatches,
    IReadOnlyList<Match> UpcomingMatches);

/// <summary>Something loaded from the provider plus when we fetched it.</summary>
public sealed record Timestamped<T>(T Value, DateTimeOffset FetchedAtUtc)
    where T : class;
