using CanliSkor.Core.Domain;
using CanliSkor.Core.Polling;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Polling;

public class PollingIntervalCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 15, 0, 0, TimeSpan.Zero);
    private static readonly CanliSkor.Core.Options.PollingOptions Options = TestOptions.Polling();

    private static TimeSpan Calculate(params Match[] matches) =>
        PollingIntervalCalculator.Calculate(matches, Now, Options);

    [Fact]
    public void No_matches_polls_at_idle_interval() =>
        Assert.Equal(Options.IdleInterval, Calculate());

    [Theory]
    [InlineData(MatchStatus.Live)]
    [InlineData(MatchStatus.HalfTime)]
    public void Match_in_play_polls_at_live_interval(MatchStatus status) =>
        Assert.Equal(Options.LiveInterval, Calculate(
            Match(MatchStatus.Finished, Now.AddHours(-3)),
            Match(status, Now.AddMinutes(-30)),
            Match(MatchStatus.Scheduled, Now.AddHours(5))));

    [Fact]
    public void Only_finished_matches_polls_at_idle_interval() =>
        Assert.Equal(Options.IdleInterval, Calculate(Match(MatchStatus.Finished, Now.AddHours(-2))));

    [Fact]
    public void Distant_kickoff_is_capped_at_idle_interval() =>
        Assert.Equal(Options.IdleInterval, Calculate(Match(MatchStatus.Scheduled, Now.AddHours(2))));

    [Fact]
    public void Wakes_up_lead_time_before_next_kickoff()
    {
        var delay = Calculate(
            Match(MatchStatus.Scheduled, Now.AddMinutes(40)),
            Match(MatchStatus.Scheduled, Now.AddMinutes(10)));

        Assert.Equal(TimeSpan.FromMinutes(8), delay); // earliest kickoff (10 min) minus 2 min lead
    }

    [Fact]
    public void Kickoff_within_lead_time_polls_at_live_interval() =>
        Assert.Equal(Options.LiveInterval, Calculate(Match(MatchStatus.Scheduled, Now.AddMinutes(1))));

    [Fact]
    public void Scheduled_match_past_kickoff_is_treated_as_live()
    {
        // Provider hasn't flipped the status yet; we must not sleep through the first minutes.
        Assert.Equal(Options.LiveInterval, Calculate(Match(MatchStatus.Scheduled, Now.AddMinutes(-3))));
    }

    [Fact]
    public void Scheduled_match_long_past_kickoff_is_ignored()
    {
        // Probably postponed without the provider saying so; don't poll every 30s all day.
        Assert.Equal(Options.IdleInterval, Calculate(Match(MatchStatus.Scheduled, Now.AddHours(-4))));
    }

    [Theory]
    [InlineData(MatchStatus.Postponed)]
    [InlineData(MatchStatus.Cancelled)]
    public void Postponed_or_cancelled_matches_do_not_trigger_fast_polling(MatchStatus status) =>
        Assert.Equal(Options.IdleInterval, Calculate(Match(status, Now.AddMinutes(5))));
}
