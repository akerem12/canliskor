using CanliSkor.Core.Notifications;

namespace CanliSkor.Api.Workers;

/// <summary>
/// At startup, brings the stored subscribers back into memory. It runs beside the rest of the startup, so a
/// database that is asleep or unreachable never keeps the site from coming up; it just tries again.
/// </summary>
internal sealed partial class PushSubscriberLoadWorker(
    PushSubscriberRegistry registry,
    TimeProvider timeProvider,
    ILogger<PushSubscriberLoadWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await registry.LoadAsync(stoppingToken) is { } count)
            {
                LogLoaded(count);
                return;
            }

            await Task.Delay(RetryAfter, timeProvider, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} stored notification subscribers")]
    private partial void LogLoaded(int count);
}
