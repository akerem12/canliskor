namespace CanliSkor.Infrastructure.Espn.Dtos;

// Mirrors only the parts of ESPN's match summary JSON (/summary?event={id}) that we use.
// Team and competitor shapes are shared with the scoreboard DTOs.

internal sealed record EspnSummaryResponse(
    EspnSummaryHeader? Header,
    EspnBoxscore? Boxscore,
    IReadOnlyList<EspnKeyEvent>? KeyEvents,
    IReadOnlyList<EspnRoster>? Rosters = null);

internal sealed record EspnSummaryHeader(string? Id, EspnLeague? League, IReadOnlyList<EspnSummaryCompetition>? Competitions);

internal sealed record EspnSummaryCompetition(string? Date, EspnStatus? Status, IReadOnlyList<EspnCompetitor>? Competitors);

internal sealed record EspnBoxscore(IReadOnlyList<EspnBoxscoreTeam>? Teams);

internal sealed record EspnBoxscoreTeam(string? HomeAway, IReadOnlyList<EspnStatistic>? Statistics);

internal sealed record EspnStatistic(string? Name, string? DisplayValue);

/// <param name="Participants">Goal: scorer, then assist. Substitution: player on, then player off.</param>
internal sealed record EspnKeyEvent(
    EspnKeyEventType? Type,
    EspnClock? Clock,
    bool ScoringPlay,
    EspnEventTeam? Team,
    IReadOnlyList<EspnParticipant>? Participants);

/// <param name="Type">Slug such as "goal", "goal---header", "penalty---scored", "own-goal", "yellow-card".</param>
internal sealed record EspnKeyEventType(string? Type);

internal sealed record EspnClock(string? DisplayValue);

internal sealed record EspnEventTeam(string? Id);

internal sealed record EspnParticipant(EspnAthlete? Athlete);

internal sealed record EspnAthlete(string? DisplayName, string? Id = null, string? ShortName = null);

/// <param name="Formation">E.g. "4-2-3-1".</param>
/// <param name="Roster">Starters and substitutes. Missing until the line-up is announced.</param>
internal sealed record EspnRoster(
    string? HomeAway,
    string? Formation,
    EspnUniform? Uniform,
    IReadOnlyList<EspnRosterEntry>? Roster);

/// <param name="Color">Hex without "#", e.g. "990000".</param>
internal sealed record EspnUniform(string? Color);

/// <param name="SubbedInFor">The player this substitute replaced.</param>
/// <param name="Plays">The player's cards, goals and substitutions, in match order.</param>
internal sealed record EspnRosterEntry(
    bool Starter,
    string? Jersey,
    EspnAthlete? Athlete,
    EspnPosition? Position,
    bool SubbedIn,
    bool SubbedOut,
    EspnSubstitutionPartner? SubbedInFor,
    IReadOnlyList<EspnPlayerStat>? Stats,
    IReadOnlyList<EspnPlayerPlay>? Plays);

/// <param name="Abbreviation">"G", "CD-L", "LB", "DM", "AM-R", "F", ...; "SUB" for everyone on the bench.</param>
internal sealed record EspnPosition(string? Abbreviation);

internal sealed record EspnSubstitutionPartner(EspnAthlete? Athlete);

internal sealed record EspnPlayerStat(string? Name, double? Value);

internal sealed record EspnPlayerPlay(EspnClock? Clock, bool Substitution, bool RedCard);

// Team roster (/teams/{id}/roster).

internal sealed record EspnRosterResponse(EspnTeam? Team, IReadOnlyList<EspnRosterAthlete>? Athletes);

/// <param name="Position">Abbreviation is "G", "D", "M" or "F".</param>
/// <param name="Citizenship">Country name, e.g. "Türkiye".</param>
internal sealed record EspnRosterAthlete(
    string? Id,
    string? DisplayName,
    string? Jersey,
    EspnPosition? Position,
    int? Age,
    string? Citizenship);
