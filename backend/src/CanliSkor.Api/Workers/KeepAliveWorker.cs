namespace CanliSkor.Api.Workers;

/// <summary>
/// Requests the site's own public address every few minutes. Render's free plan stops a service after about
/// 15 minutes without a visitor; a request through the public address counts as one, so the service stays up.
/// </summary>
internal sealed partial class KeepAliveWorker(
    IHttpClientFactory httpClientFactory,
    Uri healthUrl,
    TimeProvider timeProvider,
    ILogger<KeepAliveWorker> logger) : BackgroundService
{
    /// <summary>Comfortably below Render's 15 minutes, so one failed request doesn't let the service stop.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(healthUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, timeProvider, stoppingToken);

            try
            {
                using var client = httpClientFactory.CreateClient(nameof(KeepAliveWorker));
                using var response = await client.GetAsync(healthUrl, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // A missed request is harmless; the next one is only a few minutes away.
                LogRequestFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Keep-alive on: requesting {Url} every 5 minutes")]
    private partial void LogStarted(Uri url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Keep-alive request failed")]
    private partial void LogRequestFailed(Exception exception);
}

internal static class KeepAliveExtensions
{
    /// <summary>
    /// Adds the worker only on Render (which sets <c>RENDER_EXTERNAL_URL</c> to the public address), so it never
    /// runs locally, in tests or in CI. <c>KeepAlive__Enabled=false</c> switches it off there too.
    /// </summary>
    public static IServiceCollection AddKeepAlive(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue("KeepAlive:Enabled", true) ||
            !Uri.TryCreate(configuration["RENDER_EXTERNAL_URL"], UriKind.Absolute, out var publicUrl))
        {
            return services;
        }

        var healthUrl = new Uri(publicUrl, "/health");
        services.AddHttpClient(nameof(KeepAliveWorker), client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHostedService(sp => ActivatorUtilities.CreateInstance<KeepAliveWorker>(sp, healthUrl));
        return services;
    }
}
