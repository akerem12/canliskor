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
    private readonly PushSubscriberRegistry _subscribers = new(new FakePushSubscriberStore());
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
        Assert.Equal("⏰ Kick-off in 30 minutes", reminder.Title);
        Assert.Equal("Home - Away", reminder.Body);
        Assert.Equal($"/?league=tur.1&match={match.Id}", reminder.Url);
    }

    [Fact]
    public async Task Says_how_long_is_left_when_the_reminder_is_late()
    {
        Playing(TestData.Match(MatchStatus.Scheduled, Now.AddMinutes(12)));
        Subscribe(teamIds: ["2"], language: "tr");

        await _notifier.RunAsync(CancellationToken.None);

        var (_, reminder) = Assert.Single(_sender.Sent);
        Assert.Equal("⏰ Maça 12 Dakika Kaldı", reminder.Title);
        Assert.Equal("Home - Away", reminder.Body);
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
    public async Task Stays_quiet_for_a_match_that_is_off(MatchStatus status)
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
        Assert.Equal("📋 Line-ups are out", lineups.Title);
        Assert.Equal("Home - Away", lineups.Body);
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

    [Fact]
    public async Task Announces_the_kickoff_and_each_goal_once_with_the_scoring_team_crest()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["2"]);

        await _notifier.RunAsync(CancellationToken.None);

        var (_, kickoff) = Assert.Single(_sender.Sent);
        Assert.Equal("Kick-off: Home - Away", kickoff.Title);
        Assert.Equal("The match is under way.", kickoff.Body);
        // The subscriber's own team is the away side.
        Assert.Equal(AwayCrest, kickoff.Icon);

        _provider.Returns(Scored(match, Event(MatchEventType.Goal, "12'", TeamSide.Home, "Player A", "g1")));
        for (var round = 0; round < 3; round++)
        {
            _time.Advance(TimeSpan.FromSeconds(15));
            await _notifier.RunAsync(CancellationToken.None);
        }

        Assert.Equal(2, _sender.Sent.Count);
        var goal = _sender.Sent[1].Notification;
        Assert.Equal("Home [1] - 0 Away", goal.Title);
        Assert.Equal("⚽ 12' Player A", goal.Body);
        Assert.Equal(HomeCrest, goal.Icon);
        Assert.Equal($"/?league=tur.1&match={match.Id}", goal.Url);
    }

    [Fact]
    public async Task Writes_penalties_own_goals_and_misses_in_turkish()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(matchIds: [match.Id], language: "tr");
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Scored(match,
            Event(MatchEventType.PenaltyGoal, "10'", TeamSide.Home, "P1", "e1"),
            // Scored by a home player, so it counts for the away team.
            Event(MatchEventType.OwnGoal, "20'", TeamSide.Away, "P2", "e2"),
            Event(MatchEventType.PenaltyMissed, "45'+2'", TeamSide.Home, "P3", "e3"),
            Event(MatchEventType.RedCard, "50'", TeamSide.Away, "P4", "e4")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(
        [
            ("Maç Başladı: Home - Away", "Karşılaşmada ilk düdük çaldı."),
            ("Home [1] - 0 Away", "⚽ 10' P1 (P)"),
            ("Home 1 - [1] Away", "⚽ 20' P2 (K.K.)"),
            ("❌ Penaltı Kaçtı! Home - Away", "45'+2' P3 (Home)"),
            ("🟥 Kırmızı Kart! Home - Away", "50' P4 (Away)"),
        ], _sender.Sent.Select(s => (s.Notification.Title, s.Notification.Body)));
        // The own goal shows the crest of the team it counts for.
        Assert.Equal(AwayCrest, _sender.Sent[2].Notification.Icon);
    }

    [Fact]
    public async Task Announces_a_penalty_given()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"], language: "tr");
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Detail(match, new MatchEvent(MatchEventType.PenaltyAwarded, "34'", TeamSide.Away, null, "Player C", Id: "p1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        var penalty = _sender.Sent[^1].Notification;
        Assert.Equal("⚠️ Penaltı! Home - Away", penalty.Title);
        Assert.Equal("34' Away penaltı kazandı", penalty.Body);
        Assert.Equal(AwayCrest, penalty.Icon);
    }

    [Fact]
    public async Task A_penalty_given_is_one_notification_when_its_line_gets_an_id_later()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        // A fresh commentary line comes without its play's id (Real Madrid - Sevilla, 18 Oct 2026)...
        _provider.Returns(Detail(match, new MatchEvent(MatchEventType.PenaltyAwarded, "61'", TeamSide.Home, null, "Castrín", Id: "commentary:64")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        // ...and gets it a little later.
        _provider.Returns(Detail(match, new MatchEvent(MatchEventType.PenaltyAwarded, "61'", TeamSide.Home, null, "Castrín", Id: "53936840")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Single(_sender.Sent, s => s.Notification.Title.StartsWith("⚠️"));
    }

    [Fact]
    public async Task A_goal_taken_back_is_announced_with_the_score_change()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        var scored = match with { Score = new Score(1, 0) };
        _provider.Returns(Detail(scored, Event(MatchEventType.Goal, "12'", TeamSide.Home, "Player A", "g1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        // VAR: the goal is gone from the events and from the score.
        _provider.Returns(Detail(match with { Score = new Score(0, 0) }));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(15));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(3, _sender.Sent.Count);
        var disallowed = _sender.Sent[^1].Notification;
        Assert.Equal("Home 0 - 0 Away", disallowed.Title);
        Assert.Equal("❌ Goal disallowed · 12' Player A", disallowed.Body);
        Assert.Equal(HomeCrest, disallowed.Icon);
    }

    [Fact]
    public async Task A_goal_entered_twice_is_announced_once_and_taken_back_once()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        // Fenerbahçe - Alanyaspor: one goal, entered at 17' and again at 19'; the score says 1-0.
        _provider.Returns(Detail(match with { Score = new Score(1, 0) },
            Event(MatchEventType.Goal, "17'", TeamSide.Home, "Player A", "a"),
            Event(MatchEventType.Goal, "19'", TeamSide.Home, "Player A", "b")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        // Then both entries are removed (VAR).
        _provider.Returns(Detail(match));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(
        [
            "Kick-off: Home - Away",
            "Home [1] - 0 Away",
            "Home 0 - 0 Away",
        ], _sender.Sent.Select(s => s.Notification.Title));
    }

    [Fact]
    public async Task A_goal_whose_first_entry_is_replaced_by_a_second_is_neither_announced_again_nor_taken_back()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        var scored = match with { Score = new Score(1, 0) };
        _provider.Returns(Detail(scored, Event(MatchEventType.Goal, "17'", TeamSide.Home, "Player A", "a")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Detail(scored, Event(MatchEventType.Goal, "19'", TeamSide.Home, "Player A", "b")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task Two_quick_goals_by_one_player_are_two_goals_when_the_score_says_so()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Detail(match with { Score = new Score(2, 0) },
            Event(MatchEventType.Goal, "17'", TeamSide.Home, "Player A", "a"),
            Event(MatchEventType.Goal, "19'", TeamSide.Home, "Player A", "b")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(["Home [1] - 0 Away", "Home [2] - 0 Away"], _sender.Sent.Skip(1).Select(s => s.Notification.Title));
    }

    [Fact]
    public async Task A_goal_the_score_has_but_no_entry_names_is_announced_without_scorer_after_a_while()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Detail(match with { Score = new Score(0, 1) }));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Single(_sender.Sent);

        _time.Advance(MatchNotifier.NameWait);
        await _notifier.RunAsync(CancellationToken.None);
        var goal = _sender.Sent[^1].Notification;
        Assert.Equal("Home 0 - [1] Away", goal.Title);
        Assert.Equal("⚽ Goal", goal.Body);
        Assert.Equal(AwayCrest, goal.Icon);

        // The entry comes after all: no second notification for the same goal.
        _provider.Returns(Scored(match, Event(MatchEventType.Goal, "20'", TeamSide.Away, "Player B", "g1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task A_goal_entry_waits_for_the_score_and_is_named_once_it_counts()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        // The entry is there before the score: not counted yet.
        _provider.Returns(Detail(match, Event(MatchEventType.Goal, "12'", TeamSide.Home, "Player A", "g1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Single(_sender.Sent);

        // The scoreboard shows the goal, so the detail is loaded again.
        _time.Advance(TimeSpan.FromSeconds(15));
        Playing(match with { Score = new Score(1, 0) });
        _provider.Returns(Scored(match, Event(MatchEventType.Goal, "12'", TeamSide.Home, "Player A", "g1")));
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Equal("⚽ 12' Player A", _sender.Sent[^1].Notification.Body);
    }

    [Fact]
    public async Task A_goal_missing_from_events_that_do_not_add_up_to_the_score_is_not_taken_back()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        var scored = match with { Score = new Score(1, 0) };
        _provider.Returns(Detail(scored, Event(MatchEventType.Goal, "12'", TeamSide.Home, "Player A", "g1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        // The events came back empty while the score still says 1-0: a hiccup, not VAR.
        _provider.Returns(Detail(scored));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task A_missed_penalty_entered_again_under_another_id_is_not_sent_again()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Detail(match, Event(MatchEventType.PenaltyMissed, "17'", TeamSide.Home, "Taker", "taker-entry")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        // Later the goalkeeper's save of the same penalty, a new entry: the same miss.
        _provider.Returns(Detail(match, new MatchEvent(MatchEventType.PenaltyMissed, "17'", TeamSide.Home, null, null, Id: "keeper-entry")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
        Assert.Equal("17' Taker (Home)", _sender.Sent[^1].Notification.Body);
    }

    [Fact]
    public async Task A_miss_known_only_from_the_goalkeepers_save_waits_for_the_takers_name()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Detail(match, new MatchEvent(MatchEventType.PenaltyMissed, "17'", TeamSide.Home, null, null, Id: "keeper-entry")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(15));
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Single(_sender.Sent);

        // The taker's entry comes before the wait is over.
        _provider.Returns(Detail(match, Event(MatchEventType.PenaltyMissed, "17'", TeamSide.Home, "Taker", "taker-entry")));
        _time.Advance(TimeSpan.FromSeconds(15));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
        Assert.Equal("17' Taker (Home)", _sender.Sent[^1].Notification.Body);
    }

    [Fact]
    public async Task A_miss_whose_taker_never_comes_is_sent_with_the_team_after_the_wait()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Detail(match, new MatchEvent(MatchEventType.PenaltyMissed, "17'", TeamSide.Home, null, null, Id: "keeper-entry")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        _time.Advance(MatchNotifier.NameWait);
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal("17' Home", _sender.Sent[^1].Notification.Body);
    }

    [Fact]
    public async Task Yellow_cards_and_substitutions_are_not_notified()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Detail(match,
            Event(MatchEventType.YellowCard, "5'", TeamSide.Home, "P1", "y1"),
            Event(MatchEventType.Substitution, "6'", TeamSide.Home, "P2", "s1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task After_a_restart_mid_match_only_what_happens_from_then_on_is_sent()
    {
        var match = Playing(InPlay("60'", new Score(1, 0)));
        var goal = Event(MatchEventType.Goal, "12'", TeamSide.Home, "Player A", "g1");
        _provider.Returns(Detail(match, goal));
        Subscribe(teamIds: ["1"]);

        // No kickoff and no goal: both happened before this server was watching.
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Empty(_sender.Sent);

        _provider.Returns(Detail(match, goal, Event(MatchEventType.RedCard, "70'", TeamSide.Away, "Player B", "r1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        var (_, card) = Assert.Single(_sender.Sent);
        Assert.Equal("🟥 Red card! Home - Away", card.Title);
        Assert.Equal("70' Player B (Away)", card.Body);
        Assert.Equal(AwayCrest, card.Icon);
    }

    [Fact]
    public async Task A_corrected_event_is_not_sent_again()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        _provider.Returns(Scored(match, Event(MatchEventType.Goal, "12'", TeamSide.Home, "Player A", "g1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        // The provider credits the goal to someone else a minute later: same event, same id.
        _provider.Returns(Scored(match, Event(MatchEventType.Goal, "13'", TeamSide.Home, "Player C", "g1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task Someone_who_follows_later_gets_new_events_but_not_old_ones()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Scored(match, Event(MatchEventType.Goal, "2'", TeamSide.Home, "Player A", "g1")));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Equal(2, _sender.Sent.Count);

        _time.Advance(EventNewsForAndABit);
        Subscribe(matchIds: [match.Id], endpoint: "https://fcm.googleapis.com/fcm/send/late");
        _provider.Returns(Scored(match,
            Event(MatchEventType.Goal, "2'", TeamSide.Home, "Player A", "g1"),
            Event(MatchEventType.Goal, "14'", TeamSide.Away, "Player B", "g2")));
        await _notifier.RunAsync(CancellationToken.None);

        var late = _sender.Sent.Where(s => s.Subscriber.Endpoint.EndsWith("late")).Select(s => s.Notification.Title);
        Assert.Equal(["Home 1 - [1] Away"], late);
    }

    [Fact]
    public async Task A_goal_that_comes_with_the_final_whistle_is_still_sent_and_then_the_match_is_left_alone()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);
        await _notifier.RunAsync(CancellationToken.None);

        var finished = Playing(match with { Status = MatchStatus.Finished, Clock = "90'+4'", Score = new Score(1, 0) });
        _provider.Returns(Detail(finished, Event(MatchEventType.Goal, "90'+3'", TeamSide.Home, "Player A", "g1")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Equal("Home [1] - 0 Away", _sender.Sent[^2].Notification.Title);

        Assert.Equal("Full-time: Home 1 - 0 Away", _sender.Sent[^1].Notification.Title);

        // Kept a while for phones the full time didn't reach, then nothing is looked at any more.
        _time.Advance(EventNewsForAndABit);
        await _notifier.RunAsync(CancellationToken.None);
        var requests = _provider.DetailRequests.Count;
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        Assert.Equal(requests, _provider.DetailRequests.Count);
    }

    [Fact]
    public async Task Announces_half_time_the_second_half_and_full_time_with_the_score()
    {
        var match = Playing(InPlay("1'"));
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["2"], language: "tr");
        await _notifier.RunAsync(CancellationToken.None);

        Playing(match with { Status = MatchStatus.HalfTime, Clock = "45'+2'", Score = new Score(1, 1) });
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(15));
        await _notifier.RunAsync(CancellationToken.None);

        Playing(match with { Status = MatchStatus.Live, Clock = "46'", Score = new Score(1, 1) });
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);

        Playing(match with { Status = MatchStatus.Finished, Clock = "90'+4'", Score = new Score(2, 1) });
        _time.Advance(TimeSpan.FromMinutes(1));
        await _notifier.RunAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(15));
        await _notifier.RunAsync(CancellationToken.None);

        Assert.Equal(
        [
            ("Maç Başladı: Home - Away", "Karşılaşmada ilk düdük çaldı."),
            ("Devre Arası: Home 1 - 1 Away", "İlk yarı sona erdi."),
            ("İkinci Yarı Başladı: Home 1 - 1 Away", "Karşılaşmada ikinci yarı başladı."),
            ("Maç Sonu: Home 2 - 1 Away", "Karşılaşma sona erdi."),
        ], _sender.Sent.Select(s => (s.Notification.Title, s.Notification.Body)));
        Assert.All(_sender.Sent, s => Assert.Equal(AwayCrest, s.Notification.Icon));
    }

    [Fact]
    public async Task A_server_started_during_half_time_does_not_announce_it()
    {
        var match = Playing(InPlay("45'+2'", new Score(0, 0)) with { Status = MatchStatus.HalfTime });
        _provider.Returns(Detail(match));
        Subscribe(teamIds: ["1"]);

        await _notifier.RunAsync(CancellationToken.None);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task A_match_in_play_with_no_followers_costs_nothing()
    {
        Playing(InPlay("30'"));
        Subscribe(teamIds: ["999"]);

        await _notifier.RunAsync(CancellationToken.None);

        Assert.Empty(_provider.DetailRequests);
    }

    [Theory]
    [InlineData("tr", "34'", "⚽ 34'")]
    [InlineData("tr", "", "⚽ Gol")]
    [InlineData("en", "", "⚽ Goal")]
    public void A_goal_whose_scorer_is_not_named_yet_shows_no_name(string language, string clock, string body)
    {
        var match = InPlay("35'", new Score(1, 0));
        var goal = new MatchEvent(MatchEventType.Goal, clock, TeamSide.Home, null, null);

        var notification = MatchNotificationText.Event(match, goal, new Score(1, 0), "k", language)!;

        Assert.Equal("Home [1] - 0 Away", notification.Title);
        Assert.Equal(body, notification.Body);
        Assert.Equal(HomeCrest, notification.Icon);
    }

    private const string HomeCrest = "https://a.espncdn.com/i/teamlogos/soccer/500/1.png";
    private const string AwayCrest = "https://a.espncdn.com/i/teamlogos/soccer/500/2.png";

    private static readonly TimeSpan EventNewsForAndABit = MatchNotifier.EventNewsFor + TimeSpan.FromMinutes(1);

    /// <summary>A match in play, with crests, kicked off at <paramref name="clock"/> minutes ago (roughly).</summary>
    private static Match InPlay(string clock, Score? score = null)
    {
        var match = TestData.Match(MatchStatus.Live, Now.AddMinutes(-1), score: score ?? new Score(0, 0));
        return match with
        {
            Clock = clock,
            HomeTeam = match.HomeTeam with { LogoUrl = HomeCrest },
            AwayTeam = match.AwayTeam with { LogoUrl = AwayCrest },
        };
    }

    private static MatchEvent Event(MatchEventType type, string clock, TeamSide side, string player, string id) =>
        new(type, clock, side, player, null, Id: id);

    private static MatchDetail Detail(Match match, params MatchEvent[] events) => new(match, events, []);

    /// <summary>The detail with the score its goals make, as the provider sends it.</summary>
    private static MatchDetail Scored(Match match, params MatchEvent[] events)
    {
        bool IsGoal(MatchEvent e) => e.Type is MatchEventType.Goal or MatchEventType.PenaltyGoal or MatchEventType.OwnGoal;
        var score = new Score(events.Count(e => IsGoal(e) && e.Side == TeamSide.Home), events.Count(e => IsGoal(e) && e.Side == TeamSide.Away));
        return Detail(match with { Score = score }, events);
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
        // The fake store answers at once, so there is nothing to wait for.
        _subscribers.RegisterAsync(new PushSubscriber(endpoint, "p256dh", "auth", language,
            new HashSet<string>(teamIds ?? []), new HashSet<string>(matchIds ?? []), kickoffReminder, lineupAlerts)).GetAwaiter().GetResult();

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
