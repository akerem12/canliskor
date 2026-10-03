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
    private readonly Dictionary<(string, string), MatchDetailSnapshot> _details = new();
    private readonly Dictionary<(string, string), SquadSnapshot> _squads = new();

    public Task<ScoreboardSnapshot?> GetAsync(string leagueCode, DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(_snapshots.GetValueOrDefault((leagueCode, date)));

    public Task SetAsync(ScoreboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _snapshots[(snapshot.Scoreboard.League.Code, snapshot.Scoreboard.Date)] = snapshot;
        return Task.CompletedTask;
    }

    public Task<MatchDetailSnapshot?> GetDetailAsync(string leagueCode, string matchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_details.GetValueOrDefault((leagueCode, matchId)));

    public Task SetDetailAsync(MatchDetailSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _details[(snapshot.Detail.Match.LeagueCode, snapshot.Detail.Match.Id)] = snapshot;
        return Task.CompletedTask;
    }

    public Task<SquadSnapshot?> GetSquadAsync(string leagueCode, string teamId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_squads.GetValueOrDefault((leagueCode, teamId)));

    public Task SetSquadAsync(SquadSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _squads[(snapshot.Squad.LeagueCode, snapshot.Squad.TeamId)] = snapshot;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Returns canned scoreboards and match details. A scoreboard that isn't configured is a provider failure;
/// a detail that isn't configured is an unknown match, unless <see cref="FailDetails"/> is set.
/// </summary>
internal sealed class FakeFootballDataProvider : IFootballDataProvider
{
    private readonly Dictionary<(string, DateOnly), LeagueScoreboard> _scoreboards = new();
    private readonly Dictionary<(string, string), MatchDetail> _details = new();
    private readonly Dictionary<(string, string), Squad> _squads = new();

    public List<(string LeagueCode, DateOnly Date)> Requests { get; } = [];

    public List<(string LeagueCode, string MatchId)> DetailRequests { get; } = [];

    public List<(string LeagueCode, string TeamId)> SquadRequests { get; } = [];

    public bool FailSquads { get; set; }

    public bool FailDetails { get; set; }

    public void Returns(LeagueScoreboard scoreboard) =>
        _scoreboards[(scoreboard.League.Code, scoreboard.Date)] = scoreboard;

    public void Returns(MatchDetail detail) =>
        _details[(detail.Match.LeagueCode, detail.Match.Id)] = detail;

    public void Returns(Squad squad) => _squads[(squad.LeagueCode, squad.TeamId)] = squad;

    public Task<Squad?> GetSquadAsync(string leagueCode, string teamId, CancellationToken cancellationToken = default)
    {
        SquadRequests.Add((leagueCode, teamId));
        return FailSquads
            ? throw new FootballDataProviderException($"Squad of {teamId} unavailable")
            : Task.FromResult(_squads.GetValueOrDefault((leagueCode, teamId)));
    }

    public Task<MatchDetail?> GetMatchDetailAsync(string leagueCode, string matchId, CancellationToken cancellationToken = default)
    {
        DetailRequests.Add((leagueCode, matchId));
        return FailDetails
            ? throw new FootballDataProviderException($"Detail of {matchId} unavailable")
            : Task.FromResult(_details.GetValueOrDefault((leagueCode, matchId)));
    }

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

internal sealed class FakeMatchUpdatePublisher : IMatchUpdatePublisher
{
    public List<MatchChange> Published { get; } = [];

    public bool Fail { get; set; }

    public Task PublishAsync(IReadOnlyList<MatchChange> changes, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Push transport is down");
        }

        Published.AddRange(changes);
        return Task.CompletedTask;
    }
}
