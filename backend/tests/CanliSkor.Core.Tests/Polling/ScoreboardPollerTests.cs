using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Polling;
using CanliSkor.Core.Ratings;
using CanliSkor.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static CanliSkor.Core.Tests.TestData;

namespace CanliSkor.Core.Tests.Polling;

public class ScoreboardPollerTests
{
    // 22:30 UTC on 3 Oct is already 01:30 on 4 Oct in Istanbul (UTC+3).
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 22, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 4);
    private static readonly DateOnly Yesterday = new(2026, 10, 3);

    private readonly FakeFootballDataProvider _provider = new();
    private readonly FakeMatchStore _store = new();
    private readonly FakeMatchUpdatePublisher _publisher = new();
    private readonly FakeTimeProvider _time = new(Now);

    private readonly MatchViewerRegistry _viewers = new();

    private ScoreboardPoller CreatePoller(params string[] leagues) => new(
        _provider,
        _store,
        _publisher,
        new LiveDetailRefresher(
            _viewers,
            new MatchDetailService(
                _provider,
                _store,
                new OnDemandFetchGate(),
                new LineupRater(new StaticOptionsMonitor<RatingOptions>(new RatingOptions())),
                TestOptions.Leagues(leagues),
                new StaticOptionsMonitor<PollingOptions>(TestOptions.Polling()),
                _time,
                NullLogger<MatchDetailService>.Instance),
            _store,
            _publisher,
            NullLogger<LiveDetailRefresher>.Instance),
        TestOptions.Leagues(leagues),
        new StaticOptionsMonitor<PollingOptions>(TestOptions.Polling()),
        _time,
        NullLogger<ScoreboardPoller>.Instance);

    [Fact]
    public async Task Fetches_every_league_for_today_in_istanbul_and_stores_it()
    {
        // Yesterday is known and over, so only today is fetched.
        await _store.SetAsync(new ScoreboardSnapshot(Scoreboard("tur.1", Yesterday), Now.AddHours(-1)));
        await _store.SetAsync(new ScoreboardSnapshot(Scoreboard("eng.1", Yesterday), Now.AddHours(-1)));
        _provider.Returns(Scoreboard("tur.1", Today));
        _provider.Returns(Scoreboard("eng.1", Today));

        await CreatePoller("tur.1", "eng.1").PollAsync(CancellationToken.None);

        Assert.Equal([("tur.1", Today), ("eng.1", Today)], _provider.Requests);
        var stored = await _store.GetAsync("tur.1", Today);
        Assert.NotNull(stored);
        Assert.Equal(Now, stored.FetchedAtUtc);
        Assert.NotNull(await _store.GetAsync("eng.1", Today));
    }

    [Fact]
    public async Task Returns_live_interval_when_a_match_is_live()
    {
        _provider.Returns(Scoreboard("tur.1", Today, Match(MatchStatus.Live, Now.AddMinutes(-20))));

        var delay = await CreatePoller("tur.1").PollAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public async Task Provider_failure_keeps_last_snapshot_and_retries_sooner()
    {
        var previous = new ScoreboardSnapshot(Scoreboard("tur.1", Today, Match(MatchStatus.Finished, Now.AddHours(-3))), Now.AddMinutes(-15));
        await _store.SetAsync(previous);
        // _provider has no data configured, so every fetch fails.

        var delay = await CreatePoller("tur.1").PollAsync(CancellationToken.None);

        Assert.Same(previous, await _store.GetAsync("tur.1", Today));
        Assert.Equal(TimeSpan.FromMinutes(1), delay); // ErrorRetryInterval instead of 15 min idle
    }

    [Fact]
    public async Task One_failing_league_does_not_block_the_others()
    {
        _provider.Returns(Scoreboard("eng.1", Today));

        await CreatePoller("tur.1", "eng.1").PollAsync(CancellationToken.None);

        Assert.NotNull(await _store.GetAsync("eng.1", Today));
    }

    [Fact]
    public async Task Keeps_polling_yesterday_while_a_match_there_is_still_live()
    {
        // Kicked off 22:00 Istanbul yesterday, now 01:30 — e.g. extra time and penalties.
        await _store.SetAsync(new ScoreboardSnapshot(Scoreboard("uefa.champions", Yesterday, Match(MatchStatus.Live, Now.AddMinutes(-150))), Now.AddSeconds(-30)));
        _provider.Returns(Scoreboard("uefa.champions", Yesterday, Match(MatchStatus.Finished, Now.AddMinutes(-150))));
        _provider.Returns(Scoreboard("uefa.champions", Today));

        await CreatePoller("uefa.champions").PollAsync(CancellationToken.None);

        Assert.Equal([("uefa.champions", Yesterday), ("uefa.champions", Today)], _provider.Requests);
        var updated = await _store.GetAsync("uefa.champions", Yesterday);
        Assert.Equal(MatchStatus.Finished, Assert.Single(updated!.Scoreboard.Matches).Status);
    }

    [Fact]
    public async Task Publishes_changes_compared_to_the_previous_snapshot()
    {
        var match = Match(MatchStatus.Live, Now.AddMinutes(-20), score: new Score(0, 0));
        await _store.SetAsync(new ScoreboardSnapshot(Scoreboard("tur.1", Today, match), Now.AddSeconds(-30)));
        _provider.Returns(Scoreboard("tur.1", Today, match with { Score = new Score(1, 0) }));

        await CreatePoller("tur.1").PollAsync(CancellationToken.None);

        var change = Assert.Single(_publisher.Published);
        Assert.Equal(MatchChangeKind.Score, change.Kinds);
        Assert.Equal(new Score(1, 0), change.Match.Score);
    }

    [Fact]
    public async Task First_poll_publishes_nothing()
    {
        _provider.Returns(Scoreboard("tur.1", Today, Match(MatchStatus.Live, Now.AddMinutes(-20))));

        await CreatePoller("tur.1").PollAsync(CancellationToken.None);

        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Publish_failure_does_not_fail_the_poll()
    {
        var match = Match(MatchStatus.Live, Now.AddMinutes(-20), score: new Score(0, 0));
        await _store.SetAsync(new ScoreboardSnapshot(Scoreboard("tur.1", Today, match), Now.AddSeconds(-30)));
        _provider.Returns(Scoreboard("tur.1", Today, match with { Score = new Score(1, 0) }));
        _publisher.Fail = true;

        var delay = await CreatePoller("tur.1").PollAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
        Assert.Equal(new Score(1, 0), Assert.Single((await _store.GetAsync("tur.1", Today))!.Scoreboard.Matches).Score);
    }

    [Fact]
    public async Task After_a_restart_checks_yesterday_once_for_games_still_running()
    {
        // Restarted at 01:30 with nothing cached: a game from yesterday evening may still be in extra time.
        _provider.Returns(Scoreboard("uefa.champions", Yesterday, Match(MatchStatus.Live, Now.AddMinutes(-150))));
        _provider.Returns(Scoreboard("uefa.champions", Today));

        var delay = await CreatePoller("uefa.champions").PollAsync(CancellationToken.None);

        Assert.Equal([("uefa.champions", Yesterday), ("uefa.champions", Today)], _provider.Requests);
        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public async Task Stops_polling_yesterday_once_its_matches_are_finished()
    {
        await _store.SetAsync(new ScoreboardSnapshot(Scoreboard("uefa.champions", Yesterday, Match(MatchStatus.Finished, Now.AddMinutes(-150))), Now.AddMinutes(-5)));
        _provider.Returns(Scoreboard("uefa.champions", Today));

        await CreatePoller("uefa.champions").PollAsync(CancellationToken.None);

        Assert.Equal([("uefa.champions", Today)], _provider.Requests);
    }

    [Fact]
    public async Task Watched_live_match_gets_its_detail_refreshed_and_pushed()
    {
        var live = Match(MatchStatus.Live, Now.AddMinutes(-30), score: new Score(1, 0));
        _provider.Returns(Scoreboard("tur.1", Today, live));
        _provider.Returns(Scoreboard("tur.1", Yesterday));
        _provider.Returns(new MatchDetail(live, [new MatchEvent(MatchEventType.YellowCard, "12'", TeamSide.Home, "Player", null)], []));
        _viewers.Watch("connection", "tur.1", live.Id);

        await CreatePoller("tur.1").PollAsync(CancellationToken.None);

        var pushed = Assert.Single(_publisher.PublishedDetails);
        Assert.Equal(live.Id, pushed.Detail.Match.Id);
        Assert.Single(pushed.Detail.Events);
        Assert.Same(pushed, await _store.GetDetailAsync("tur.1", live.Id));
    }

    [Fact]
    public async Task Details_are_only_refreshed_for_matches_someone_is_watching_and_that_are_in_play()
    {
        var live = Match(MatchStatus.Live, Now.AddMinutes(-30), score: new Score(1, 0));
        var finished = Match(MatchStatus.Finished, Now.AddHours(-3), score: new Score(2, 2));
        _provider.Returns(Scoreboard("tur.1", Today, live, finished));
        _provider.Returns(Scoreboard("tur.1", Yesterday));
        _viewers.Watch("connection", "tur.1", finished.Id);

        await CreatePoller("tur.1").PollAsync(CancellationToken.None);

        Assert.Empty(_provider.DetailRequests);
        Assert.Empty(_publisher.PublishedDetails);
    }

    [Fact]
    public async Task Failing_detail_refresh_does_not_fail_the_poll()
    {
        var live = Match(MatchStatus.Live, Now.AddMinutes(-30), score: new Score(1, 0));
        _provider.Returns(Scoreboard("tur.1", Today, live));
        _provider.Returns(Scoreboard("tur.1", Yesterday));
        _provider.FailDetails = true;
        _viewers.Watch("connection", "tur.1", live.Id);

        var delay = await CreatePoller("tur.1").PollAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
        Assert.Empty(_publisher.PublishedDetails);
    }
}
