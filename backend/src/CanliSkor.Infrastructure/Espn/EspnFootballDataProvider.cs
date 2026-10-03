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

    // Standings live in another branch of ESPN's API than everything under the base address ("/apis/site/v2/...").
    private const string StandingsPath = "/apis/v2/sports/soccer/";

    private const string AllCompetitions = "all";

    /// <summary>More than any league plays in a month; ESPN's default would cut a busy month short.</summary>
    private const int MonthLimit = 300;

    /// <summary>More than any competition has teams (international friendlies: about 200).</summary>
    private const int TeamsLimit = 500;

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

    public async Task<LeagueStandings?> GetStandingsAsync(string leagueCode, CancellationToken cancellationToken = default)
    {
        var url = StandingsPath + $"{Uri.EscapeDataString(leagueCode)}/standings";
        var response = await GetOrNullAsync<EspnStandingsResponse>(url, $"standings for '{leagueCode}'", cancellationToken);

        return response is null ? null : EspnTeamMapper.MapStandings(response, leagueCode);
    }

    public async Task<LeagueTeams?> GetLeagueTeamsAsync(string leagueCode, CancellationToken cancellationToken = default)
    {
        var url = $"{Uri.EscapeDataString(leagueCode)}/teams?limit={TeamsLimit}";
        var response = await GetOrNullAsync<EspnTeamsResponse>(url, $"teams of '{leagueCode}'", cancellationToken);
        if (response is null)
        {
            return null;
        }

        var teams = (response.Sports?.FirstOrDefault()?.Leagues?.FirstOrDefault()?.Teams ?? [])
            .Where(t => !string.IsNullOrEmpty(t.Team?.Id) && !string.IsNullOrEmpty(t.Team.DisplayName))
            .Select(t => EspnScoreboardMapper.MapTeam(t.Team!))
            .ToList();

        return new LeagueTeams(leagueCode, teams);
    }

    public async Task<LeagueFixtures?> GetLeagueFixturesAsync(string leagueCode, DateOnly from, CancellationToken cancellationToken = default)
    {
        // ESPN rejects day ranges but takes a whole month ("202610"), so two requests cover this month and the next.
        var matches = new List<Match>();
        string? leagueName = null;
        foreach (var month in new[] { from, from.AddMonths(1) })
        {
            var url = $"{Uri.EscapeDataString(leagueCode)}/scoreboard?dates={month.ToString("yyyyMM", CultureInfo.InvariantCulture)}&limit={MonthLimit}";
            var response = await GetOrNullAsync<EspnScoreboardResponse>(url, $"fixtures for '{leagueCode}' in {month:yyyy-MM}", cancellationToken);
            if (response is null)
            {
                return null;
            }

            var scoreboard = EspnScoreboardMapper.Map(response, leagueCode, month);
            leagueName ??= scoreboard.League.Name;
            matches.AddRange(scoreboard.Matches);
        }

        return new LeagueFixtures(leagueCode, leagueName ?? leagueCode, matches.DistinctBy(m => m.Id).OrderBy(m => m.KickoffUtc).ToList());
    }

    public async Task<TeamProfile?> GetTeamProfileAsync(string leagueCode, string teamId, CancellationToken cancellationToken = default)
    {
        // Under a league's own code ESPN lists only that league's matches; "all" covers every competition
        // the team plays in (league, cups, Europe), which is what a team's page is about.
        var url = $"{AllCompetitions}/teams/{Uri.EscapeDataString(teamId)}/schedule";
        var what = $"schedule for team '{teamId}'";

        // Results and fixtures are two views of the same schedule. Sequential, like everything else we ask of ESPN.
        var results = await GetOrNullAsync<EspnScheduleResponse>(url, what, cancellationToken);
        if (results is null)
        {
            return null;
        }

        var fixtures = await GetOrNullAsync<EspnScheduleResponse>(url + "?fixture=true", what, cancellationToken);

        return EspnTeamMapper.MapProfile(results, fixtures ?? new EspnScheduleResponse(results.Team, []), leagueCode);
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
