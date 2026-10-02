using CanliSkor.Core.Domain;
using CanliSkor.Core.Polling;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Polling;

public class MatchChangeDetectorTests
{
    private static readonly DateTimeOffset Kickoff = new(2026, 10, 9, 17, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 10, 9);

    private static readonly Match LiveMatch = Match(MatchStatus.Live, Kickoff, score: new Score(0, 0)) with { Clock = "20'" };

    [Fact]
    public void No_previous_scoreboard_means_no_changes()
    {
        Assert.Empty(MatchChangeDetector.Detect(null, Scoreboard("tur.1", Date, LiveMatch)));
    }

    [Fact]
    public void Identical_match_produces_no_change()
    {
        Assert.Empty(Detect(LiveMatch, LiveMatch with { }));
    }

    [Fact]
    public void Goal_is_reported_as_score_change()
    {
        var change = Assert.Single(Detect(LiveMatch, LiveMatch with { Score = new Score(1, 0) }));

        Assert.Equal(MatchChangeKind.Score, change.Kinds);
        Assert.Equal(new Score(1, 0), change.Match.Score);
    }

    [Fact]
    public void Clock_only_change_is_reported()
    {
        var change = Assert.Single(Detect(LiveMatch, LiveMatch with { Clock = "21'" }));

        Assert.Equal(MatchChangeKind.Clock, change.Kinds);
    }

    [Fact]
    public void Kickoff_reports_status_score_and_clock_together()
    {
        var scheduled = LiveMatch with { Status = MatchStatus.Scheduled, Clock = null, Score = null };

        var change = Assert.Single(Detect(scheduled, LiveMatch));

        Assert.Equal(MatchChangeKind.Status | MatchChangeKind.Score | MatchChangeKind.Clock, change.Kinds);
    }

    [Fact]
    public void Matches_new_in_this_poll_are_skipped()
    {
        var other = Match(MatchStatus.Live, Kickoff);

        var changes = MatchChangeDetector.Detect(Scoreboard("tur.1", Date, LiveMatch), Scoreboard("tur.1", Date, LiveMatch, other));

        Assert.Empty(changes);
    }

    private static IReadOnlyList<MatchChange> Detect(Match before, Match after) =>
        MatchChangeDetector.Detect(Scoreboard("tur.1", Date, before), Scoreboard("tur.1", Date, after));
}
