using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Services;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// One round of match notifications. For every match about to kick off that a subscriber follows: the reminder
/// half an hour before kickoff, and "line-ups are out" as soon as the provider has both starting elevens. For
/// every followed match in play: the kickoff, then each goal, missed penalty and red card as it appears in the
/// match's events. Works from the scoreboards the poller keeps in the store, so a kickoff that was moved moves its
/// reminder, and a match postponed or cancelled gets none. Kept separate from the hosted service so it can be
/// unit-tested.
/// </summary>
public sealed partial class MatchNotifier(
    PushSubscriberRegistry subscribers,
    NotificationLog log,
    IPushSender sender,
    IMatchStore store,
    MatchDetailService details,
    IOptionsMonitor<FootballOptions> footballOptions,
    TimeProvider timeProvider,
    ILogger<MatchNotifier> logger)
{
    public static readonly TimeSpan ReminderLead = TimeSpan.FromMinutes(30);

    /// <summary>Line-ups come out about an hour before kickoff; looking starts a little earlier.</summary>
    public static readonly TimeSpan LineupWatchLead = TimeSpan.FromMinutes(75);

    /// <summary>How often one match's line-ups are looked for. Each look is a request to the provider.</summary>
    public static readonly TimeSpan LineupCheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A match first seen in play by this minute counts as just kicked off: its kickoff is announced and all its
    /// events are news. Seen later (the server restarted mid-match), what happened so far is taken as known.
    /// </summary>
    public const int KickoffWindowMinutes = 5;

    /// <summary>
    /// An event is sent for this long after it was first seen: to anyone who follows the match by then, and again to
    /// a phone the push service couldn't reach. After that it is no news any more.
    /// </summary>
    public static readonly TimeSpan EventNewsFor = TimeSpan.FromMinutes(10);

    private static readonly Score NoGoals = new(0, 0);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var everyone = subscribers.All();
        if (everyone.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        log.ForgetOld(now);

        foreach (var match in await UpcomingMatchesAsync(now, cancellationToken))
        {
            var followers = everyone.Where(s => s.Follows(match)).ToList();
            if (followers.Count == 0)
            {
                continue;
            }

            var untilKickoff = match.KickoffUtc - now;
            if (untilKickoff <= ReminderLead)
            {
                // The kickoff time is part of the name: a match moved to another time is reminded of again.
                var reminder = $"reminder:{match.Id}:{match.KickoffUtc.ToUnixTimeSeconds()}";
                await SendAsync(reminder, followers.Where(s => s.KickoffReminder),
                    s => MatchNotificationText.KickoffReminder(match, untilKickoff, s.Language, CrestFor(s, match)), now, cancellationToken);
            }

            var lineups = $"lineups:{match.Id}";
            var waiting = followers.Where(s => s.LineupAlerts && !log.WasSent(lineups, s.Endpoint)).ToList();
            if (waiting.Count > 0 && await LineupsAnnouncedAsync(match, now, cancellationToken))
            {
                await SendAsync(lineups, waiting,
                    s => MatchNotificationText.LineupsAnnounced(match, untilKickoff, s.Language, CrestFor(s, match)), now, cancellationToken);
            }
        }

        foreach (var match in await MatchesInPlayAsync(now, cancellationToken))
        {
            var followers = everyone.Where(s => s.Follows(match)).ToList();
            if (followers.Count > 0)
            {
                await NotifyInPlayAsync(match, followers, now, cancellationToken);
            }
        }
    }

    private async Task NotifyInPlayAsync(Match match, List<PushSubscriber> followers, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var live = log.LiveMatch(match.Id);
        if (live is null)
        {
            var justStarted = match.Status == MatchStatus.Live && (Minute(match.Clock) ?? 0) <= KickoffWindowMinutes;
            if (justStarted)
            {
                live = log.StartLiveMatch(match.Id, [], now, sawKickoff: true);
            }
            else
            {
                // Without the events so far nothing can be told apart as new: wait for the next round.
                var known = await DetailAsync(match, cancellationToken);
                if (known is null)
                {
                    return;
                }

                live = log.StartLiveMatch(match.Id, known.Events.Select(EventKey), now, sawKickoff: false);
            }
        }

        if (live.SawKickoff && now - live.StartedAt <= EventNewsFor)
        {
            await SendAsync($"kickoff:{match.Id}", followers,
                s => MatchNotificationText.Kickoff(match, s.Language, CrestFor(s, match)), now, cancellationToken);
        }

        var detail = await DetailAsync(match, cancellationToken);
        if (detail is not null)
        {
            foreach (var (matchEvent, score) in WithRunningScore(detail.Events))
            {
                if (!IsNotified(matchEvent.Type))
                {
                    continue;
                }

                var key = EventKey(matchEvent);
                if (now - live.FirstSeen(key, now) > EventNewsFor)
                {
                    continue;
                }

                await SendAsync($"event:{match.Id}:{key}", followers,
                    s => MatchNotificationText.Event(match, matchEvent, score, key, s.Language)!, now, cancellationToken);
            }
        }

        // The last round for a finished match: its final events have just been dealt with.
        if (match.Status == MatchStatus.Finished)
        {
            live.Over = true;
        }
    }

    private static bool IsNotified(MatchEventType type) => type is MatchEventType.Goal or MatchEventType.PenaltyGoal
        or MatchEventType.OwnGoal or MatchEventType.PenaltyMissed or MatchEventType.RedCard;

    /// <summary>
    /// What tells an event apart: the provider's id, which survives corrections (another scorer, another minute),
    /// else what the event is.
    /// </summary>
    internal static string EventKey(MatchEvent matchEvent) =>
        matchEvent.Id ?? $"{matchEvent.Type}:{matchEvent.Side}:{matchEvent.PlayerId ?? matchEvent.Player}:{matchEvent.Clock}";

    /// <summary>Each event with the score right after it. Events are in match order.</summary>
    private static IEnumerable<(MatchEvent Event, Score Score)> WithRunningScore(IReadOnlyList<MatchEvent> events)
    {
        var score = NoGoals;
        foreach (var matchEvent in events)
        {
            if (matchEvent.Type is MatchEventType.Goal or MatchEventType.PenaltyGoal or MatchEventType.OwnGoal)
            {
                score = matchEvent.Side == TeamSide.Home ? score with { Home = score.Home + 1 } : score with { Away = score.Away + 1 };
            }

            yield return (matchEvent, score);
        }
    }

    /// <summary>The minute a clock shows, without added time: 57 for "57'", 45 for "45'+2'"; null without a clock.</summary>
    private static int? Minute(string? clock)
    {
        var digits = new string((clock ?? "").TakeWhile(char.IsAsciiDigit).ToArray());
        return int.TryParse(digits, out var minute) ? minute : null;
    }

    /// <summary>The crest of the subscriber's own team if it plays in the match, else the home team's.</summary>
    private static string? CrestFor(PushSubscriber subscriber, Match match) =>
        !subscriber.TeamIds.Contains(match.HomeTeam.Id) && subscriber.TeamIds.Contains(match.AwayTeam.Id)
            ? match.AwayTeam.LogoUrl
            : match.HomeTeam.LogoUrl;

    /// <summary>Best effort: null if the provider fails, and the next round tries again.</summary>
    private async Task<MatchDetail?> DetailAsync(Match match, CancellationToken cancellationToken)
    {
        try
        {
            // Cached for one poll interval, and reloaded as soon as the scoreboard shows a new score or status.
            return (await details.GetAsync(match.LeagueCode, match.Id, cancellationToken))?.Detail;
        }
        catch (FootballDataProviderException ex)
        {
            LogDetailFailed(ex, match.LeagueCode, match.Id);
            return null;
        }
    }

    /// <summary>
    /// Matches in play, and matches that ended since the last round (their last events and the final whistle may
    /// have come together).
    /// </summary>
    private async Task<IReadOnlyList<Match>> MatchesInPlayAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = IstanbulTime.DateOf(now);
        var matches = new Dictionary<string, Match>();

        foreach (var leagueCode in footballOptions.CurrentValue.Leagues)
        {
            // A match after midnight is still filed under the day it kicked off.
            foreach (var date in new[] { today.AddDays(-1), today })
            {
                var snapshot = await store.GetAsync(leagueCode, date, cancellationToken);
                foreach (var match in snapshot?.Scoreboard.Matches ?? [])
                {
                    if (match.Status.IsInPlay() || (match.Status == MatchStatus.Finished && log.LiveMatch(match.Id) is { Over: false }))
                    {
                        matches[$"{match.LeagueCode}/{match.Id}"] = match;
                    }
                }
            }
        }

        return [.. matches.Values];
    }

    /// <summary>Matches still to be played that kick off within <see cref="LineupWatchLead"/>.</summary>
    private async Task<IReadOnlyList<Match>> UpcomingMatchesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = IstanbulTime.DateOf(now);
        var matches = new Dictionary<string, Match>();

        foreach (var leagueCode in footballOptions.CurrentValue.Leagues)
        {
            // Around midnight the next kickoff may be filed under the day before or after.
            foreach (var date in new[] { today.AddDays(-1), today, today.AddDays(1) })
            {
                var snapshot = await store.GetAsync(leagueCode, date, cancellationToken);
                foreach (var match in snapshot?.Scoreboard.Matches ?? [])
                {
                    // Scheduled only: not postponed, not cancelled, not started.
                    if (match.Status == MatchStatus.Scheduled && match.KickoffUtc > now && match.KickoffUtc - now <= LineupWatchLead)
                    {
                        matches[$"{match.LeagueCode}/{match.Id}"] = match;
                    }
                }
            }
        }

        return [.. matches.Values];
    }

    private async Task<bool> LineupsAnnouncedAsync(Match match, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var last = log.LastLineupCheck(match.Id);
        if (last is not null && (last.Announced || now - last.At < LineupCheckInterval))
        {
            return last.Announced;
        }

        // Best effort: a failed look is repeated a minute later, and must not hold up the other matches.
        var announced = false;
        try
        {
            var snapshot = await details.RefreshAsync(match.LeagueCode, match.Id, cancellationToken);
            announced = snapshot?.Detail.Lineups is not null;
        }
        catch (FootballDataProviderException ex)
        {
            LogLineupCheckFailed(ex, match.LeagueCode, match.Id);
        }

        log.RecordLineupCheck(match.Id, new LineupCheck(now, announced));
        return announced;
    }

    private async Task SendAsync(
        string notification,
        IEnumerable<PushSubscriber> recipients,
        Func<PushSubscriber, PushNotification> write,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var subscriber in recipients)
        {
            if (log.WasSent(notification, subscriber.Endpoint))
            {
                continue;
            }

            var outcome = await sender.SendAsync(subscriber, write(subscriber), cancellationToken);
            if (outcome == PushOutcome.Gone)
            {
                await subscribers.RemoveAsync(subscriber.Endpoint, cancellationToken);
            }

            // A failed one stays open and is tried again on the next round.
            if (outcome != PushOutcome.Failed)
            {
                log.MarkSent(notification, subscriber.Endpoint, now);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading the events of match {MatchId} ({LeagueCode}) for notifications failed")]
    private partial void LogDetailFailed(Exception exception, string leagueCode, string matchId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Looking for the line-ups of match {MatchId} ({LeagueCode}) failed")]
    private partial void LogLineupCheckFailed(Exception exception, string leagueCode, string matchId);
}
