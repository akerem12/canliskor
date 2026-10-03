namespace CanliSkor.Core.Domain;

/// <param name="KickoffUtc">Always UTC. Conversion to Europe/Istanbul happens at the edges (API/UI).</param>
/// <param name="Clock">Display text for the match minute, e.g. "67'" or "90'+4'". Null before kickoff.</param>
/// <param name="Score">Null until the match has started.</param>
/// <param name="Venue">The stadium, e.g. "RAMS Park". Null if the provider doesn't say.</param>
/// <param name="Odds">The bookmaker's prices for the result. Null if none are posted.</param>
public sealed record Match(
    string Id,
    string LeagueCode,
    DateTimeOffset KickoffUtc,
    MatchStatus Status,
    string? Clock,
    Team HomeTeam,
    Team AwayTeam,
    Score? Score,
    string? Venue = null,
    MatchOdds? Odds = null);

/// <summary>Decimal odds for home win, draw and away win ("1X2"): what one unit staked pays back, e.g. 1.22.</summary>
/// <param name="Provider">The bookmaker, e.g. "DraftKings". Null if unknown.</param>
public sealed record MatchOdds(decimal Home, decimal Draw, decimal Away, string? Provider)
{
    /// <summary>The team with the shorter price. Null if both are priced the same.</summary>
    public TeamSide? Favorite => Home < Away ? TeamSide.Home : Away < Home ? TeamSide.Away : null;
}
