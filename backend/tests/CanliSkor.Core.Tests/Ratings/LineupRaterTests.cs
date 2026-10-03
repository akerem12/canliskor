using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Ratings;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Ratings;

public class LineupRaterTests
{
    private static readonly DateTimeOffset Kickoff = new(2026, 10, 9, 17, 0, 0, TimeSpan.Zero);
    private static readonly PlayerMatchStats Quiet = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, GoalsConceded: 1);

    private static readonly LineupRater Rater = new(new StaticOptionsMonitor<RatingOptions>(new RatingOptions()));

    private static LineupPlayer Player(
        string name, PlayerPosition? position = PlayerPosition.Forward,
        string? cameOnAt = null, string? wentOffAt = null, string? sentOffAt = null) =>
        new(name, name, name, Jersey: null, position, cameOnAt, wentOffAt, Quiet, sentOffAt);

    /// <summary>The home team has the given starters and bench; the away team a single starter.</summary>
    private static MatchDetail Detail(Match match, LineupPlayer[] starters, LineupPlayer[]? bench = null, params MatchEvent[] events) =>
        new(match, events, [], new MatchLineups(
            new TeamLineup("4-4-2", null, [starters], bench ?? []),
            new TeamLineup("4-4-2", null, [[Player("Opponent")]], [])));

    private static LineupPlayer HomePlayer(MatchDetail rated, string name) =>
        rated.Lineups!.Home.Rows.SelectMany(r => r).Concat(rated.Lineups.Home.Bench).Single(p => p.Name == name);

    private static Match Finished(int home, int away) => Match(MatchStatus.Finished, Kickoff, score: new Score(home, away));

    [Fact]
    public void Starter_who_stays_on_plays_the_whole_match()
    {
        var rated = Rater.Rate(Detail(Finished(1, 1), [Player("Starter")]));

        Assert.Equal(90, HomePlayer(rated, "Starter").MinutesPlayed);
        Assert.Equal(6.5, HomePlayer(rated, "Starter").Rating);
    }

    [Fact]
    public void Minutes_run_from_coming_on_to_going_off()
    {
        var rated = Rater.Rate(Detail(Finished(1, 1),
            [Player("Replaced", wentOffAt: "76'")],
            [Player("Late sub", cameOnAt: "76'"), Player("On and off", cameOnAt: "45'+2'", wentOffAt: "80'")]));

        Assert.Equal(76, HomePlayer(rated, "Replaced").MinutesPlayed);
        Assert.Equal(14, HomePlayer(rated, "Late sub").MinutesPlayed);
        Assert.Equal(35, HomePlayer(rated, "On and off").MinutesPlayed);
    }

    [Fact]
    public void Red_card_ends_a_players_match()
    {
        var rated = Rater.Rate(Detail(Finished(1, 1), [Player("Sent off", sentOffAt: "84'")]));

        Assert.Equal(84, HomePlayer(rated, "Sent off").MinutesPlayed);
    }

    [Fact]
    public void Substitute_with_only_a_few_minutes_has_minutes_but_no_rating()
    {
        var rated = Rater.Rate(Detail(Finished(1, 1), [Player("Starter")], [Player("Cameo", cameOnAt: "88'")]));

        Assert.Equal(2, HomePlayer(rated, "Cameo").MinutesPlayed);
        Assert.Null(HomePlayer(rated, "Cameo").Rating);
    }

    [Fact]
    public void Unused_substitute_has_neither_minutes_nor_rating()
    {
        var rated = Rater.Rate(Detail(Finished(1, 1), [Player("Starter")], [Player("Unused", position: null)]));

        Assert.Null(HomePlayer(rated, "Unused").MinutesPlayed);
        Assert.Null(HomePlayer(rated, "Unused").Rating);
    }

    [Fact]
    public void Winners_and_losers_get_the_result_once_the_match_is_over()
    {
        var rated = Rater.Rate(Detail(Finished(2, 1), [Player("Starter")]));

        Assert.Equal(6.8, HomePlayer(rated, "Starter").Rating);
        Assert.Equal(6.2, rated.Lineups!.Away.Rows[0][0].Rating);
    }

    [Fact]
    public void Live_match_is_rated_up_to_the_current_minute_without_the_result()
    {
        var live = Match(MatchStatus.Live, Kickoff, score: new Score(2, 0)) with { Clock = "67'" };

        var rated = Rater.Rate(Detail(live, [Player("Starter")], [Player("Sub", cameOnAt: "60'")]));

        Assert.Equal(67, HomePlayer(rated, "Starter").MinutesPlayed);
        Assert.Equal(6.5, HomePlayer(rated, "Starter").Rating);
        Assert.Equal(7, HomePlayer(rated, "Sub").MinutesPlayed);
        Assert.Null(HomePlayer(rated, "Sub").Rating);
    }

    [Fact]
    public void Half_time_counts_as_45_minutes()
    {
        var rated = Rater.Rate(Detail(Match(MatchStatus.HalfTime, Kickoff, score: new Score(0, 0)), [Player("Starter")]));

        Assert.Equal(45, HomePlayer(rated, "Starter").MinutesPlayed);
    }

    [Fact]
    public void Extra_time_stretches_the_match()
    {
        var rated = Rater.Rate(Detail(Finished(2, 1), [Player("Starter")], null,
            new MatchEvent(MatchEventType.Goal, "112'", TeamSide.Home, "Starter", null)));

        Assert.Equal(112, HomePlayer(rated, "Starter").MinutesPlayed);
    }

    [Fact]
    public void Stoppage_time_does_not_stretch_the_match()
    {
        var rated = Rater.Rate(Detail(Finished(2, 1), [Player("Starter")], null,
            new MatchEvent(MatchEventType.Goal, "90'+6'", TeamSide.Home, "Starter", null)));

        Assert.Equal(90, HomePlayer(rated, "Starter").MinutesPlayed);
    }

    [Theory]
    [InlineData(MatchStatus.Scheduled)]
    [InlineData(MatchStatus.Postponed)]
    public void Match_that_has_not_been_played_is_not_rated(MatchStatus status)
    {
        var detail = Detail(Match(status, Kickoff), [Player("Starter")]);

        Assert.Same(detail, Rater.Rate(detail));
    }

    [Fact]
    public void Match_without_lineups_is_returned_as_is()
    {
        var detail = new MatchDetail(Finished(1, 0), [], []);

        Assert.Same(detail, Rater.Rate(detail));
    }
}
