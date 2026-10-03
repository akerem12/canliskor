namespace CanliSkor.Infrastructure.Espn.Dtos;

// Mirrors only the parts of ESPN's team schedule (/teams/{id}/schedule) and standings JSON that we use.

/// <param name="Events">Results come newest first; fixtures (?fixture=true) soonest first.</param>
internal sealed record EspnScheduleResponse(EspnScheduleTeam? Team, IReadOnlyList<EspnScheduleEvent>? Events);

/// <param name="StandingSummary">E.g. "3rd in Turkish Super Lig".</param>
internal sealed record EspnScheduleTeam(string? Id, string? DisplayName, string? Logo, string? StandingSummary, bool IsNational);

/// <param name="League">The competition the match belongs to; its slug is our league code.</param>
internal sealed record EspnScheduleEvent(string? Id, string? Date, EspnLeague? League, IReadOnlyList<EspnScheduleCompetition>? Competitions);

internal sealed record EspnScheduleCompetition(EspnStatus? Status, EspnVenue? Venue, IReadOnlyList<EspnScheduleCompetitor>? Competitors);

internal sealed record EspnVenue(string? FullName, EspnVenueAddress? Address);

internal sealed record EspnVenueAddress(string? City);

/// <param name="Score">An object here, unlike the scoreboard's plain string. Missing before kickoff.</param>
internal sealed record EspnScheduleCompetitor(string? HomeAway, EspnTeam? Team, EspnScheduleScore? Score);

internal sealed record EspnScheduleScore(string? DisplayValue);

/// <param name="Children">One per group; a plain league has a single one, a competition without a table none.</param>
internal sealed record EspnStandingsResponse(string? Name, IReadOnlyList<EspnStandingsGroup>? Children);

internal sealed record EspnStandingsGroup(string? Name, EspnStandingsTable? Standings);

internal sealed record EspnStandingsTable(IReadOnlyList<EspnStandingsEntry>? Entries);

internal sealed record EspnStandingsEntry(EspnStandingsTeam? Team, EspnStandingsNote? Note, IReadOnlyList<EspnStandingsStat>? Stats);

/// <remarks>Standings carry no logo, unlike every other team shape.</remarks>
internal sealed record EspnStandingsTeam(string? Id, string? DisplayName, string? ShortDisplayName, bool IsNational);

/// <param name="Color">"#81D6AC"</param>
/// <param name="Description">E.g. "Champions League", "Relegation".</param>
internal sealed record EspnStandingsNote(string? Color, string? Description);

/// <param name="Name">"rank", "gamesPlayed", "wins", "ties", "losses", "pointsFor", "pointsAgainst", "pointDifferential", "points", ...</param>
internal sealed record EspnStandingsStat(string? Name, double? Value);
