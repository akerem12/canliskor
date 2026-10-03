using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Ratings;

namespace CanliSkor.Core.Tests.Ratings;

public class PlayerRatingCalculatorTests
{
    private static readonly RatingOptions Defaults = new();

    private static PlayerMatchStats Stats(
        int goals = 0, int assists = 0, int shotsOnTarget = 0, int foulsCommitted = 0, int foulsSuffered = 0,
        int offsides = 0, int yellowCards = 0, int redCards = 0, int ownGoals = 0, int saves = 0, int goalsConceded = 1) =>
        new(goals, assists, Shots: shotsOnTarget, shotsOnTarget, foulsCommitted, foulsSuffered, offsides,
            yellowCards, redCards, ownGoals, saves, goalsConceded);

    private static double? Rate(
        PlayerPosition position, PlayerMatchStats stats, int minutes = 90, MatchOutcome? outcome = MatchOutcome.Draw) =>
        PlayerRatingCalculator.Calculate(position, minutes, stats, outcome, Defaults);

    [Fact]
    public void Quiet_match_is_the_base_rating()
    {
        // A forward isn't blamed for the goal conceded.
        Assert.Equal(6.5, Rate(PlayerPosition.Forward, Stats()));
    }

    [Fact]
    public void Too_few_minutes_means_no_rating()
    {
        Assert.Null(Rate(PlayerPosition.Forward, Stats(goals: 1), minutes: 9));
        Assert.Equal(7.5, Rate(PlayerPosition.Forward, Stats(goals: 1, shotsOnTarget: 1), minutes: 10));
    }

    [Theory]
    [InlineData(PlayerPosition.Goalkeeper, 7.6)] // 6.5 + 1.5 - 0.4 conceded
    [InlineData(PlayerPosition.Defender, 7.7)]   // 6.5 + 1.4 - 0.25 conceded = 7.65
    [InlineData(PlayerPosition.Midfielder, 7.6)] // 6.5 + 1.2 - 0.1 conceded
    [InlineData(PlayerPosition.Forward, 7.5)]    // 6.5 + 1.0
    public void Goal_is_worth_more_the_further_back_the_scorer_plays(PlayerPosition position, double expected)
    {
        Assert.Equal(expected, Rate(position, Stats(goals: 1, shotsOnTarget: 1)));
    }

    [Fact]
    public void Shots_on_target_count_only_when_they_were_not_goals()
    {
        Assert.Equal(6.9, Rate(PlayerPosition.Forward, Stats(shotsOnTarget: 2)));
        Assert.Equal(7.7, Rate(PlayerPosition.Forward, Stats(goals: 1, shotsOnTarget: 2)));
    }

    [Fact]
    public void Adds_up_everything_a_player_did()
    {
        // Gift Orban in Amed 3-2 Besiktas: goal, assist, another shot on target, three fouls, one suffered, win.
        var stats = Stats(goals: 1, assists: 1, shotsOnTarget: 2, foulsCommitted: 3, foulsSuffered: 1);

        // 6.5 + 1.0 + 0.8 + 0.2 - 0.3 + 0.1 + 0.3
        Assert.Equal(8.6, Rate(PlayerPosition.Forward, stats, minutes: 76, outcome: MatchOutcome.Win));
    }

    [Fact]
    public void Cards_offsides_and_own_goals_cost()
    {
        Assert.Equal(6.1, Rate(PlayerPosition.Forward, Stats(yellowCards: 1)));
        Assert.Equal(5.0, Rate(PlayerPosition.Forward, Stats(redCards: 1)));
        Assert.Equal(5.3, Rate(PlayerPosition.Forward, Stats(ownGoals: 1)));
        Assert.Equal(6.3, Rate(PlayerPosition.Forward, Stats(offsides: 2)));
    }

    [Fact]
    public void Goalkeeper_is_rated_on_saves_and_goals_conceded()
    {
        // Five saves, two conceded: 6.5 + 1.5 - 0.8
        Assert.Equal(7.2, Rate(PlayerPosition.Goalkeeper, Stats(saves: 5, goalsConceded: 2)));
    }

    [Theory]
    [InlineData(PlayerPosition.Goalkeeper, 7.1)]
    [InlineData(PlayerPosition.Defender, 7.0)]
    [InlineData(PlayerPosition.Midfielder, 6.7)]
    [InlineData(PlayerPosition.Forward, 6.5)]
    public void Clean_sheet_rewards_those_who_defend(PlayerPosition position, double expected)
    {
        Assert.Equal(expected, Rate(position, Stats(goalsConceded: 0)));
    }

    [Fact]
    public void Clean_sheet_needs_an_hour_on_the_pitch()
    {
        Assert.Equal(6.5, Rate(PlayerPosition.Defender, Stats(goalsConceded: 0), minutes: 59));
        Assert.Equal(7.0, Rate(PlayerPosition.Defender, Stats(goalsConceded: 0), minutes: 60));
    }

    [Fact]
    public void Result_counts_once_the_match_is_over()
    {
        Assert.Equal(6.8, Rate(PlayerPosition.Forward, Stats(), outcome: MatchOutcome.Win));
        Assert.Equal(6.2, Rate(PlayerPosition.Forward, Stats(), outcome: MatchOutcome.Loss));
        Assert.Equal(6.5, Rate(PlayerPosition.Forward, Stats(), outcome: null));
    }

    [Fact]
    public void Rating_stays_between_3_and_10()
    {
        Assert.Equal(10, Rate(PlayerPosition.Defender, Stats(goals: 4, shotsOnTarget: 4)));
        Assert.Equal(3, Rate(PlayerPosition.Goalkeeper, Stats(redCards: 1, ownGoals: 1, goalsConceded: 5)));
    }

    [Fact]
    public void Weights_come_from_the_options()
    {
        var options = new RatingOptions { Base = 6, Assist = 1 };

        Assert.Equal(7, PlayerRatingCalculator.Calculate(PlayerPosition.Forward, 90, Stats(assists: 1), null, options));
    }
}
