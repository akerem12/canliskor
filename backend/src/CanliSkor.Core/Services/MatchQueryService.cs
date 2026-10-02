using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Services;

/// <summary>
/// Read side for the API. Only ever reads the store — never calls the provider —
/// so request traffic cannot cause extra load on the external API.
/// </summary>
public sealed class MatchQueryService(
    IMatchStore store,
    IOptionsMonitor<FootballOptions> footballOptions,
    TimeProvider timeProvider)
{
    /// <summary>Today's (Istanbul) scoreboards in configured league order, leagues without matches left out.</summary>
    public async Task<IReadOnlyList<ScoreboardSnapshot>> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await GetSnapshotsAsync(IstanbulTime.Today(timeProvider), cancellationToken);
        return snapshots.Where(s => s.Scoreboard.Matches.Count > 0).ToList();
    }

    /// <summary>In-play matches only. Includes yesterday so games running past midnight still show.</summary>
    public async Task<IReadOnlyList<ScoreboardSnapshot>> GetLiveAsync(CancellationToken cancellationToken = default)
    {
        var today = IstanbulTime.Today(timeProvider);
        var snapshots = (await GetSnapshotsAsync(today.AddDays(-1), cancellationToken))
            .Concat(await GetSnapshotsAsync(today, cancellationToken));

        return snapshots
            .Select(s => s with { Scoreboard = s.Scoreboard with { Matches = s.Scoreboard.Matches.Where(m => m.Status.IsInPlay()).ToList() } })
            .Where(s => s.Scoreboard.Matches.Count > 0)
            .ToList();
    }

    private async Task<List<ScoreboardSnapshot>> GetSnapshotsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var result = new List<ScoreboardSnapshot>();
        foreach (var leagueCode in footballOptions.CurrentValue.Leagues)
        {
            if (await store.GetAsync(leagueCode, date, cancellationToken) is { } snapshot)
            {
                result.Add(snapshot);
            }
        }

        return result;
    }
}
