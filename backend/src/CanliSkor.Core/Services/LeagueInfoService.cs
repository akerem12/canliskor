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

    /// <summary>Fixture lists change rarely: a postponement, a kickoff time moved for television.</summary>
    public static readonly TimeSpan FixturesRefreshAfter = TimeSpan.FromMinutes(30);

    /// <summary>A match that kicked off this long ago still counts as coming up, so one in play doesn't vanish from the list.</summary>
    public static readonly TimeSpan UpcomingGrace = TimeSpan.FromHours(3);

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

    /// <summary>The league's matches still to be played, this month and next, soonest first.</summary>
    /// <returns>Null if the league isn't followed or the provider doesn't know it.</returns>
    /// <exception cref="FootballDataProviderException">Loading failed and nothing is cached.</exception>
    public async Task<Timestamped<LeagueFixtures>?> GetFixturesAsync(string leagueCode, CancellationToken cancellationToken = default)
    {
        var today = IstanbulTime.Today(timeProvider);
        var entry = await GetAsync(
            // The month is part of the key: on the 1st a new pair of months is loaded.
            $"fixtures:{leagueCode}:{today.Year}-{today.Month}",
            leagueCode,
            FixturesRefreshAfter,
            ct => provider.GetLeagueFixturesAsync(leagueCode, today, ct),
            cancellationToken);
        if (entry is null)
        {
            return null;
        }

        // The cached list is the whole two months; what has been played since is dropped on the way out.
        var earliest = timeProvider.GetUtcNow() - UpcomingGrace;
        var upcoming = entry.Value.Matches
            .Where(m => m.KickoffUtc >= earliest && m.Status is MatchStatus.Scheduled or MatchStatus.Live or MatchStatus.HalfTime)
            .OrderBy(m => m.KickoffUtc)
            .ToList();

        return entry with { Value = entry.Value with { Matches = upcoming } };
    }

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
