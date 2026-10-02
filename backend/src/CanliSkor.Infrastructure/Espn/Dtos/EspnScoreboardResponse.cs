namespace CanliSkor.Infrastructure.Espn.Dtos;

// Mirrors only the parts of ESPN's scoreboard JSON that we actually use.
// Everything is nullable: the API is undocumented, so we validate in the mapper instead of trusting the shape.

internal sealed record EspnScoreboardResponse(
    IReadOnlyList<EspnLeague>? Leagues,
    IReadOnlyList<EspnEvent>? Events);

internal sealed record EspnLeague(string? Name, string? Abbreviation, string? Slug);

internal sealed record EspnEvent(
    string? Id,
    string? Date,
    EspnStatus? Status,
    IReadOnlyList<EspnCompetition>? Competitions);

internal sealed record EspnStatus(string? DisplayClock, EspnStatusType? Type);

internal sealed record EspnStatusType(string? Name, string? State);

internal sealed record EspnCompetition(IReadOnlyList<EspnCompetitor>? Competitors);

internal sealed record EspnCompetitor(string? HomeAway, string? Score, EspnTeam? Team);

internal sealed record EspnTeam(string? Id, string? DisplayName, string? ShortDisplayName, string? Logo);
