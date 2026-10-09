using CanliSkor.Core.Abstractions;
using CanliSkor.Infrastructure.Caching;
using CanliSkor.Infrastructure.Espn;
using CanliSkor.Infrastructure.Push;
using CanliSkor.Infrastructure.Storage;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

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

        services.AddOptions<PushOptions>()
            .Bind(configuration.GetSection(PushOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Singletons: the keys must stay the same for as long as the process lives.
        services.AddSingleton(sp => VapidKeys.FromSecret(sp.GetRequiredService<IOptions<PushOptions>>().Value.Secret));
        services.AddSingleton(sp =>
        {
            var keys = sp.GetRequiredService<VapidKeys>();
            return new VapidAuthentication(keys.PublicKey, keys.PrivateKey) { Subject = sp.GetRequiredService<IOptions<PushOptions>>().Value.Subject };
        });
        services.AddHttpClient<WebPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15));

        // The Android app is reached through Firebase. The access token is kept between sends, hence the singleton.
        services.AddOptions<FirebaseOptions>().Bind(configuration.GetSection(FirebaseOptions.SectionName));
        services.AddHttpClient(FirebaseAccessTokens.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddSingleton<FirebaseAccessTokens>();
        services.AddHttpClient<FcmPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15));

        services.AddTransient<IPushSender, ChannelPushSender>();

        // With a database the subscribers outlive a restart; without one everything else works the same.
        var databaseUrl = configuration[DatabaseUrl.SettingName];
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            services.AddSingleton<IPushSubscriberStore, NoPushSubscriberStore>();
        }
        else
        {
            services.AddSingleton(NpgsqlDataSource.Create(DatabaseUrl.ToConnectionString(databaseUrl)));
            services.AddSingleton<IPushSubscriberStore, PostgresPushSubscriberStore>();
        }

        services.AddMemoryCache();
        services.AddSingleton<IMatchStore, InMemoryMatchStore>();

        return services;
    }
}
