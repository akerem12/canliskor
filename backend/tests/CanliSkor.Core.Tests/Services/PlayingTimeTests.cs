using CanliSkor.Core.Domain;
using CanliSkor.Core.Services;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Services;

public class PlayingTimeTests
{
    private static readonly DateTimeOffset Kickoff = new(2026, 10, 9, 17, 0, 0, TimeSpan.Zero);
    private static readonly PlayerMatchStats NoStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static LineupPlayer Player(
        string name, PlayerPosition? position = PlayerPosition.Forward,
        string? cameOnAt = null, string? wentOffAt = null, string? sentOffAt = null) =>
        new(name, name, name, Jersey: null, position, cameOnAt, wentOffAt, NoStats, sentOffAt);

    /// <summary>The home team has the given starters and bench; the away team a single starter.</summary>
    private static MatchDetail Detail(Match match, LineupPlayer[] starters, LineupPlayer[]? bench = null, params MatchEvent[] events) =>
        new(match, events, [], new MatchLineups(
            new TeamLineup("4-4-2", null, [starters], bench ?? []),
            new TeamLineup("4-4-2", null, [[Player("Opponent")]], [])));

    private static int? Minutes(MatchDetail detail, string name) =>
        detail.Lineups!.Home.Rows.SelectMany(r => r).Concat(detail.Lineups.Home.Bench).Single(p => p.Name == name).MinutesPlayed;

    private static Match Finished() => Match(MatchStatus.Finished, Kickoff, score: new Score(1, 1));

    [Fact]
    public void Starter_who_stays_on_plays_the_whole_match()
    {
        var detail = PlayingTime.Apply(Detail(Finished(), [Player("Starter")]));

        Assert.Equal(90, Minutes(detail, "Starter"));
        Assert.Equal(90, detail.Lineups!.Away.Rows[0][0].MinutesPlayed);
    }

    [Fact]
    public void Minutes_run_from_coming_on_to_going_off()
    {
        var detail = PlayingTime.Apply(Detail(Finished(),
            [Player("Replaced", wentOffAt: "76'")],
            [Player("Late sub", cameOnAt: "76'"), Player("On and off", cameOnAt: "45'+2'", wentOffAt: "80'")]));

        Assert.Equal(76, Minutes(detail, "Replaced"));
        Assert.Equal(14, Minutes(detail, "Late sub"));
        Assert.Equal(35, Minutes(detail, "On and off"));
    }

    [Fact]
    public void Red_card_ends_a_players_match()
    {
        var detail = PlayingTime.Apply(Detail(Finished(), [Player("Sent off", sentOffAt: "84'")]));

        Assert.Equal(84, Minutes(detail, "Sent off"));
    }

    [Fact]
    public void Unused_substitute_has_no_minutes()
    {
        var detail = PlayingTime.Apply(Detail(Finished(), [Player("Starter")], [Player("Unused", position: null)]));

        Assert.Null(Minutes(detail, "Unused"));
    }

    [Fact]
    public void Live_match_counts_up_to_the_current_minute()
    {
        var live = Match(MatchStatus.Live, Kickoff, score: new Score(2, 0)) with { Clock = "67'" };

        var detail = PlayingTime.Apply(Detail(live, [Player("Starter")], [Player("Sub", cameOnAt: "60'")]));

        Assert.Equal(67, Minutes(detail, "Starter"));
        Assert.Equal(7, Minutes(detail, "Sub"));
    }

    [Fact]
    public void Half_time_counts_as_45_minutes()
    {
        var detail = PlayingTime.Apply(Detail(Match(MatchStatus.HalfTime, Kickoff, score: new Score(0, 0)), [Player("Starter")]));

        Assert.Equal(45, Minutes(detail, "Starter"));
    }

    [Fact]
    public void Extra_time_stretches_the_match()
    {
        var detail = PlayingTime.Apply(Detail(Finished(), [Player("Starter")], null,
            new MatchEvent(MatchEventType.Goal, "112'", TeamSide.Home, "Starter", null)));

        Assert.Equal(112, Minutes(detail, "Starter"));
    }

    [Fact]
    public void Stoppage_time_does_not_stretch_the_match()
    {
        var detail = PlayingTime.Apply(Detail(Finished(), [Player("Starter")], null,
            new MatchEvent(MatchEventType.Goal, "90'+6'", TeamSide.Home, "Starter", null)));

        Assert.Equal(90, Minutes(detail, "Starter"));
    }

    [Theory]
    [InlineData(MatchStatus.Scheduled)]
    [InlineData(MatchStatus.Postponed)]
    public void Match_that_has_not_been_played_has_no_minutes(MatchStatus status)
    {
        var detail = Detail(Match(status, Kickoff), [Player("Starter")]);

        Assert.Same(detail, PlayingTime.Apply(detail));
    }

    [Fact]
    public void Match_without_lineups_is_returned_as_is()
    {
        var detail = new MatchDetail(Finished(), [], []);

        Assert.Same(detail, PlayingTime.Apply(detail));
    }
}
