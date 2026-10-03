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

internal sealed record EspnCompetition(IReadOnlyList<EspnCompetitor>? Competitors, EspnVenue? Venue = null, IReadOnlyList<EspnOdds?>? Odds = null);

/// <summary>One bookmaker's lines. The same shape on scoreboards ("odds") and in match summaries ("pickcenter").</summary>
/// <param name="Moneyline">The result prices as American odds, opening and closing (= current).</param>
/// <param name="HomeTeamOdds">Summaries only: the same prices as numbers. Used when <paramref name="Moneyline"/> is missing.</param>
internal sealed record EspnOdds(
    EspnOddsProvider? Provider,
    EspnMoneyline? Moneyline,
    EspnTeamOdds? HomeTeamOdds = null,
    EspnTeamOdds? AwayTeamOdds = null,
    EspnTeamOdds? DrawOdds = null);

internal sealed record EspnOddsProvider(string? Name);

internal sealed record EspnMoneyline(EspnMoneylinePrices? Home, EspnMoneylinePrices? Away, EspnMoneylinePrices? Draw);

internal sealed record EspnMoneylinePrices(EspnMoneylinePrice? Open, EspnMoneylinePrice? Close);

/// <param name="Odds">American odds as text: "-450", "+950", or "EVEN".</param>
internal sealed record EspnMoneylinePrice(string? Odds);

internal sealed record EspnTeamOdds(double? MoneyLine);

internal sealed record EspnCompetitor(string? HomeAway, string? Score, EspnTeam? Team);

/// <param name="Logo">Scoreboard responses carry a single logo URL.</param>
/// <param name="Logos">Match summaries carry a list instead (default first, then dark variants).</param>
internal sealed record EspnTeam(string? Id, string? DisplayName, string? ShortDisplayName, string? Logo, IReadOnlyList<EspnLogo>? Logos = null);

internal sealed record EspnLogo(string? Href);
