using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Notifications;
using CanliSkor.Core.Options;
using CanliSkor.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CanliSkor.Core.Tests.Notifications;

public class MatchNotifierTests
{
    // 16:00 UTC = 19:00 in Istanbul.
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 9);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly FakeMatchStore _store = new();
    private readonly FakeFootballDataProvider _provider = new();
    private readonly FakePushSender _sender = new();
    private readonly PushSubscriberRegistry _subscribers = new();
    private readonly MatchNotifier _notifier;

    public MatchNotifierTests()
    {
        var followed = TestOptions.Leagues("tur.1");
        _notifier = new MatchNotifier(
            _subscribers,
            new NotificationLog(),
            _sender,
            _store,
            new MatchDetailService(_provider, _store, new OnDemandFetchGate(), followed,
                new StaticOptionsMonitor<PollingOptions>(TestOptions.Polling()), _time, NullLogger<MatchDetailService>.Instance),
            followed,
            _time,
            NullLogger<MatchNotifier>.Instance);
    }

    [Fact]
    public async Task Reminds_half_an_hour_before_kickoff_and_only_once()
    {
        var match = Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(31)));
        Subscribe(teamIds: ["1"]);

        await _notifier.RunAsync(CancellationToken.None);
        Assert.Empty(_sender.Sent);

        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(30));
        await _notifier.RunAsync(CancellationToken.None);

        var (_, reminder) = Assert.Single(_sender.Sent);
        Assert.Equal("Match Reminder: Home vs Away", reminder.Title);
        Assert.Equal("Kickoff in 30 minutes! Tap to view pre-match stats and odds.", reminder.Body);
        Assert.Equal($"/?league=tur.1&match={match.Id}", reminder.Url);
    }

    [Fact]
    public async Task Says_how_long_is_left_when_the_reminder_is_late()
    {
        Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(12)));
        Subscribe(teamIds: ["2"], language: "tr");

        await _notifier.RunAsync(CancellationToken.None);

        var (_, reminder) = Assert.Single(_sender.Sent);
        Assert.Equal("Maç hatırlatması: Home - Away", reminder.Title);
        Assert.StartsWith("Maç 12 dakika sonra başlıyor!", reminder.Body);
    }

    [Fact]
    public async Task Follows_a_kickoff_that_is_moved()
    {
        var match = Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(20)));
        Subscribe(matchIds: [match.Id]);
        await _notifier.RunAsync(CancellationToken.None);

        // Put back by an hour: no reminder until half an hour before the new time, then one more.
        Playing(match with { KickoffUtc = Now.AddMinutes(80) });
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Single(_sender.Sent);

        _time.Advance(TimeSpan.FromMinutes(50));
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Equal(2, _sender.Sent.Count);
    }

    [Theory]
    [InlineData(MatchStatus.Postponed)]
    [InlineData(MatchStatus.Cancelled)]
    [InlineData(MatchStatus.Live)]
    public async Task Stays_quiet_for_a_match_that_is_off_or_already_running(MatchStatus status)
    {
        var match = Playing(TestData.Match(status, Now.AddMinutes(10)));
        _provider.Returns(Detail(match, withLineups: true));
        Subscribe(teamIds: ["1"]);

        await _notifier.RunAsync(CancellationToken.None);

        Assert.Empty(_sender.Sent);
        Assert.Empty(_provider.DetailRequests);
    }

    [Fact]
    public async Task Only_tells_those_who_follow_the_match()
    {
        Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(10)));
        Subscribe(teamIds: ["999"]);

        await _notifier.RunAsync(CancellationToken.None);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Announces_the_lineups_once_when_they_appear()
    {
        var match = Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(70)));
        _provider.Returns(Detail(match, withLineups: false));
        Subscribe(teamIds: ["1"]);

        await _notifier.RunAsync(CancellationToken.None);
        Assert.Empty(_sender.Sent);

        _provider.Returns(Detail(match, withLineups: true));
        for (var round = 0; round < 4; round++)
        {
            _time.Advance(TimeSpan.FromSeconds(30));
            await _notifier.RunAsync(CancellationToken.None);
        }

        var (_, lineups) = Assert.Single(_sender.Sent);
        Assert.Equal("Lineups Confirmed: Home vs Away", lineups.Title);
        Assert.Equal($"/?league=tur.1&match={match.Id}&tab=lineups", lineups.Url);
    }

    [Fact]
    public async Task Looks_for_lineups_once_a_minute_and_not_before_they_are_due()
    {
        var match = Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(76)));
        _provider.Returns(Detail(match, withLineups: false));
        Subscribe(teamIds: ["1"]);

        await _notifier.RunAsync(CancellationToken.None);
        Assert.Empty(_provider.DetailRequests);

        // Five rounds, half a minute apart, from 75 minutes before kickoff: looked at the start and after each full minute.
        for (var round = 0; round < 5; round++)
        {
            _time.Advance(TimeSpan.FromSeconds(round == 0 ? 60 : 30));
            await _notifier.RunAsync(CancellationToken.None);
        }

        Assert.Equal(3, _provider.DetailRequests.Count);
    }

    [Fact]
    public async Task Respects_what_each_subscriber_switched_on()
    {
        var match = Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(25)));
        _provider.Returns(Detail(match, withLineups: true));
        Subscribe(teamIds: ["1"], endpoint: "https://fcm.googleapis.com/reminder-only", lineupAlerts: false);
        Subscribe(teamIds: ["1"], endpoint: "https://fcm.googleapis.com/lineups-only", kickoffReminder: false);

        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
        Assert.Contains(_sender.Sent, s => s.Subscriber.Endpoint.EndsWith("reminder-only") && s.Notification.Tag.EndsWith(":reminder"));
        Assert.Contains(_sender.Sent, s => s.Subscriber.Endpoint.EndsWith("lineups-only") && s.Notification.Tag.EndsWith(":lineups"));
    }

    [Fact]
    public async Task Tries_again_after_a_failure_and_forgets_a_subscription_that_is_gone()
    {
        Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(25)));
        Subscribe(teamIds: ["1"], lineupAlerts: false);

        _sender.Outcome = PushOutcome.Failed;
        await _notifier.RunAsync(CancellationToken.None);
        _sender.Outcome = PushOutcome.Gone;
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
        Assert.Empty(_subscribers.All());
    }

    [Fact]
    public async Task A_provider_failure_while_looking_for_lineups_does_not_stop_the_reminder_of_another_match()
    {
        Playing(
            TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(60)),
            TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(20)));
        _provider.FailDetails = true;
        Subscribe(teamIds: ["1"]);

        await _notifier.RunAsync(CancellationToken.None);

        var (_, reminder) = Assert.Single(_sender.Sent);
        Assert.EndsWith(":reminder", reminder.Tag);
    }

    private Match Playing(params Match[] matches)
    {
        _store.SetAsync(new ScoreboardSnapshot(TestData.Scoreboard("tur.1", Today, matches), _time.GetUtcNow()));
        return matches[0];
    }

    private void Subscribe(
        string[]? teamIds = null,
        string[]? matchIds = null,
        string language = "en",
        string endpoint = "https://fcm.googleapis.com/fcm/send/abc",
        bool kickoffReminder = true,
        bool lineupAlerts = true) =>
        _subscribers.Register(new PushSubscriber(endpoint, "p256dh", "auth", language,
            new HashSet<string>(teamIds ?? []), new HashSet<string>(matchIds ?? []), kickoffReminder, lineupAlerts));

    private static MatchDetail Detail(Match match, bool withLineups)
    {
        var team = new TeamLineup("4-4-2", null, [], []);
        return new MatchDetail(match, [], [], withLineups ? new MatchLineups(team, team) : null);
    }

    private sealed class FakePushSender : IPushSender
    {
        public List<(PushSubscriber Subscriber, PushNotification Notification)> Sent { get; } = [];

        public PushOutcome Outcome { get; set; } = PushOutcome.Sent;

        public string PublicKey => "key";

        public Task<PushOutcome> SendAsync(PushSubscriber subscriber, PushNotification notification, CancellationToken cancellationToken = default)
        {
            Sent.Add((subscriber, notification));
            return Task.FromResult(Outcome);
        }
    }
}

public class PushSubscriberTests
{
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://wns2-am3p.notify.windows.com/w/?token=abc", true)]
    [InlineData("https://web.push.apple.com/abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com.example.org/abc", false)]
    [InlineData("https://localhost:5272/health", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void Only_browser_push_services_are_accepted_as_endpoints(string? endpoint, bool accepted) =>
        Assert.Equal(accepted, PushSubscriber.IsPushServiceEndpoint(endpoint));
}
