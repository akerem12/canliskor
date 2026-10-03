using System.Globalization;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Ratings;

/// <summary>
/// Fills in minutes played and ratings for everyone in a match's line-ups. Minutes aren't in the data, so
/// they are worked out from when the player came on, went off or was sent off, and how far the match is.
/// </summary>
public sealed class LineupRater(IOptionsMonitor<RatingOptions> options)
{
    private const int RegularTime = 90;
    private const int HalfTime = 45;

    public MatchDetail Rate(MatchDetail detail)
    {
        if (detail.Lineups is not { } lineups || MinutesSoFar(detail) is not { } matchMinutes)
        {
            return detail;
        }

        var weights = options.CurrentValue;
        var score = detail.Match.Status == MatchStatus.Finished ? detail.Match.Score : null;

        return detail with
        {
            Lineups = new MatchLineups(
                Rate(lineups.Home, matchMinutes, Outcome(score?.Home, score?.Away), weights),
                Rate(lineups.Away, matchMinutes, Outcome(score?.Away, score?.Home), weights)),
        };
    }

    private static TeamLineup Rate(TeamLineup lineup, int matchMinutes, MatchOutcome? outcome, RatingOptions weights)
    {
        LineupPlayer RatePlayer(LineupPlayer player, bool starter)
        {
            if (player.Position is not { } position || MinutesPlayed(player, starter, matchMinutes) is not { } minutes)
            {
                return player;
            }

            return player with
            {
                MinutesPlayed = minutes,
                Rating = PlayerRatingCalculator.Calculate(position, minutes, player.Stats, outcome, weights),
            };
        }

        return lineup with
        {
            Rows = lineup.Rows
                .Select(row => (IReadOnlyList<LineupPlayer>)row.Select(p => RatePlayer(p, starter: true)).ToList())
                .ToList(),
            Bench = lineup.Bench.Select(p => RatePlayer(p, starter: false)).ToList(),
        };
    }

    /// <returns>Null for a substitute who hasn't come on.</returns>
    private static int? MinutesPlayed(LineupPlayer player, bool starter, int matchMinutes)
    {
        var from = starter ? 0 : Minute(player.CameOnAt);
        if (from is null)
        {
            return null;
        }

        var until = new[] { Minute(player.WentOffAt), Minute(player.SentOffAt), matchMinutes }.Min(m => m ?? int.MaxValue);
        return Math.Max(0, until - from.Value);
    }

    /// <returns>How many minutes have been played; null if the match hasn't started or never will.</returns>
    private static int? MinutesSoFar(MatchDetail detail) => detail.Match.Status switch
    {
        MatchStatus.Live => Minute(detail.Match.Clock),
        MatchStatus.HalfTime => HalfTime,
        // Extra time shows in the events: anything timed after the 90th minute stretches the match.
        MatchStatus.Finished => detail.Events.Select(e => Minute(e.Clock) ?? 0).Append(RegularTime).Max(),
        _ => null,
    };

    /// <summary>"76'" → 76; "45'+2'" → 45: stoppage time counts as the minute it is added to.</summary>
    private static int? Minute(string? clock)
    {
        var digits = new string(clock?.TakeWhile(char.IsAsciiDigit).ToArray() ?? []);
        return digits.Length is > 0 and <= 3 ? int.Parse(digits, CultureInfo.InvariantCulture) : null;
    }

    private static MatchOutcome? Outcome(int? scored, int? conceded) => (scored, conceded) switch
    {
        (null, _) or (_, null) => null,
        _ when scored > conceded => MatchOutcome.Win,
        _ when scored < conceded => MatchOutcome.Loss,
        _ => MatchOutcome.Draw,
    };
}
