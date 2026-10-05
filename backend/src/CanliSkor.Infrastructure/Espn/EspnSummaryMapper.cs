using System.Globalization;
using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// Pure translation of ESPN's match summary to <see cref="MatchDetail"/>. ESPN's key events also include
/// kickoff, half time, delays etc.; only goals, cards and substitutions are kept. Unknown or malformed events
/// and statistics are skipped rather than failing the whole match.
/// </summary>
internal static class EspnSummaryMapper
{
    // ESPN statistic name → ours. Other statistics (passes, tackles, ...) aren't shown.
    // Before kickoff ESPN sends season totals here instead (goal difference etc.), which don't match any name.
    private static readonly Dictionary<string, MatchStatType> StatNames = new()
    {
        ["possessionPct"] = MatchStatType.Possession,
        ["totalShots"] = MatchStatType.Shots,
        ["shotsOnTarget"] = MatchStatType.ShotsOnTarget,
        ["wonCorners"] = MatchStatType.Corners,
        ["foulsCommitted"] = MatchStatType.Fouls,
        ["offsides"] = MatchStatType.Offsides,
        ["yellowCards"] = MatchStatType.YellowCards,
        ["redCards"] = MatchStatType.RedCards,
        ["saves"] = MatchStatType.Saves,
    };

    /// <returns>Null if the summary has no usable match header, or the match belongs to another league.</returns>
    public static MatchDetail? Map(EspnSummaryResponse response, string leagueCode)
    {
        // ESPN serves any match under any league's URL; the header names the league it really belongs to.
        var competition = response.Header?.Competitions?.FirstOrDefault();
        if (competition is null || response.Header!.League?.Slug != leagueCode)
        {
            return null;
        }

        var match = EspnScoreboardMapper.MapEvent(
            new EspnEvent(response.Header!.Id, competition.Date, competition.Status, [new EspnCompetition(competition.Competitors)]),
            leagueCode);
        if (match is null)
        {
            return null;
        }

        // The header carries no odds; the summary has them in a section of its own.
        match = match with { Odds = EspnOddsMapper.Map(response.Pickcenter) };

        var events = (response.KeyEvents ?? [])
            .Select(e => MapEvent(e, match))
            .OfType<MatchEvent>()
            .ToList();

        return new MatchDetail(
            match,
            events,
            MapStats(response.Boxscore),
            EspnLineupMapper.Map(response.Rosters),
            MapInfo(response.GameInfo),
            MapPreviousMeetings(response.Seasonseries, match));
    }

    private static MatchInfo? MapInfo(EspnGameInfo? gameInfo)
    {
        var officials = gameInfo?.Officials ?? [];
        // The referee is marked as such; a lone unmarked official is taken to be the referee too.
        var referee = officials.FirstOrDefault(o => string.Equals(o.Position?.Name, "Referee", StringComparison.OrdinalIgnoreCase))
            ?? (officials.Count == 1 && officials[0].Position?.Name is null ? officials[0] : null);

        var info = new MatchInfo(
            Venue: Text(gameInfo?.Venue?.FullName),
            City: Text(gameInfo?.Venue?.Address?.City),
            Country: Text(gameInfo?.Venue?.Address?.Country),
            Referee: Text(referee?.DisplayName),
            // ESPN sends 0 where it has no figure.
            Attendance: gameInfo?.Attendance > 0 ? gameInfo.Attendance : null);

        return info == new MatchInfo(null, null, null, null, null) ? null : info;
    }

    private static List<PreviousMeeting> MapPreviousMeetings(IReadOnlyList<EspnSeries>? series, Match match)
    {
        var events = series?.FirstOrDefault(s => s.Type == "head-to-head")?.Events ?? [];

        return events
            // Only what has been played, and never the match itself.
            .Where(e => e.StatusType?.Completed == true && e.Id != match.Id)
            .Select(e => (Event: e, Played: EspnScoreboardMapper.MapEvent(
                new EspnEvent(e.Id, e.Date, new EspnStatus(null, new EspnStatusType(null, "post")), [new EspnCompetition(e.Competitors)]),
                match.LeagueCode)))
            .Where(x => x.Played?.Score is not null)
            .Select(x => new PreviousMeeting(
                x.Played!.Id, x.Played.KickoffUtc, Text(x.Event.CompetitionName), AsToday(x.Played.HomeTeam), AsToday(x.Played.AwayTeam), x.Played.Score!))
            .OrderByDescending(m => m.KickoffUtc)
            .ToList();

        // The series names its teams more sparsely (no short name); today's match has the same two in full.
        Team AsToday(Team team) => team.Id == match.HomeTeam.Id ? match.HomeTeam : team.Id == match.AwayTeam.Id ? match.AwayTeam : team;
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MatchEvent? MapEvent(EspnKeyEvent e, Match match)
    {
        var slug = e.Type?.Type;
        MatchEventType? type = slug switch
        {
            "own-goal" => MatchEventType.OwnGoal,
            "penalty---scored" => MatchEventType.PenaltyGoal,
            // "goal", "goal---header", "goal---free-kick", "goal---volley", ...
            _ when e.ScoringPlay && slug?.StartsWith("goal", StringComparison.Ordinal) == true => MatchEventType.Goal,
            "yellow-card" => MatchEventType.YellowCard,
            "red-card" => MatchEventType.RedCard,
            "substitution" => MatchEventType.Substitution,
            _ => null,
        };

        // ESPN credits an own goal to the team that benefits, so the side always matches the score.
        TeamSide? side = e.Team?.Id switch
        {
            null => null,
            var id when id == match.HomeTeam.Id => TeamSide.Home,
            var id when id == match.AwayTeam.Id => TeamSide.Away,
            _ => null,
        };

        if (type is null || side is null || string.IsNullOrEmpty(e.Clock?.DisplayValue))
        {
            return null;
        }

        var player = e.Participants?.ElementAtOrDefault(0)?.Athlete;
        var related = e.Participants?.ElementAtOrDefault(1)?.Athlete;

        return new MatchEvent(
            type.Value,
            e.Clock.DisplayValue,
            side.Value,
            Player: player?.DisplayName,
            RelatedPlayer: related?.DisplayName,
            PlayerId: string.IsNullOrEmpty(player?.Id) ? null : player.Id,
            RelatedPlayerId: string.IsNullOrEmpty(related?.Id) ? null : related.Id);
    }

    private static List<MatchStat> MapStats(EspnBoxscore? boxscore)
    {
        var home = StatValues(boxscore?.Teams?.FirstOrDefault(t => t.HomeAway == "home"));
        var away = StatValues(boxscore?.Teams?.FirstOrDefault(t => t.HomeAway == "away"));

        return Enum.GetValues<MatchStatType>()
            .Where(type => home.ContainsKey(type) && away.ContainsKey(type))
            .Select(type => new MatchStat(type, home[type], away[type]))
            .ToList();
    }

    private static Dictionary<MatchStatType, double> StatValues(EspnBoxscoreTeam? team)
    {
        var values = new Dictionary<MatchStatType, double>();
        foreach (var stat in team?.Statistics ?? [])
        {
            if (stat.Name is not null
                && StatNames.TryGetValue(stat.Name, out var type)
                && double.TryParse(stat.DisplayValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                values[type] = value;
            }
        }

        return values;
    }
}
