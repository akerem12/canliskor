using System.Text.Json.Serialization;

namespace CanliSkor.Infrastructure.Espn.Dtos;

// Mirrors only the parts of ESPN's match summary JSON (/summary?event={id}) that we use.
// Team and competitor shapes are shared with the scoreboard DTOs.

internal sealed record EspnSummaryResponse(
    EspnSummaryHeader? Header,
    EspnBoxscore? Boxscore,
    IReadOnlyList<EspnKeyEvent>? KeyEvents,
    IReadOnlyList<EspnRoster>? Rosters = null,
    IReadOnlyList<EspnOdds?>? Pickcenter = null,
    EspnGameInfo? GameInfo = null,
    IReadOnlyList<EspnSeries>? Seasonseries = null,
    IReadOnlyList<EspnCommentary>? Commentary = null);

/// <param name="Attendance">0 until the crowd is reported, which for most leagues is never.</param>
/// <param name="Officials">The referee, where ESPN knows one (few leagues).</param>
internal sealed record EspnGameInfo(EspnVenue? Venue, int? Attendance, IReadOnlyList<EspnOfficial>? Officials);

internal sealed record EspnOfficial(string? DisplayName, EspnOfficialPosition? Position);

/// <param name="Name">"Referee" for the one in the middle.</param>
internal sealed record EspnOfficialPosition(string? Name);

/// <param name="Type">"head-to-head": the two teams' latest meetings (five at most), newest first.</param>
internal sealed record EspnSeries(string? Type, IReadOnlyList<EspnSeriesEvent>? Events);

/// <param name="CompetitionName">With the season, e.g. "2025-26 English Premier League".</param>
internal sealed record EspnSeriesEvent(
    string? Id,
    string? Date,
    EspnSeriesStatus? StatusType,
    IReadOnlyList<EspnCompetitor>? Competitors,
    string? CompetitionName);

internal sealed record EspnSeriesStatus(bool Completed);

internal sealed record EspnSummaryHeader(string? Id, EspnLeague? League, IReadOnlyList<EspnSummaryCompetition>? Competitions);

internal sealed record EspnSummaryCompetition(string? Date, EspnStatus? Status, IReadOnlyList<EspnCompetitor>? Competitors);

internal sealed record EspnBoxscore(IReadOnlyList<EspnBoxscoreTeam>? Teams);

internal sealed record EspnBoxscoreTeam(string? HomeAway, IReadOnlyList<EspnStatistic>? Statistics);

internal sealed record EspnStatistic(string? Name, string? DisplayValue);

/// <param name="Participants">Goal: scorer, then assist. Substitution: player on, then player off.</param>
/// <param name="Id">The same for as long as the event exists, also when ESPN corrects its details.</param>
internal sealed record EspnKeyEvent(
    EspnKeyEventType? Type,
    EspnClock? Clock,
    bool ScoringPlay,
    EspnEventTeam? Team,
    IReadOnlyList<EspnParticipant>? Participants,
    string? Id = null);

/// <summary>One line of the written commentary; those that are a play (a shot, a foul) carry it.</summary>
internal sealed record EspnCommentary(EspnCommentaryPlay? Play);

/// <param name="Type">Among others "penalty---saved", "penalty---missed", "penalty---hit-woodwork".</param>
/// <param name="Team">Named only, without an id.</param>
/// <param name="Participants">The player who did it, e.g. took the penalty.</param>
internal sealed record EspnCommentaryPlay(
    string? Id,
    EspnKeyEventType? Type,
    EspnClock? Clock,
    EspnCommentaryTeam? Team,
    IReadOnlyList<EspnParticipant>? Participants);

internal sealed record EspnCommentaryTeam(string? DisplayName);

/// <param name="Type">Slug such as "goal", "goal---header", "penalty---scored", "own-goal", "yellow-card".</param>
internal sealed record EspnKeyEventType(string? Type);

/// <param name="Value">Seconds played, e.g. 785 for "14'"; added time counts as the end of the half (2700 for "45'+1'").</param>
internal sealed record EspnClock(string? DisplayValue, double? Value = null);

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

/// <param name="SubbedIn">ESPN sends true/false for most matches and {"didSub": false} for some; both are read.</param>
/// <param name="SubbedInFor">The player this substitute replaced.</param>
/// <param name="Plays">The player's cards, goals and substitutions, in match order.</param>
/// <param name="FormationPlace">"1" to "11" for starters, "1" being the goalkeeper; "0" or missing on the bench.</param>
internal sealed record EspnRosterEntry(
    bool Starter,
    string? Jersey,
    EspnAthlete? Athlete,
    EspnPosition? Position,
    [property: JsonConverter(typeof(EspnSubstitutionFlagConverter))] bool SubbedIn,
    [property: JsonConverter(typeof(EspnSubstitutionFlagConverter))] bool SubbedOut,
    EspnSubstitutionPartner? SubbedInFor,
    IReadOnlyList<EspnPlayerStat>? Stats,
    IReadOnlyList<EspnPlayerPlay>? Plays,
    string? FormationPlace = null);

/// <param name="Abbreviation">"G", "CD-L", "LB", "DM", "AM-R", "F", ...; "SUB" for everyone on the bench.</param>
internal sealed record EspnPosition(string? Abbreviation);

internal sealed record EspnSubstitutionPartner(EspnAthlete? Athlete);

internal sealed record EspnPlayerStat(string? Name, double? Value);

internal sealed record EspnPlayerPlay(EspnClock? Clock, bool Substitution, bool RedCard);

// Team roster (/teams/{id}/roster).

internal sealed record EspnRosterResponse(EspnTeam? Team, IReadOnlyList<EspnRosterAthlete>? Athletes);

/// <param name="Position">Abbreviation is "G", "D", "M" or "F".</param>
/// <param name="Citizenship">Country name, e.g. "Türkiye".</param>
/// <param name="Statistics">This season in the roster's league. Missing for players who haven't been in a match squad.</param>
internal sealed record EspnRosterAthlete(
    string? Id,
    string? DisplayName,
    string? Jersey,
    EspnPosition? Position,
    int? Age,
    string? Citizenship,
    EspnFlag? Flag = null,
    EspnRosterStatistics? Statistics = null);

internal sealed record EspnFlag(string? Href);

internal sealed record EspnRosterStatistics(EspnRosterSplits? Splits);

internal sealed record EspnRosterSplits(IReadOnlyList<EspnRosterStatCategory>? Categories);

/// <param name="Stats">Names such as "appearances", "totalGoals", "goalAssists".</param>
internal sealed record EspnRosterStatCategory(IReadOnlyList<EspnPlayerStat>? Stats);
