using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Services;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// One round of match notifications: for every match about to kick off that a subscriber follows, the reminder
/// half an hour before kickoff, and "line-ups are out" as soon as the provider has both starting elevens.
/// Works from the scoreboards the poller keeps in the store, so a kickoff that was moved moves its reminder, and a
/// match postponed or cancelled gets none. Kept separate from the hosted service so it can be unit-tested.
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
                    s => MatchNotificationText.KickoffReminder(match, untilKickoff, s.Language), now, cancellationToken);
            }

            var lineups = $"lineups:{match.Id}";
            var waiting = followers.Where(s => s.LineupAlerts && !log.WasSent(lineups, s.Endpoint)).ToList();
            if (waiting.Count > 0 && await LineupsAnnouncedAsync(match, now, cancellationToken))
            {
                await SendAsync(lineups, waiting,
                    s => MatchNotificationText.LineupsAnnounced(match, untilKickoff, s.Language), now, cancellationToken);
            }
        }
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
                subscribers.Remove(subscriber.Endpoint);
            }

            // A failed one stays open and is tried again on the next round.
            if (outcome != PushOutcome.Failed)
            {
                log.MarkSent(notification, subscriber.Endpoint, now);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Looking for the line-ups of match {MatchId} ({LeagueCode}) failed")]
    private partial void LogLineupCheckFailed(Exception exception, string leagueCode, string matchId);
}
