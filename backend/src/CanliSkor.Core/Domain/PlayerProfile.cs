namespace CanliSkor.Core.Domain;

/// <summary>Who a player is, and what they have done this season in each competition.</summary>
/// <param name="Position">Null if the provider doesn't say.</param>
/// <param name="Nationality">Country name, e.g. "Germany". Null if unknown.</param>
/// <param name="FlagUrl">Picture of that country's flag. Null if unknown.</param>
/// <param name="HeightCm">Null if unknown.</param>
/// <param name="PhotoUrl">Portrait. Null if the provider has none, which is the case for most players.</param>
/// <param name="Team">The club the player is registered with. Null if unknown.</param>
/// <param name="Competitions">This season's competitions with at least one statistic, the player's main league first.</param>
public sealed record PlayerProfile(
    string Id,
    string Name,
    string? Jersey,
    PlayerPosition? Position,
    string? Nationality,
    string? FlagUrl,
    int? Age,
    int? HeightCm,
    string? PhotoUrl,
    Team? Team,
    IReadOnlyList<PlayerCompetitionStats> Competitions);

/// <summary>One player's season in one competition. All counts.</summary>
/// <param name="Name">E.g. "2026-27 Turkish Super Lig".</param>
/// <param name="LeagueCode">The competition's code, e.g. "tur.1". Null if the provider doesn't say.</param>
/// <param name="TeamName">The team played for there: the club, or the national team. Null if unknown.</param>
/// <param name="SubstituteAppearances">Known for the player's main league only; null elsewhere.</param>
/// <param name="CleanSheets">Goalkeepers only, like <paramref name="Saves"/> and <paramref name="GoalsConceded"/>; null for everyone else.</param>
public sealed record PlayerCompetitionStats(
    string Name,
    string? LeagueCode,
    string? TeamName,
    int Starts,
    int? SubstituteAppearances,
    int Goals,
    int Assists,
    int Shots,
    int ShotsOnTarget,
    int YellowCards,
    int RedCards,
    int FoulsCommitted,
    int FoulsSuffered,
    int Offsides,
    int? CleanSheets,
    int? Saves,
    int? GoalsConceded);
