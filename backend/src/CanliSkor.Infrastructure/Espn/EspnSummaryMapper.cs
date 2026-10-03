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

        var events = (response.KeyEvents ?? [])
            .Select(e => MapEvent(e, match))
            .OfType<MatchEvent>()
            .ToList();

        return new MatchDetail(match, events, MapStats(response.Boxscore));
    }

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

        return new MatchEvent(
            type.Value,
            e.Clock.DisplayValue,
            side.Value,
            Player: e.Participants?.ElementAtOrDefault(0)?.Athlete?.DisplayName,
            RelatedPlayer: e.Participants?.ElementAtOrDefault(1)?.Athlete?.DisplayName);
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
