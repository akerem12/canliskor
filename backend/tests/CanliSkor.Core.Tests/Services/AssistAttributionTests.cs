using CanliSkor.Core.Domain;
using CanliSkor.Core.Services;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Services;

public class AssistAttributionTests
{
    private static readonly DateTimeOffset Kickoff = new(2026, 10, 9, 17, 0, 0, TimeSpan.Zero);

    private static LineupPlayer Player(string name, int assists = 0, string? cameOnAt = null, string? wentOffAt = null, string? sentOffAt = null) =>
        new(name, name, name, Jersey: null, PlayerPosition.Forward, cameOnAt, wentOffAt,
            new PlayerMatchStats(0, assists, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), sentOffAt);

    private static MatchEvent Goal(string clock, string scorer, string? assist = null, TeamSide side = TeamSide.Home, MatchEventType type = MatchEventType.Goal) =>
        new(type, clock, side, scorer, assist, PlayerId: scorer, RelatedPlayerId: assist);

    /// <summary>The home team has the given starters and bench; the away team a single starter.</summary>
    private static MatchDetail Detail(LineupPlayer[] starters, LineupPlayer[] bench, params MatchEvent[] events) =>
        new(Match(MatchStatus.Finished, Kickoff, score: new Score(1, 0)), events, [], new MatchLineups(
            new TeamLineup("4-4-2", null, [starters], bench),
            new TeamLineup("4-4-2", null, [[Player("Opponent", assists: 1)]], [])));

    private static string?[] Assists(MatchDetail detail) => AssistAttribution.Apply(detail).Events.Select(e => e.RelatedPlayer).ToArray();

    [Fact]
    public void The_only_goal_goes_to_the_only_player_with_an_assist()
    {
        var detail = AssistAttribution.Apply(Detail([Player("Scorer"), Player("Maker", assists: 1)], [], Goal("13'", "Scorer")));

        Assert.Equal(new MatchEvent(MatchEventType.Goal, "13'", TeamSide.Home, "Scorer", "Maker", "Scorer", "Maker"), Assert.Single(detail.Events));
    }

    [Fact]
    public void An_assist_that_fits_two_goals_is_left_open()
    {
        // The real case: Orban scored the first and set up one of the two others; the data doesn't say which.
        var detail = Detail([Player("Orban", assists: 1), Player("Saba"), Player("Soyalp")], [],
            Goal("13'", "Orban"), Goal("22'", "Saba"), Goal("59'", "Soyalp"));

        Assert.Equal([null, null, null], Assists(detail));
    }

    [Fact]
    public void A_player_does_not_assist_their_own_goal()
    {
        var detail = Detail([Player("Orban", assists: 1), Player("Saba")], [], Goal("13'", "Orban"), Goal("22'", "Saba"));

        Assert.Equal([null, "Orban"], Assists(detail));
    }

    [Fact]
    public void Goals_scored_while_the_player_was_off_the_pitch_do_not_count()
    {
        var detail = Detail(
            [Player("Scorer"), Player("Starter", assists: 1, wentOffAt: "60'")],
            [Player("Sub", assists: 1, cameOnAt: "60'"), Player("Unused", assists: 0)],
            Goal("20'", "Scorer"), Goal("80'", "Scorer"));

        Assert.Equal(["Starter", "Sub"], Assists(detail));
    }

    [Fact]
    public void A_goal_in_the_minute_of_a_substitution_could_be_either_players()
    {
        var detail = Detail(
            [Player("Scorer"), Player("Starter", assists: 1, wentOffAt: "60'")],
            [Player("Sub", assists: 1, cameOnAt: "60'")],
            Goal("60'", "Scorer"));

        Assert.Equal([null], Assists(detail));
    }

    [Fact]
    public void Settling_one_assist_can_settle_the_next()
    {
        // Late could only have made the second goal, which leaves the first to Early.
        var detail = Detail(
            [Player("Scorer"), Player("Early", assists: 1)],
            [Player("Late", assists: 1, cameOnAt: "70'")],
            Goal("10'", "Scorer"), Goal("85'", "Scorer"));

        Assert.Equal(["Early", "Late"], Assists(detail));
    }

    [Fact]
    public void A_player_with_two_assists_and_two_possible_goals_made_both()
    {
        var detail = Detail([Player("Scorer"), Player("Maker", assists: 2)], [], Goal("10'", "Scorer"), Goal("50'", "Scorer"));

        Assert.Equal(["Maker", "Maker"], Assists(detail));
    }

    [Fact]
    public void Assists_named_by_the_event_are_kept_and_counted()
    {
        // Maker's one assist is already on the first goal, so the second stays open.
        var detail = Detail([Player("Scorer"), Player("Maker", assists: 1)], [], Goal("10'", "Scorer", assist: "Maker"), Goal("50'", "Scorer"));

        Assert.Equal(["Maker", null], Assists(detail));
    }

    [Fact]
    public void Penalties_and_own_goals_never_get_an_assist()
    {
        var detail = Detail([Player("Scorer"), Player("Maker", assists: 1)], [],
            Goal("10'", "Scorer", type: MatchEventType.PenaltyGoal), Goal("50'", "Opponent", type: MatchEventType.OwnGoal));

        Assert.Equal([null, null], Assists(detail));
    }

    [Fact]
    public void Each_team_is_worked_out_from_its_own_players()
    {
        var detail = Detail([Player("Scorer"), Player("Maker", assists: 1)], [],
            Goal("10'", "Scorer"), Goal("30'", "Away scorer", side: TeamSide.Away));

        Assert.Equal(["Maker", "Opponent"], Assists(detail));
    }

    [Fact]
    public void Two_players_claiming_one_goal_means_nobody_gets_it()
    {
        var detail = Detail([Player("Scorer"), Player("One", assists: 1), Player("Two", assists: 1)], [], Goal("10'", "Scorer"));

        Assert.Equal([null], Assists(detail));
    }

    [Fact]
    public void Nothing_changes_without_lineups_or_without_assists()
    {
        var withoutLineups = new MatchDetail(Match(MatchStatus.Finished, Kickoff), [Goal("10'", "Scorer")], []);
        var withoutAssists = Detail([Player("Scorer"), Player("Other")], [], Goal("10'", "Scorer"));

        Assert.Same(withoutLineups, AssistAttribution.Apply(withoutLineups));
        Assert.Same(withoutAssists, AssistAttribution.Apply(withoutAssists));
    }
}
