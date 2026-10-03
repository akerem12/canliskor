using System.Globalization;
using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Services;

/// <summary>
/// Names the assist provider of goals whose event doesn't. For some leagues the data says how many assists each
/// player had in the match but not for which goal. A goal gets an assister only where that leaves no doubt: a
/// player with n assists still unaccounted for who could have set up exactly n of the team's open goals (was on
/// the pitch, didn't score it himself) set up those. Anything ambiguous stays without an assist.
/// </summary>
public static class AssistAttribution
{
    public static MatchDetail Apply(MatchDetail detail)
    {
        if (detail.Lineups is not { } lineups)
        {
            return detail;
        }

        var events = detail.Events.ToList();
        var changed = Attribute(events, TeamSide.Home, lineups.Home);
        changed |= Attribute(events, TeamSide.Away, lineups.Away);

        return changed ? detail with { Events = events } : detail;
    }

    private static bool Attribute(List<MatchEvent> events, TeamSide side, TeamLineup lineup)
    {
        // Penalties and own goals have no assist; goals that already name one are settled.
        var open = Enumerable.Range(0, events.Count)
            .Where(i => events[i] is { Type: MatchEventType.Goal, RelatedPlayer: null } e && e.Side == side && Minute(e.Clock) is not null)
            .ToHashSet();

        var assisters = lineup.Rows.SelectMany(row => row).Select(p => new Assister(p, Starter: true))
            .Concat(lineup.Bench.Select(p => new Assister(p, Starter: false)))
            .Select(a => a with { Owed = a.Player.Stats.Assists - events.Count(e => e.Type == MatchEventType.Goal && e.Side == side && e.RelatedPlayerId == a.Player.Id) })
            .Where(a => a.Owed > 0)
            .ToList();

        var changed = false;
        while (open.Count > 0 && assisters.Count > 0)
        {
            // Everyone whose open goals are exactly as many as the assists they are owed.
            var claims = assisters
                .Select(a => (Assister: a, Goals: open.Where(i => CouldAssist(a, events[i])).ToList()))
                .Where(c => c.Goals.Count == c.Assister.Owed)
                .ToList();
            if (claims.Count == 0)
            {
                break;
            }

            // Two players claiming the same goal means the numbers don't add up: neither gets it.
            var contested = claims.SelectMany(c => c.Goals).GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            foreach (var (assister, goals) in claims)
            {
                assisters.Remove(assister);
                if (goals.Any(contested.Contains))
                {
                    continue;
                }

                foreach (var i in goals)
                {
                    events[i] = events[i] with { RelatedPlayer = assister.Player.Name, RelatedPlayerId = assister.Player.Id };
                    open.Remove(i);
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool CouldAssist(Assister assister, MatchEvent goal)
    {
        var player = assister.Player;
        if (goal.PlayerId == player.Id || (goal.PlayerId is null && goal.Player == player.Name))
        {
            return false;
        }

        // Both ends count as on the pitch: a goal in the minute of a substitution could be either player's.
        var minute = Minute(goal.Clock)!.Value;
        var from = assister.Starter ? 0 : Minute(player.CameOnAt);
        var until = new[] { Minute(player.WentOffAt), Minute(player.SentOffAt) }.Min(m => m ?? int.MaxValue);

        return from is not null && from <= minute && minute <= until;
    }

    /// <summary>"76'" → 76; "45'+2'" → 45: stoppage time counts as the minute it is added to.</summary>
    private static int? Minute(string? clock)
    {
        var digits = new string(clock?.TakeWhile(char.IsAsciiDigit).ToArray() ?? []);
        return digits.Length is > 0 and <= 3 ? int.Parse(digits, CultureInfo.InvariantCulture) : null;
    }

    private sealed record Assister(LineupPlayer Player, bool Starter, int Owed = 0);
}
