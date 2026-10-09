using CanliSkor.Core.Notifications;
using CanliSkor.Core.Options;
using CanliSkor.Core.Polling;
using CanliSkor.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanliSkor.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FootballOptions>()
            .Bind(configuration.GetSection(FootballOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PollingOptions>()
            .Bind(configuration.GetSection(PollingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // Scoped, not singleton: it depends on IFootballDataProvider, which is a typed HttpClient.
        // Typed clients are short-lived by design; capturing one in a singleton would defeat
        // IHttpClientFactory's handler rotation (e.g. DNS changes would never be picked up).
        services.AddScoped<ScoreboardPoller>();
        services.AddScoped<MatchQueryService>();
        services.AddScoped<OnDemandScoreboardLoader>();
        services.AddScoped<MatchDetailService>();
        services.AddScoped<SquadService>();
        services.AddScoped<LeagueInfoService>();
        services.AddScoped<ExpectedLineupService>();
        services.AddSingleton<OnDemandFetchGate>();
        services.AddSingleton<MatchViewerRegistry>();
        services.AddSingleton<PollSchedule>();
        services.AddScoped<LiveDetailRefresher>();
        services.AddSingleton<PushSubscriberRegistry>();
        services.AddSingleton<NotificationLog>();
        services.AddScoped<MatchNotifier>();

        return services;
    }
}
