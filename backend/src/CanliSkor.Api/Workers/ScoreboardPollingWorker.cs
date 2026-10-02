using CanliSkor.Core.Options;
using CanliSkor.Core.Polling;
using Microsoft.Extensions.Options;

namespace CanliSkor.Api.Workers;

/// <summary>
/// Thin hosting loop: run a poll, wait as long as the poller says, repeat.
/// All decisions live in <see cref="ScoreboardPoller"/>, which is unit-tested on its own.
/// </summary>
internal sealed partial class ScoreboardPollingWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<PollingOptions> pollingOptions,
    TimeProvider timeProvider,
    ILogger<ScoreboardPollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                // New scope per round: gives the poller a fresh typed HttpClient each time (see AddCore).
                await using var scope = scopeFactory.CreateAsyncScope();
                delay = await scope.ServiceProvider.GetRequiredService<ScoreboardPoller>().PollAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An unhandled exception would stop the BackgroundService (and by default the host).
                // A bug in one round must not end live updates for good, so log and try again later.
                LogPollCrashed(ex);
                delay = pollingOptions.CurrentValue.ErrorRetryInterval;
            }

            await Task.Delay(delay, timeProvider, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error during scoreboard poll")]
    private partial void LogPollCrashed(Exception exception);
}
