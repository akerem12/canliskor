using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Options;
using Microsoft.Extensions.Options;

namespace CanliSkor.Core.Tests;

internal static class TestData
{
    private static int _nextId;

    public static Match Match(
        MatchStatus status,
        DateTimeOffset kickoffUtc,
        string leagueCode = "tur.1",
        Score? score = null) => new(
            Id: Interlocked.Increment(ref _nextId).ToString(),
            LeagueCode: leagueCode,
            KickoffUtc: kickoffUtc,
            Status: status,
            Clock: status.IsInPlay() ? "45'" : null,
            HomeTeam: new Team("1", "Home FC", "Home", null),
            AwayTeam: new Team("2", "Away FC", "Away", null),
            Score: score);

    public static LeagueScoreboard Scoreboard(string leagueCode, DateOnly date, params Match[] matches) =>
        new(new League(leagueCode, leagueCode.ToUpperInvariant()), date, matches);
}

internal sealed class FakeMatchStore : IMatchStore
{
    private readonly Dictionary<(string, DateOnly), ScoreboardSnapshot> _snapshots = new();

    public Task<ScoreboardSnapshot?> GetAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(_snapshots.GetValueOrDefault((leagueCode, date)));

    public Task SetAsync(ScoreboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _snapshots[(snapshot.Scoreboard.League.Code, snapshot.Scoreboard.Date)] = snapshot;
        return Task.CompletedTask;
    }
}

/// <summary>Returns canned scoreboards; anything not configured is a provider failure.</summary>
internal sealed class FakeFootballDataProvider : IFootballDataProvider
{
    private readonly Dictionary<(string, DateOnly), LeagueScoreboard> _scoreboards = new();

    public List<(string LeagueCode, DateOnly Date)> Requests { get; } = [];

    public void Returns(LeagueScoreboard scoreboard) =>
        _scoreboards[(scoreboard.League.Code, scoreboard.Date)] = scoreboard;

    public Task<LeagueScoreboard> GetScoreboardAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default)
    {
        Requests.Add((leagueCode, date));
        return _scoreboards.TryGetValue((leagueCode, date), out var scoreboard)
            ? Task.FromResult(scoreboard)
            : throw new FootballDataProviderException($"No data for {leagueCode} {date}");
    }
}

internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;
    public T Get(string? name) => value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal static class TestOptions
{
    public static PollingOptions Polling() => new()
    {
        LiveInterval = TimeSpan.FromSeconds(30),
        IdleInterval = TimeSpan.FromMinutes(15),
        KickoffLeadTime = TimeSpan.FromMinutes(2),
        ErrorRetryInterval = TimeSpan.FromMinutes(1),
    };

    public static IOptionsMonitor<FootballOptions> Leagues(params string[] codes) =>
        new StaticOptionsMonitor<FootballOptions>(new FootballOptions { Leagues = [.. codes] });
}
