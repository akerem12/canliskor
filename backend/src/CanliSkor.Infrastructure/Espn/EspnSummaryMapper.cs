using System.Globalization;
using System.Text;
using Regex = System.Text.RegularExpressions.Regex;
using GeneratedRegexAttribute = System.Text.RegularExpressions.GeneratedRegexAttribute;
using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// Pure translation of ESPN's match summary to <see cref="MatchDetail"/>. ESPN's key events also include
/// kickoff, half time, delays etc.; only goals, cards and substitutions are kept. Unknown or malformed events
/// and statistics are skipped rather than failing the whole match.
/// </summary>
internal static partial class EspnSummaryMapper
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

        var events = MergeInOrder(
            (response.KeyEvents ?? []).Select(e => (Event: MapEvent(e, match), e.Clock?.Value)),
            (response.Commentary ?? []).Select(c => (Event: MapMissedPenalty(c.Play, match) ?? MapPenaltyAwarded(c, match), c.Play?.Clock?.Value ?? c.Time?.Value)));

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

    /// <summary>
    /// The key events in ESPN's order, with the penalties given and missed (only found in the commentary) slotted
    /// in by the clock: after every key event of the same second or earlier.
    /// </summary>
    private static List<MatchEvent> MergeInOrder(
        IEnumerable<(MatchEvent? Event, double? Seconds)> keyEvents,
        IEnumerable<(MatchEvent? Event, double? Seconds)> missedPenalties)
    {
        var merged = keyEvents.Where(e => e.Event is not null).ToList();
        foreach (var missed in missedPenalties.Where(e => e.Event is not null))
        {
            var at = merged.FindIndex(e => e.Seconds > missed.Seconds);
            merged.Insert(at < 0 ? merged.Count : at, missed);
        }

        return [.. merged.Select(e => e.Event!)];
    }

    /// <summary>
    /// "Penalty conceded by Dean Huijsen (Real Madrid) after a foul in the penalty area." ESPN has no event type for
    /// a penalty given, but writes this line for every one, also when VAR gives it; a penalty VAR takes back gets
    /// none. The team in brackets gave it away, so the other one gets it.
    /// </summary>
    private static MatchEvent? MapPenaltyAwarded(EspnCommentary line, Match match)
    {
        var text = PenaltyConceded.Match(line.Text ?? "");
        if (!text.Success || string.IsNullOrEmpty(line.Time?.DisplayValue))
        {
            return null;
        }

        // The foul it comes with names the team exactly; a handball comes without one, and the text's spelling
        // of the team can differ from the header's ("Atletico Madrid", "Racing de Santander").
        var conceding = SideOf(line.Play?.Team?.DisplayName, match) ?? LooseSideOf(text.Groups["team"].Value, match);
        if (conceding is null)
        {
            return null;
        }

        return new MatchEvent(
            MatchEventType.PenaltyAwarded,
            line.Time.DisplayValue,
            conceding == TeamSide.Home ? TeamSide.Away : TeamSide.Home,
            Player: null,
            RelatedPlayer: text.Groups["player"].Value,
            Id: !string.IsNullOrEmpty(line.Play?.Id) ? line.Play.Id : line.Sequence is { } sequence ? $"commentary:{sequence}" : null);
    }

    [GeneratedRegex(@"^Penalty conceded by (?<player>.+?) \((?<team>[^)]+)\)")]
    private static partial Regex PenaltyConcededPattern();

    private static readonly Regex PenaltyConceded = PenaltyConcededPattern();

    private static TeamSide? SideOf(string? teamName, Match match) => teamName switch
    {
        null => null,
        var name when name == match.HomeTeam.Name => TeamSide.Home,
        var name when name == match.AwayTeam.Name => TeamSide.Away,
        _ => null,
    };

    /// <summary>The side whose name or short name matches, ignoring accents, case and spaces; null unless exactly one does.</summary>
    private static TeamSide? LooseSideOf(string teamName, Match match)
    {
        var name = Plain(teamName);
        bool Matches(Team team) => new[] { Plain(team.Name), Plain(team.ShortName) }
            .Any(n => n.Length >= 4 && (n == name || name.Contains(n, StringComparison.Ordinal) || n.Contains(name, StringComparison.Ordinal)));

        return (Matches(match.HomeTeam), Matches(match.AwayTeam)) switch
        {
            (true, false) => TeamSide.Home,
            (false, true) => TeamSide.Away,
            _ => null,
        };
    }

    /// <summary>"Atlético Madrid" → "atleticomadrid".</summary>
    private static string Plain(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant));

    private static MatchEvent? MapMissedPenalty(EspnCommentaryPlay? play, Match match)
    {
        if (play?.Type?.Type is not ("penalty---saved" or "penalty---missed" or "penalty---hit-woodwork")
            || string.IsNullOrEmpty(play.Clock?.DisplayValue))
        {
            return null;
        }

        // The commentary names the team but gives no id.
        var side = SideOf(play.Team?.DisplayName, match);
        if (side is null)
        {
            return null;
        }

        var taker = play.Participants?.ElementAtOrDefault(0)?.Athlete;
        return new MatchEvent(
            MatchEventType.PenaltyMissed,
            play.Clock.DisplayValue,
            side.Value,
            Player: taker?.DisplayName,
            RelatedPlayer: null,
            PlayerId: string.IsNullOrEmpty(taker?.Id) ? null : taker.Id,
            Id: string.IsNullOrEmpty(play.Id) ? null : play.Id);
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

        var player = e.Participants?.ElementAtOrDefault(0)?.Athlete;
        var related = e.Participants?.ElementAtOrDefault(1)?.Athlete;

        return new MatchEvent(
            type.Value,
            e.Clock.DisplayValue,
            side.Value,
            Player: player?.DisplayName,
            RelatedPlayer: related?.DisplayName,
            PlayerId: string.IsNullOrEmpty(player?.Id) ? null : player.Id,
            RelatedPlayerId: string.IsNullOrEmpty(related?.Id) ? null : related.Id,
            Id: string.IsNullOrEmpty(e.Id) ? null : e.Id);
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
