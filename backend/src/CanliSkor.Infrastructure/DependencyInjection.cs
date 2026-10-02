using CanliSkor.Core.Abstractions;
using CanliSkor.Infrastructure.Caching;
using CanliSkor.Infrastructure.Espn;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanliSkor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EspnOptions>()
            .Bind(configuration.GetSection(EspnOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IFootballDataProvider, EspnFootballDataProvider>((sp, client) =>
            {
                client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<EspnOptions>>().Value.BaseUrl);
            })
            // Retry with exponential backoff + jitter, per-attempt and total timeouts, circuit breaker.
            .AddStandardResilienceHandler();

        services.AddMemoryCache();
        services.AddSingleton<IMatchStore, InMemoryMatchStore>();

        return services;
    }
}
