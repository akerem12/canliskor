using CanliSkor.Core.Notifications;

namespace CanliSkor.Api.Workers;

/// <summary>
/// Thin hosting loop for <see cref="MatchNotifier"/>: every half minute, so a kickoff reminder is at most that late.
/// A round without subscribers does nothing, and one with them reads the scoreboards already in memory.
/// </summary>
internal sealed partial class MatchNotificationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<MatchNotificationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
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

            await Task.Delay(Interval, timeProvider, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error while sending match notifications")]
    private partial void LogRoundCrashed(Exception exception);
}
