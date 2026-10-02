using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Abstractions;

/// <summary>
/// Source of football data. Implementations translate an external API into our domain models;
/// nothing outside the implementation should know which API is behind it.
/// </summary>
public interface IFootballDataProvider
{
    /// <exception cref="FootballDataProviderException">The provider could not deliver data.</exception>
    Task<LeagueScoreboard> GetScoreboardAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Single failure type for all providers, so callers don't have to catch HTTP- or JSON-specific exceptions.
/// </summary>
public sealed class FootballDataProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);
