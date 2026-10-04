namespace CanliSkor.Core.Domain;

/// <summary>A team's registered players for the current season of one league.</summary>
/// <param name="Players">Goalkeepers first, then defenders, midfielders and forwards; by shirt number within each.</param>
public sealed record Squad(string LeagueCode, string TeamId, string TeamName, IReadOnlyList<SquadPlayer> Players);

/// <param name="Position">Null if the provider doesn't say.</param>
/// <param name="Nationality">Country name, e.g. "Türkiye". Null if unknown.</param>
/// <param name="FlagUrl">That country's flag. Null if unknown.</param>
/// <param name="Season">Null if the provider has no numbers for the player this season.</param>
public sealed record SquadPlayer(
    string Id,
    string Name,
    string? Jersey,
    PlayerPosition? Position,
    int? Age,
    string? Nationality,
    string? FlagUrl = null,
    SquadPlayerSeason? Season = null);

/// <summary>A player's season so far in the league the squad was asked for.</summary>
/// <param name="Appearances">Matches played in, from the start or as a substitute.</param>
public sealed record SquadPlayerSeason(int Appearances, int Goals, int Assists);

/// <summary>A squad plus when we fetched it.</summary>
public sealed record SquadSnapshot(Squad Squad, DateTimeOffset FetchedAtUtc);
