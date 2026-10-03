using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Services;

/// <summary>
/// League tables and team profiles, loaded on request and cached in the store. Same rules as the other
/// request-driven loads: followed leagues only, one provider call at a time across the app
/// (<see cref="OnDemandFetchGate"/>), and the last good copy is served if the provider fails.
/// </summary>
public sealed partial class LeagueInfoService(
    IFootballDataProvider provider,
    IMatchStore store,
    OnDemandFetchGate gate,
    IOptionsMonitor<FootballOptions> footballOptions,
    TimeProvider timeProvider,
    ILogger<LeagueInfoService> logger)
{
    /// <summary>A table only moves when a match ends (or a goal is scored); a few minutes behind is fine.</summary>
    public static readonly TimeSpan StandingsRefreshAfter = TimeSpan.FromMinutes(5);

    /// <summary>Results and fixtures of one team change a couple of times a week.</summary>
    public static readonly TimeSpan TeamRefreshAfter = TimeSpan.FromMinutes(15);

    /// <summary>Followed leagues in display order, with the names the provider gave on today's scoreboards.</summary>
    public async Task<IReadOnlyList<League>> GetLeaguesAsync(CancellationToken cancellationToken = default)
    {
        var today = IstanbulTime.Today(timeProvider);
        var leagues = new List<League>();
        foreach (var code in footballOptions.CurrentValue.Leagues)
        {
            // Right after a start the poller may not have reached a league yet: its code stands in for the name.
            var snapshot = await store.GetAsync(code, today, cancellationToken);
            leagues.Add(snapshot?.Scoreboard.League ?? new League(code, code));
        }

        return leagues;
    }

    /// <returns>Null if the league isn't followed or the provider doesn't know it.</returns>
    /// <exception cref="FootballDataProviderException">Loading failed and nothing is cached.</exception>
    public Task<Timestamped<LeagueStandings>?> GetStandingsAsync(string leagueCode, CancellationToken cancellationToken = default) =>
        GetAsync($"standings:{leagueCode}", leagueCode, StandingsRefreshAfter, ct => provider.GetStandingsAsync(leagueCode, ct), cancellationToken);

    /// <returns>Null if the league isn't followed or the provider doesn't know the team in it.</returns>
    /// <exception cref="FootballDataProviderException">Loading failed and nothing is cached.</exception>
    public Task<Timestamped<TeamProfile>?> GetTeamAsync(string leagueCode, string teamId, CancellationToken cancellationToken = default) =>
        GetAsync($"team:{leagueCode}:{teamId}", leagueCode, TeamRefreshAfter, ct => provider.GetTeamProfileAsync(leagueCode, teamId, ct), cancellationToken);

    private async Task<Timestamped<T>?> GetAsync<T>(
        string key, string leagueCode, TimeSpan refreshAfter, Func<CancellationToken, Task<T?>> load, CancellationToken cancellationToken)
        where T : class
    {
        // Only followed leagues: requests can't make us fetch anything else from the provider.
        if (!footballOptions.CurrentValue.Leagues.Contains(leagueCode))
        {
            return null;
        }

        bool IsFresh(Timestamped<T> entry) => timeProvider.GetUtcNow() - entry.FetchedAtUtc < refreshAfter;

        var cached = await store.GetCachedAsync<T>(key, cancellationToken);
        if (cached is not null && IsFresh(cached))
        {
            return cached;
        }

        using (await gate.AcquireAsync(cancellationToken))
        {
            // Another request may have loaded it while we waited.
            cached = await store.GetCachedAsync<T>(key, cancellationToken);
            if (cached is not null && IsFresh(cached))
            {
                return cached;
            }

            try
            {
                var value = await load(cancellationToken);
                if (value is null)
                {
                    return null;
                }

                var entry = new Timestamped<T>(value, timeProvider.GetUtcNow());
                await store.SetCachedAsync(key, entry, cancellationToken);
                return entry;
            }
            catch (FootballDataProviderException ex) when (cached is not null)
            {
                LogLoadFailed(ex, key);
                return cached;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading {Key} failed; serving the cached copy")]
    private partial void LogLoadFailed(Exception exception, string key);
}
