using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// Pure translation of ESPN's team schedule and standings to <see cref="TeamProfile"/> and
/// <see cref="LeagueStandings"/>. Malformed events and table rows are skipped rather than failing the whole thing.
/// </summary>
internal static class EspnTeamMapper
{
    /// <summary>Club crests follow one URL pattern; standings don't carry them, so the pattern fills in.</summary>
    private const string ClubLogoUrl = "https://a.espncdn.com/i/teamlogos/soccer/500/{0}.png";

    /// <summary>A venue has to host this many of the team's home matches to count as its stadium.</summary>
    private const int MinHomeMatchesAtStadium = 2;

    /// <param name="results">Played matches, newest first.</param>
    /// <param name="fixtures">Matches to come, soonest first.</param>
    /// <returns>Null if the schedule doesn't name the team.</returns>
    public static TeamProfile? MapProfile(EspnScheduleResponse results, EspnScheduleResponse fixtures, string leagueCode)
    {
        var espnTeam = results.Team ?? fixtures.Team;
        if (string.IsNullOrEmpty(espnTeam?.Id) || string.IsNullOrEmpty(espnTeam.DisplayName))
        {
            return null;
        }

        var team = new Team(
            espnTeam.Id,
            espnTeam.DisplayName,
            espnTeam.DisplayName,
            string.IsNullOrWhiteSpace(espnTeam.Logo) ? null : espnTeam.Logo);

        var played = MapMatches(results, leagueCode).Where(m => m.Status == MatchStatus.Finished).OrderByDescending(m => m.KickoffUtc).ToList();
        // A match being played right now is in neither list at ESPN's end; whatever is not over counts as upcoming.
        var upcoming = MapMatches(fixtures, leagueCode).Where(m => m.Status != MatchStatus.Finished).OrderBy(m => m.KickoffUtc).ToList();

        // National teams play their home matches all over the country.
        var stadium = espnTeam.IsNational ? null : FindStadium(espnTeam.Id, results, fixtures);

        return new TeamProfile(
            leagueCode,
            team,
            espnTeam.IsNational,
            string.IsNullOrWhiteSpace(espnTeam.StandingSummary) ? null : espnTeam.StandingSummary,
            stadium?.FullName,
            string.IsNullOrWhiteSpace(stadium?.Address?.City) ? null : stadium.Address.City,
            played,
            upcoming,
            MapCompetitions(results, fixtures));
    }

    /// <returns>Null if the response isn't a standings document.</returns>
    public static LeagueStandings? MapStandings(EspnStandingsResponse response, string leagueCode)
    {
        if (response.Name is null && response.Children is null)
        {
            return null;
        }

        var groups = (response.Children ?? [])
            .Select(group => new StandingsGroup(
                group.Name ?? string.Empty,
                (group.Standings?.Entries ?? []).Select(MapRow).OfType<StandingsRow>().OrderBy(r => r.Rank).ToList()))
            .Where(group => group.Rows.Count > 0)
            .ToList();

        return new LeagueStandings(leagueCode, response.Name ?? leagueCode, groups);
    }

    /// <summary>Every competition the schedules mention, each once, in the order they first appear.</summary>
    private static List<League> MapCompetitions(params EspnScheduleResponse[] schedules) =>
        schedules
            .SelectMany(s => s.Events ?? [])
            .Select(e => e.League)
            .Where(l => !string.IsNullOrEmpty(l?.Slug))
            .DistinctBy(l => l!.Slug)
            .Select(l => new League(l!.Slug!, l.Name ?? l.Abbreviation ?? l.Slug!))
            .ToList();

    private static IEnumerable<Match> MapMatches(EspnScheduleResponse schedule, string leagueCode) =>
        (schedule.Events ?? [])
            .Select(e =>
            {
                var competition = e.Competitions?.FirstOrDefault();
                var competitors = competition?.Competitors?
                    .Select(c => new EspnCompetitor(c.HomeAway, c.Score?.DisplayValue, c.Team))
                    .ToList();

                // Same shape as a scoreboard event once the score is flattened, so the same rules apply.
                return EspnScoreboardMapper.MapEvent(
                    new EspnEvent(e.Id, e.Date, competition?.Status, [new EspnCompetition(competitors)]),
                    e.League?.Slug ?? leagueCode);
            })
            .OfType<Match>();

    /// <summary>The venue of most of the team's home matches, if it hosts at least a couple of them.</summary>
    private static EspnVenue? FindStadium(string teamId, params EspnScheduleResponse[] schedules) =>
        schedules
            .SelectMany(s => s.Events ?? [])
            .Select(e => e.Competitions?.FirstOrDefault())
            .Where(c => c?.Venue?.FullName is { Length: > 0 }
                && c.Competitors?.Any(x => x.HomeAway == "home" && x.Team?.Id == teamId) == true)
            .GroupBy(c => c!.Venue!.FullName)
            .Where(g => g.Count() >= MinHomeMatchesAtStadium)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?
            .First()!
            .Venue;

    private static StandingsRow? MapRow(EspnStandingsEntry entry)
    {
        var team = entry.Team;
        if (string.IsNullOrEmpty(team?.Id) || string.IsNullOrEmpty(team.DisplayName))
        {
            return null;
        }

        int Stat(string name) => (int)Math.Round(entry.Stats?.FirstOrDefault(s => s.Name == name)?.Value ?? 0);

        return new StandingsRow(
            Stat("rank"),
            new Team(
                team.Id,
                team.DisplayName,
                team.ShortDisplayName ?? team.DisplayName,
                team.IsNational ? null : string.Format(System.Globalization.CultureInfo.InvariantCulture, ClubLogoUrl, team.Id)),
            Played: Stat("gamesPlayed"),
            Wins: Stat("wins"),
            Draws: Stat("ties"),
            Losses: Stat("losses"),
            GoalsFor: Stat("pointsFor"),
            GoalsAgainst: Stat("pointsAgainst"),
            GoalDifference: Stat("pointDifferential"),
            Points: Stat("points"),
            Note: string.IsNullOrWhiteSpace(entry.Note?.Description) ? null : entry.Note.Description,
            NoteColor: entry.Note?.Color is { Length: 7 } color && color[0] == '#' ? color.ToLowerInvariant() : null);
    }
}
