using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// The only class that talks to ESPN. The injected HttpClient is a typed client from IHttpClientFactory,
/// with base address and resilience (retry, timeout, circuit breaker) configured in DependencyInjection.
/// </summary>
internal sealed class EspnFootballDataProvider(HttpClient httpClient) : IFootballDataProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LeagueScoreboard> GetScoreboardAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default)
    {
        var url = $"{Uri.EscapeDataString(leagueCode)}/scoreboard?dates={date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";

        EspnScoreboardResponse? response;
        try
        {
            response = await httpClient.GetFromJsonAsync<EspnScoreboardResponse>(url, JsonOptions, cancellationToken);
        }
        // A TaskCanceledException without our token being cancelled means the HTTP timeout fired.
        catch (Exception ex) when ((ex is HttpRequestException or JsonException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            throw new FootballDataProviderException($"Failed to fetch ESPN scoreboard for '{leagueCode}' on {date:yyyy-MM-dd}.", ex);
        }

        if (response is null)
        {
            throw new FootballDataProviderException($"ESPN returned an empty scoreboard for '{leagueCode}' on {date:yyyy-MM-dd}.");
        }

        return EspnScoreboardMapper.Map(response, leagueCode, date);
    }
}
