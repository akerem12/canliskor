using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;

namespace CanliSkor.Core.Ratings;

public enum MatchOutcome
{
    Win,
    Draw,
    Loss,
}

/// <summary>
/// Our own player rating: an estimate computed from the match statistics we have, not taken from any
/// rating provider. Pure, so the formula can be tested and tuned through <see cref="RatingOptions"/> alone.
/// </summary>
public static class PlayerRatingCalculator
{
    /// <param name="outcome">The player's team's result; null while the match is still being played.</param>
    /// <returns>One decimal, e.g. 7.3. Null if the player was on the pitch too briefly to be rated.</returns>
    public static double? Calculate(
        PlayerPosition position, int minutesPlayed, PlayerMatchStats stats, MatchOutcome? outcome, RatingOptions options)
    {
        if (minutesPlayed < options.MinMinutes)
        {
            return null;
        }

        var rating = options.Base
            + stats.Goals * options.Goal.For(position)
            + stats.Assists * options.Assist
            + Math.Max(0, stats.ShotsOnTarget - stats.Goals) * options.ShotOnTarget
            + stats.FoulsSuffered * options.FoulSuffered
            + stats.FoulsCommitted * options.FoulCommitted
            + stats.Offsides * options.Offside
            + stats.YellowCards * options.YellowCard
            + stats.RedCards * options.RedCard
            + stats.OwnGoals * options.OwnGoal
            + stats.Saves * options.Save
            + stats.GoalsConceded * options.GoalConceded.For(position);

        if (stats.GoalsConceded == 0 && minutesPlayed >= options.CleanSheetMinMinutes)
        {
            rating += options.CleanSheet.For(position);
        }

        rating += outcome switch
        {
            MatchOutcome.Win => options.Win,
            MatchOutcome.Loss => options.Loss,
            _ => 0,
        };

        // Through decimal, so a sum such as 7.35 that a double holds as 7.3499999 still rounds to 7.4.
        return (double)Math.Round((decimal)Math.Clamp(rating, options.Min, options.Max), 1, MidpointRounding.AwayFromZero);
    }
}
