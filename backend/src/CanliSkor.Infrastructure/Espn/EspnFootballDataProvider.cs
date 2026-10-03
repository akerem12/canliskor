using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Time;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// The only class that talks to ESPN. The injected HttpClient is a typed client from IHttpClientFactory,
/// with base address and resilience (retry, timeout, circuit breaker) configured in DependencyInjection.
/// </summary>
internal sealed class EspnFootballDataProvider(HttpClient httpClient) : IFootballDataProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// ESPN files matches under their US Eastern date, so a 02:00 Istanbul kickoff (e.g. an evening game in Brazil)
    /// is listed under the previous day. An Istanbul day always lies within the previous and the same Eastern day,
    /// so we fetch both and keep the matches whose kickoff falls on the requested Istanbul date.
    /// </summary>
    public async Task<LeagueScoreboard> GetScoreboardAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default)
    {
        // Sequential on purpose, like the poller: gentle on an unofficial API.
        var previousDay = await FetchEspnDayAsync(leagueCode, date.AddDays(-1), date, cancellationToken);
        var sameDay = await FetchEspnDayAsync(leagueCode, date, date, cancellationToken);

        var matches = previousDay.Matches.Concat(sameDay.Matches)
            .Where(m => IstanbulTime.DateOf(m.KickoffUtc) == date)
            .DistinctBy(m => m.Id)
            .OrderBy(m => m.KickoffUtc)
            .ToList();

        return sameDay with { Matches = matches };
    }

    public async Task<MatchDetail?> GetMatchDetailAsync(string leagueCode, string matchId, CancellationToken cancellationToken = default)
    {
        var url = $"{Uri.EscapeDataString(leagueCode)}/summary?event={Uri.EscapeDataString(matchId)}";
        var response = await GetOrNullAsync<EspnSummaryResponse>(url, $"summary for match '{matchId}' in '{leagueCode}'", cancellationToken);

        return response is null ? null : EspnSummaryMapper.Map(response, leagueCode);
    }

    public async Task<Squad?> GetSquadAsync(string leagueCode, string teamId, CancellationToken cancellationToken = default)
    {
        // ESPN only serves a team under a league it plays in.
        var url = $"{Uri.EscapeDataString(leagueCode)}/teams/{Uri.EscapeDataString(teamId)}/roster";
        var response = await GetOrNullAsync<EspnRosterResponse>(url, $"roster for team '{teamId}' in '{leagueCode}'", cancellationToken);

        return response is null ? null : EspnRosterMapper.Map(response, leagueCode);
    }

    /// <returns>Null if ESPN doesn't know the resource: it answers an unknown id with 404 (and some malformed ids with 400).</returns>
    private async Task<T?> GetOrNullAsync<T>(string url, string what, CancellationToken cancellationToken)
        where T : class
    {
        T? response;
        try
        {
            using var httpResponse = await httpClient.GetAsync(url, cancellationToken);
            if (httpResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                return null;
            }

            httpResponse.EnsureSuccessStatusCode();
            response = await httpResponse.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException or JsonException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            throw new FootballDataProviderException($"Failed to fetch ESPN {what}.", ex);
        }

        return response ?? throw new FootballDataProviderException($"ESPN returned an empty {what}.");
    }

    /// <param name="espnDate">The date in ESPN's (US Eastern) calendar.</param>
    /// <param name="date">The Istanbul date being assembled.</param>
    private async Task<LeagueScoreboard> FetchEspnDayAsync(string leagueCode, DateOnly espnDate, DateOnly date, CancellationToken cancellationToken)
    {
        var url = $"{Uri.EscapeDataString(leagueCode)}/scoreboard?dates={espnDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";

        EspnScoreboardResponse? response;
        try
        {
            response = await httpClient.GetFromJsonAsync<EspnScoreboardResponse>(url, JsonOptions, cancellationToken);
        }
        // A TaskCanceledException without our token being cancelled means the HTTP timeout fired.
        catch (Exception ex) when ((ex is HttpRequestException or JsonException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            throw new FootballDataProviderException($"Failed to fetch ESPN scoreboard for '{leagueCode}' on ESPN date {espnDate:yyyy-MM-dd}.", ex);
        }

        if (response is null)
        {
            throw new FootballDataProviderException($"ESPN returned an empty scoreboard for '{leagueCode}' on ESPN date {espnDate:yyyy-MM-dd}.");
        }

        return EspnScoreboardMapper.Map(response, leagueCode, date);
    }
}
