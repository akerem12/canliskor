using System.Globalization;
using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// Pure translation from ESPN's JSON shape to our domain. No I/O, so it is fully unit-testable.
/// Malformed events are skipped rather than failing the whole scoreboard.
/// </summary>
internal static class EspnScoreboardMapper
{
    public static LeagueScoreboard Map(EspnScoreboardResponse response, string leagueCode, DateOnly date)
    {
        var espnLeague = response.Leagues?.FirstOrDefault();
        var league = new League(leagueCode, espnLeague?.Abbreviation ?? espnLeague?.Name ?? leagueCode);

        var matches = (response.Events ?? [])
            .Select(e => MapEvent(e, leagueCode))
            .OfType<Match>()
            .OrderBy(m => m.KickoffUtc)
            .ToList();

        return new LeagueScoreboard(league, date, matches);
    }

    internal static Match? MapEvent(EspnEvent e, string leagueCode)
    {
        var competition = e.Competitions?.FirstOrDefault();
        var competitors = competition?.Competitors;
        var home = competitors?.FirstOrDefault(c => c.HomeAway == "home");
        var away = competitors?.FirstOrDefault(c => c.HomeAway == "away");

        if (e.Id is null || home?.Team is null || away?.Team is null || !TryParseKickoff(e.Date, out var kickoff))
        {
            return null;
        }

        var status = MapStatus(e.Status?.Type);
        var started = status is not (MatchStatus.Scheduled or MatchStatus.Postponed or MatchStatus.Cancelled);

        return new Match(
            Id: e.Id,
            LeagueCode: leagueCode,
            KickoffUtc: kickoff,
            Status: status,
            // ESPN reports "0'" and "0" scores before kickoff; we expose "not started" as null instead.
            Clock: started ? e.Status?.DisplayClock : null,
            HomeTeam: MapTeam(home.Team),
            AwayTeam: MapTeam(away.Team),
            Score: started ? ParseScore(home.Score, away.Score) : null,
            Venue: string.IsNullOrWhiteSpace(competition?.Venue?.FullName) ? null : competition.Venue.FullName);
    }

    internal static MatchStatus MapStatus(EspnStatusType? type)
    {
        // Specific status names first; fall back to the coarse pre/in/post state
        // so an unknown new status name still lands in a sensible bucket.
        switch (type?.Name)
        {
            case "STATUS_HALFTIME":
                return MatchStatus.HalfTime;
            case "STATUS_POSTPONED":
                return MatchStatus.Postponed;
            case "STATUS_CANCELED":
            case "STATUS_ABANDONED":
                return MatchStatus.Cancelled;
        }

        return type?.State switch
        {
            "in" => MatchStatus.Live,
            "post" => MatchStatus.Finished,
            _ => MatchStatus.Scheduled,
        };
    }

    private static Team MapTeam(EspnTeam team)
    {
        var logo = team.Logo ?? team.Logos?.FirstOrDefault()?.Href;
        return new(
            Id: team.Id ?? string.Empty,
            Name: team.DisplayName ?? string.Empty,
            ShortName: team.ShortDisplayName ?? team.DisplayName ?? string.Empty,
            // Some newly promoted teams come with logo: "" — normalize to null so clients can show a fallback.
            LogoUrl: string.IsNullOrWhiteSpace(logo) ? null : logo);
    }

    private static Score? ParseScore(string? home, string? away) =>
        int.TryParse(home, CultureInfo.InvariantCulture, out var h) && int.TryParse(away, CultureInfo.InvariantCulture, out var a)
            ? new Score(h, a)
            : null;

    // ESPN sends "2026-09-20T14:00Z" (no seconds), so we parse explicitly instead of relying on the JSON serializer.
    private static bool TryParseKickoff(string? value, out DateTimeOffset kickoff) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out kickoff);
}
