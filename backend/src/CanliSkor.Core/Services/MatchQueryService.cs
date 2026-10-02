using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using CanliSkor.Core.Time;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Services;

/// <summary>
/// Read side for the API. Today and live data come only from the store, which the poller keeps current,
/// so that traffic never reaches the external API. Other days go through <see cref="OnDemandScoreboardLoader"/>.
/// </summary>
public sealed class MatchQueryService(
    IMatchStore store,
    OnDemandScoreboardLoader loader,
    IOptionsMonitor<FootballOptions> footballOptions,
    TimeProvider timeProvider)
{
    /// <summary>How far from today clients may browse. Bounds what browsing can ask of the provider.</summary>
    public const int MaxDaysAway = 7;

    public bool IsBrowsable(DateOnly date) =>
        Math.Abs(date.DayNumber - IstanbulTime.Today(timeProvider).DayNumber) <= MaxDaysAway;

    /// <summary>One day's scoreboards in configured league order, leagues without matches left out.</summary>
    /// <param name="date">An Istanbul date within <see cref="MaxDaysAway"/> of today; null means today.</param>
    public async Task<IReadOnlyList<ScoreboardSnapshot>> GetDayAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var today = IstanbulTime.Today(timeProvider);
        var day = date ?? today;
        if (!IsBrowsable(day))
        {
            throw new ArgumentOutOfRangeException(nameof(date), day, $"Only dates within {MaxDaysAway} days of today are available.");
        }

        List<ScoreboardSnapshot> snapshots;
        if (day == today)
        {
            snapshots = await GetSnapshotsAsync(day, cancellationToken);
        }
        else
        {
            snapshots = [];
            foreach (var leagueCode in footballOptions.CurrentValue.Leagues)
            {
                if (await loader.GetAsync(leagueCode, day, today, cancellationToken) is { } snapshot)
                {
                    snapshots.Add(snapshot);
                }
            }
        }

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
