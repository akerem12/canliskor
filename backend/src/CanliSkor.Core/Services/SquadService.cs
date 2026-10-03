using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Services;

/// <summary>
/// Team squads, loaded on request and cached in the store. A squad only changes with a transfer or a new shirt
/// number, so one fetch serves everyone for <see cref="RefreshAfter"/>. Shares <see cref="OnDemandFetchGate"/>
/// with browsing and match details, so all request-driven provider calls together still run one at a time.
/// </summary>
public sealed partial class SquadService(
    IFootballDataProvider provider,
    IMatchStore store,
    OnDemandFetchGate gate,
    IOptionsMonitor<FootballOptions> footballOptions,
    TimeProvider timeProvider,
    ILogger<SquadService> logger)
{
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromHours(6);

    /// <returns>
    /// The cached or freshly loaded squad; the stale one if loading fails; null if the league isn't followed
    /// or the provider doesn't know the team in that league.
    /// </returns>
    /// <exception cref="FootballDataProviderException">Loading failed and nothing is cached.</exception>
    public async Task<SquadSnapshot?> GetAsync(string leagueCode, string teamId, CancellationToken cancellationToken = default)
    {
        // Only followed leagues: requests can't make us fetch anything else from the provider.
        if (!footballOptions.CurrentValue.Leagues.Contains(leagueCode))
        {
            return null;
        }

        var cached = await store.GetSquadAsync(leagueCode, teamId, cancellationToken);
        if (cached is not null && IsFresh(cached))
        {
            return cached;
        }

        using (await gate.AcquireAsync(cancellationToken))
        {
            // Another request may have loaded it while we waited.
            cached = await store.GetSquadAsync(leagueCode, teamId, cancellationToken);
            if (cached is not null && IsFresh(cached))
            {
                return cached;
            }

            try
            {
                var squad = await provider.GetSquadAsync(leagueCode, teamId, cancellationToken);
                if (squad is null)
                {
                    return null;
                }

                var snapshot = new SquadSnapshot(squad, timeProvider.GetUtcNow());
                await store.SetSquadAsync(snapshot, cancellationToken);
                return snapshot;
            }
            catch (FootballDataProviderException ex) when (cached is not null)
            {
                LogLoadFailed(ex, leagueCode, teamId);
                return cached;
            }
        }
    }

    private bool IsFresh(SquadSnapshot snapshot) => timeProvider.GetUtcNow() - snapshot.FetchedAtUtc < RefreshAfter;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading the squad of team {TeamId} ({LeagueCode}) failed; serving the cached copy")]
    private partial void LogLoadFailed(Exception exception, string leagueCode, string teamId);
}
