namespace CanliSkor.Core.Domain;

/// <summary>A team's registered players for the current season of one league.</summary>
/// <param name="Players">Goalkeepers first, then defenders, midfielders and forwards; by shirt number within each.</param>
public sealed record Squad(string LeagueCode, string TeamId, string TeamName, IReadOnlyList<SquadPlayer> Players);

/// <param name="Position">Null if the provider doesn't say.</param>
/// <param name="Nationality">Country name, e.g. "Türkiye". Null if unknown.</param>
public sealed record SquadPlayer(string Id, string Name, string? Jersey, PlayerPosition? Position, int? Age, string? Nationality);

/// <summary>A squad plus when we fetched it.</summary>
public sealed record SquadSnapshot(Squad Squad, DateTimeOffset FetchedAtUtc);
