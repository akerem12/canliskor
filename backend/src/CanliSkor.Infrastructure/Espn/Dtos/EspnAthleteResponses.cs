namespace CanliSkor.Infrastructure.Espn.Dtos;

// A player's page: /apis/common/v3/sports/soccer/{league}/athletes/{id} and .../overview.

internal sealed record EspnAthleteResponse(EspnAthleteProfile? Athlete);

/// <param name="Position">Abbreviation is "G", "D", "M" or "F".</param>
/// <param name="DisplayHeight">Feet and inches, e.g. "6' 0\"".</param>
/// <param name="StatsSummary">The season in the player's main league.</param>
internal sealed record EspnAthleteProfile(
    string? Id,
    string? DisplayName,
    string? Jersey,
    EspnPosition? Position,
    EspnTeam? Team,
    string? DisplayHeight,
    int? Age,
    string? Citizenship,
    EspnLogo? Flag,
    EspnStatsSummary? StatsSummary);

/// <param name="DisplayName">The competition's name followed by " Stats", e.g. "2026-27 Turkish Super Lig Stats".</param>
internal sealed record EspnStatsSummary(string? DisplayName, IReadOnlyList<EspnSummaryStatistic>? Statistics);

/// <param name="Name">"starts-subIns" is the one we read; its display value is e.g. "3 (3)": starts, then substitute appearances.</param>
internal sealed record EspnSummaryStatistic(string? Name, string? DisplayValue);

internal sealed record EspnAthleteOverviewResponse(EspnAthleteStatistics? Statistics);

/// <param name="Names">What each position in a split's <c>Stats</c> means: "starts", "totalGoals", ... Missing if the player has no statistics.</param>
/// <param name="Filters">The one named "team" lists the teams the splits refer to.</param>
internal sealed record EspnAthleteStatistics(
    IReadOnlyList<string>? Names,
    IReadOnlyList<EspnStatisticsFilter>? Filters,
    IReadOnlyList<EspnAthleteSplit>? Splits);

internal sealed record EspnStatisticsFilter(string? Name, IReadOnlyList<EspnFilterOption>? Options);

internal sealed record EspnFilterOption(string? Value, string? DisplayValue);

/// <param name="DisplayName">E.g. "2026-27 Champions League".</param>
internal sealed record EspnAthleteSplit(string? DisplayName, string? TeamId, string? LeagueSlug, IReadOnlyList<string>? Stats);
