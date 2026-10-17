using CanliSkor.Core.Notifications;

namespace CanliSkor.Api.Workers;

/// <summary>
/// Thin hosting loop for <see cref="MatchNotifier"/>: right after every poll that changed a scoreboard (see
/// <see cref="NotificationSignal"/>), so a goal or a whistle goes out as soon as the poller has it, and every 15
/// seconds besides, for what no scoreboard shows (a red card, a penalty given, line-ups). A round without subscribers does nothing, and one with them reads the scoreboards and
/// match details already in memory (a live match's details are reloaded at most once per poll interval).
/// </summary>
internal sealed partial class MatchNotificationWorker(
    IServiceScopeFactory scopeFactory,
    NotificationSignal signal,
    TimeProvider timeProvider,
    ILogger<MatchNotificationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Taken before the round: a change the poller reports during it starts the next one at once.
            var changed = signal.Next;
            try
            {
                // New scope per round, like the poller: the notifier's HTTP clients are short-lived by design.
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MatchNotifier>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A bug in one round must not end notifications for good.
                LogRoundCrashed(ex);
            }

            await Task.WhenAny(Task.Delay(Interval, timeProvider, stoppingToken), changed.WaitAsync(stoppingToken));
            stoppingToken.ThrowIfCancellationRequested();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error while sending match notifications")]
    private partial void LogRoundCrashed(Exception exception);
}
