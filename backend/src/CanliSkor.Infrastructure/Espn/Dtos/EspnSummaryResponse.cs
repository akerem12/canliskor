namespace CanliSkor.Infrastructure.Espn.Dtos;

// Mirrors only the parts of ESPN's match summary JSON (/summary?event={id}) that we use.
// Team and competitor shapes are shared with the scoreboard DTOs.

internal sealed record EspnSummaryResponse(
    EspnSummaryHeader? Header,
    EspnBoxscore? Boxscore,
    IReadOnlyList<EspnKeyEvent>? KeyEvents);

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

internal sealed record EspnAthlete(string? DisplayName);
