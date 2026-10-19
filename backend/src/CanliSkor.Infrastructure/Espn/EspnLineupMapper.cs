using System.Globalization;
using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// Pure translation of ESPN's rosters to <see cref="MatchLineups"/>. ESPN gives each starter a position
/// ("CD-L", "AM-R", ...) and the team a formation ("4-2-3-1") but not who stands where, so the rows are
/// worked out here: starters are ordered from defence to attack, cut into rows of the formation's sizes,
/// and each row is ordered left to right. Around kick-off ESPN may send the starters without their positions;
/// their formation places then give them (see <see cref="PlacePositions"/>).
/// </summary>
internal static class EspnLineupMapper
{
    private const string Goalkeeper = "G";

    // How far up the pitch a position is. Rows are filled in this order.
    private static readonly Dictionary<string, double> Depths = new()
    {
        ["CD"] = 1, ["CD-L"] = 1, ["CD-R"] = 1, ["LB"] = 1, ["RB"] = 1,
        ["SW"] = 1.5,
        ["DM"] = 2,
        ["CM"] = 3, ["CM-L"] = 3, ["CM-R"] = 3, ["LM"] = 3, ["RM"] = 3,
        ["AM"] = 4, ["AM-L"] = 4, ["AM-R"] = 4,
        ["CF-L"] = 4.5, ["CF-R"] = 4.5, ["LCF"] = 4.5, ["RCF"] = 4.5, ["LF"] = 4.5, ["RF"] = 4.5,
        ["F"] = 5, ["CF"] = 5,
    };

    // A position ESPN adds later lands in midfield, in the centre, rather than breaking the line-up.
    private const double UnknownDepth = 3;

    /// <summary>
    /// The position ESPN gives each formation place ("1" to "11", in that order) in each formation. Learnt from 230
    /// team line-ups across 15 competitions in October 2026: within a formation a place had the same position every
    /// time but once (the two holding midfielders of one 4-2-3-1 the other way round).
    /// </summary>
    private static readonly Dictionary<string, string[]> PlacePositions = new()
    {
        ["4-2-3-1"] = ["G", "RB", "LB", "LM", "CD-R", "CD-L", "AM-R", "RM", "F", "AM", "AM-L"],
        ["4-3-3"] = ["G", "RB", "LB", "CM", "CD-R", "CD-L", "RM", "LM", "F", "RF", "LF"],
        ["4-4-2"] = ["G", "RB", "LB", "CM-R", "CD-R", "CD-L", "RM", "CM-L", "CF-L", "CF-R", "LM"],
        ["4-1-4-1"] = ["G", "RB", "LB", "DM", "CD-R", "CD-L", "RM", "CM-R", "F", "CM-L", "LM"],
        ["4-5-1"] = ["G", "RB", "LB", "CM-R", "CD-R", "CD-L", "RM", "CM-L", "F", "AM", "LM"],
        ["4-4-1-1"] = ["G", "RB", "LB", "CM-R", "CD-R", "CD-L", "RM", "CM-L", "F", "RCF", "LM"],
        ["4-3-2-1"] = ["G", "RB", "LB", "CM", "CD-R", "CD-L", "CM-L", "CM-R", "F", "CF-R", "CF-L"],
        ["4-1-2-1-2"] = ["G", "RB", "LB", "DM", "CD-R", "CD-L", "RM", "AM", "CF-L", "CF-R", "LM"],
        ["4-3-1-2"] = ["G", "RB", "LB", "CM", "CD-R", "CD-L", "RM", "AM", "CF-R", "M", "LM"],
        ["4-1-3-2"] = ["G", "RB", "LB", "SW", "CD-R", "CD-L", "RM", "CM", "CF-R", "CF-L", "LM"],
        ["4-2-2-2"] = ["G", "RB", "LB", "CM-R", "CD-R", "CD-L", "AM-R", "CM-L", "CF-L", "CF-R", "AM-L"],
        ["3-4-2-1"] = ["G", "RM", "LM", "CD-L", "CD", "CD-R", "CM-R", "CM-L", "F", "CF-R", "CF-L"],
        ["3-4-3"] = ["G", "RM", "LM", "CD-L", "CD", "CD-R", "CM-R", "CM-L", "F", "CF-R", "CF-L"],
        ["3-5-2"] = ["G", "RM", "LM", "CD-L", "CD", "CD-R", "CM-R", "CM-L", "CF-L", "CF-R", "AM"],
        ["3-4-1-2"] = ["G", "RM", "LM", "CD-L", "CD", "CD-R", "CM-R", "CM-L", "AM", "CF-R", "CF-L"],
        ["3-1-4-2"] = ["G", "RM", "LM", "CD", "CD-R", "CD-L", "CM-R", "SW", "CF-R", "CF-L", "CM-L"],
        ["5-3-2"] = ["G", "RB", "LB", "CD-L", "CD", "CD-R", "CM-R", "CM", "CF-L", "CF-R", "CM-L"],
        ["5-4-1"] = ["G", "RB", "LB", "CD-L", "CD", "CD-R", "RM", "CM-R", "F", "CM-L", "LM"],
    };

    /// <summary>
    /// For places in a formation that isn't known, or isn't given: the most common one. Places 2, 3, 5 and 6 are the
    /// back four in every four-at-the-back formation, so a team that plays one is at least right at the back.
    /// </summary>
    private const string CommonestFormation = "4-2-3-1";

    /// <returns>Null unless both teams have a starting eleven.</returns>
    public static MatchLineups? Map(IReadOnlyList<EspnRoster>? rosters)
    {
        var home = MapTeam(rosters?.FirstOrDefault(r => r.HomeAway == "home"));
        var away = MapTeam(rosters?.FirstOrDefault(r => r.HomeAway == "away"));

        return home is not null && away is not null ? new MatchLineups(home, away) : null;
    }

    private static TeamLineup? MapTeam(EspnRoster? roster)
    {
        var entries = (roster?.Roster ?? [])
            .Where(e => !string.IsNullOrEmpty(e.Athlete?.Id) && !string.IsNullOrEmpty(e.Athlete.DisplayName))
            .ToList();
        var starters = entries.Where(e => e.Starter).ToList();
        if (starters.Count == 0)
        {
            return null;
        }

        var starterPositions = starters
            .GroupBy(e => e.Athlete!.Id!)
            .ToDictionary(g => g.Key, g => StarterPosition(g.First(), roster!.Formation));
        string? AbbreviationOf(EspnRosterEntry e) => starterPositions[e.Athlete!.Id!];

        var positions = ResolvePositions(entries, starterPositions);
        LineupPlayer ToPlayer(EspnRosterEntry e) => MapPlayer(e, positions.GetValueOrDefault(e.Athlete!.Id!));

        var keepers = starters.Where(e => AbbreviationOf(e) == Goalkeeper).Take(1).ToList();
        var outfield = starters.Except(keepers).ToList();
        var outfieldRows = Arrange(outfield, AbbreviationOf, ParseFormation(roster!.Formation));

        IEnumerable<IReadOnlyList<EspnRosterEntry>> rows = keepers.Count > 0 ? [keepers, .. outfieldRows] : outfieldRows;

        return new TeamLineup(
            string.Join('-', outfieldRows.Select(r => r.Count)),
            MapColor(roster.Uniform?.Color),
            rows.Select(row => (IReadOnlyList<LineupPlayer>)row.Select(ToPlayer).ToList()).ToList(),
            entries.Where(e => !e.Starter).Select(ToPlayer).ToList());
    }

    /// <summary>A row wider than this can't be a line of a real formation.</summary>
    private const int MaxRowSize = 5;

    /// <summary>Cuts outfield players into rows, defence first, each row left to right.</summary>
    /// <param name="rowSizes">
    /// From the formation. If null, or it doesn't add up to the number of players (a formation ESPN got wrong,
    /// or fewer than ten outfield starters), players of the same depth form a row instead. If the positions are
    /// missing too, that would put everyone in one row across the pitch, so then they are spread over a plain
    /// 4-3-3 (or as close as the number of players allows).
    /// </param>
    internal static IReadOnlyList<IReadOnlyList<T>> Arrange<T>(
        IReadOnlyList<T> outfield, Func<T, string?> position, IReadOnlyList<int>? rowSizes)
    {
        // OrderBy is stable: players of the same depth keep ESPN's order.
        var byDepth = outfield.OrderBy(p => Depth(position(p))).ToList();

        var rows = rowSizes is not null && rowSizes.Sum() == byDepth.Count
            ? SplitBySizes(byDepth, rowSizes)
            : byDepth.GroupBy(p => Depth(position(p))).Select(g => g.ToList()).ToList();
        if (rows.Any(row => row.Count > MaxRowSize))
        {
            rows = SplitBySizes(byDepth, DefaultRowSizes(byDepth.Count));
        }

        return rows
            .Select(row => (IReadOnlyList<T>)row.OrderBy(p => Lateral(position(p))).ToList())
            .ToList();
    }

    /// <summary>ESPN's position for a starter, or else the one that goes with their formation place.</summary>
    /// <returns>Null if neither is given.</returns>
    internal static string? StarterPosition(EspnRosterEntry starter, string? formation)
    {
        if (!string.IsNullOrEmpty(starter.Position?.Abbreviation))
        {
            return starter.Position.Abbreviation;
        }

        if (!int.TryParse(starter.FormationPlace, NumberStyles.None, CultureInfo.InvariantCulture, out var place) || place is < 1 or > 11)
        {
            return null;
        }

        var byPlace = formation is not null && PlacePositions.TryGetValue(formation, out var known)
            ? known
            : PlacePositions[CommonestFormation];
        return byPlace[place - 1];
    }

    /// <returns>Null if the text isn't a formation such as "4-2-3-1".</returns>
    internal static IReadOnlyList<int>? ParseFormation(string? formation)
    {
        var parts = formation?.Split('-') ?? [];
        var sizes = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size is < 1 or > 10)
            {
                return null;
            }

            sizes.Add(size);
        }

        return sizes.Count > 0 ? sizes : null;
    }

    /// <summary>4-3-3 for ten players; for fewer, four at the back and the rest shared out, the midfield first.</summary>
    internal static IReadOnlyList<int> DefaultRowSizes(int players)
    {
        if (players <= 4)
        {
            return [players];
        }

        var defence = 4;
        var midfield = (players - defence + 1) / 2;
        var attack = players - defence - midfield;
        return attack > 0 ? [defence, midfield, attack] : [defence, midfield];
    }

    private static List<List<T>> SplitBySizes<T>(List<T> players, IReadOnlyList<int> sizes)
    {
        var rows = new List<List<T>>();
        var taken = 0;
        foreach (var size in sizes)
        {
            rows.Add(players.GetRange(taken, size));
            taken += size;
        }

        return rows;
    }

    private static double Depth(string? position) =>
        position is not null && Depths.TryGetValue(position, out var depth) ? depth : UnknownDepth;

    /// <summary>0 on the left touchline to 4 on the right.</summary>
    private static int Lateral(string? position) => position switch
    {
        "LB" or "LM" or "LF" => 0,
        "LCF" => 1,
        "RCF" => 3,
        _ when position?.EndsWith("-L", StringComparison.Ordinal) == true => 1,
        _ when position?.EndsWith("-R", StringComparison.Ordinal) == true => 3,
        "RB" or "RM" or "RF" => 4,
        _ => 2,
    };

    private static PlayerPosition PositionOf(string? abbreviation) => abbreviation == Goalkeeper
        ? PlayerPosition.Goalkeeper
        : Depth(abbreviation) switch
        {
            < 2 => PlayerPosition.Defender,
            < 4.5 => PlayerPosition.Midfielder,
            _ => PlayerPosition.Forward,
        };

    /// <summary>
    /// Position per athlete id. Starters have their own; ESPN lists every substitute as "SUB", so one who
    /// came on takes the position of the player they replaced (who may be a substitute too).
    /// </summary>
    private static Dictionary<string, PlayerPosition> ResolvePositions(
        List<EspnRosterEntry> entries, Dictionary<string, string?> starterPositions)
    {
        var positions = starterPositions.ToDictionary(p => p.Key, p => PositionOf(p.Value));

        var pending = entries.Where(e => !e.Starter && e.SubbedInFor?.Athlete?.Id is not null).ToList();
        bool resolvedAny;
        do
        {
            resolvedAny = false;
            foreach (var e in pending.ToList())
            {
                if (positions.TryGetValue(e.SubbedInFor!.Athlete!.Id!, out var replaced))
                {
                    positions[e.Athlete!.Id!] = replaced;
                    pending.Remove(e);
                    resolvedAny = true;
                }
            }
        }
        while (resolvedAny && pending.Count > 0);

        return positions;
    }

    private static LineupPlayer MapPlayer(EspnRosterEntry e, PlayerPosition? position)
    {
        var substitutions = (e.Plays ?? [])
            .Where(p => p.Substitution && !string.IsNullOrEmpty(p.Clock?.DisplayValue))
            .Select(p => p.Clock!.DisplayValue!)
            .ToList();

        // A starter's only substitution is going off; a substitute comes on first and may go off again later.
        var cameOnAt = e.Starter ? null : substitutions.ElementAtOrDefault(0);
        var wentOffAt = e.SubbedOut ? substitutions.ElementAtOrDefault(e.Starter ? 0 : 1) : null;

        return new LineupPlayer(
            e.Athlete!.Id!,
            e.Athlete.DisplayName!,
            string.IsNullOrEmpty(e.Athlete.ShortName) ? e.Athlete.DisplayName! : e.Athlete.ShortName,
            string.IsNullOrEmpty(e.Jersey) ? null : e.Jersey,
            e.Starter || e.SubbedIn ? position : null,
            cameOnAt,
            wentOffAt,
            MapStats(e.Stats),
            SentOffAt: e.Plays?.FirstOrDefault(p => p.RedCard && !string.IsNullOrEmpty(p.Clock?.DisplayValue))?.Clock!.DisplayValue);
    }

    private static PlayerMatchStats MapStats(IReadOnlyList<EspnPlayerStat>? stats)
    {
        int Count(string name) => (int)Math.Round(stats?.FirstOrDefault(s => s.Name == name)?.Value ?? 0);

        return new PlayerMatchStats(
            Goals: Count("totalGoals"),
            Assists: Count("goalAssists"),
            Shots: Count("totalShots"),
            ShotsOnTarget: Count("shotsOnTarget"),
            FoulsCommitted: Count("foulsCommitted"),
            FoulsSuffered: Count("foulsSuffered"),
            Offsides: Count("offsides"),
            YellowCards: Count("yellowCards"),
            RedCards: Count("redCards"),
            OwnGoals: Count("ownGoals"),
            Saves: Count("saves"),
            GoalsConceded: Count("goalsConceded"));
    }

    private static string? MapColor(string? hex) =>
        hex is { Length: 6 } && hex.All(Uri.IsHexDigit) ? $"#{hex.ToLowerInvariant()}" : null;
}
