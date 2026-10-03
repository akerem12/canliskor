using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using CanliSkor.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CanliSkor.Core.Tests.Services;

public class SquadServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 18, 0, 0, TimeSpan.Zero);
    private static readonly Squad Besiktas = new("tur.1", "1895", "Besiktas",
        [new SquadPlayer("1", "Keeper", "1", PlayerPosition.Goalkeeper, 30, "Germany")]);

    private readonly FakeFootballDataProvider _provider = new();
    private readonly FakeMatchStore _store = new();
    private readonly FakeTimeProvider _time = new(Now);

    private SquadService CreateService() => new(
        _provider,
        _store,
        new OnDemandFetchGate(),
        TestOptions.Leagues("tur.1"),
        _time,
        NullLogger<SquadService>.Instance);

    [Fact]
    public async Task Loads_a_squad_from_the_provider_and_caches_it()
    {
        _provider.Returns(Besiktas);

        var snapshot = await CreateService().GetAsync("tur.1", "1895");

        Assert.Same(Besiktas, snapshot?.Squad);
        Assert.Equal(Now, snapshot?.FetchedAtUtc);
        Assert.Same(snapshot, await _store.GetSquadAsync("tur.1", "1895"));
    }

    [Fact]
    public async Task Cached_squad_is_served_without_asking_the_provider_again()
    {
        _provider.Returns(Besiktas);
        var service = CreateService();
        await service.GetAsync("tur.1", "1895");

        _time.Advance(SquadService.RefreshAfter - TimeSpan.FromMinutes(1));
        await service.GetAsync("tur.1", "1895");

        Assert.Single(_provider.SquadRequests);
    }

    [Fact]
    public async Task Squad_is_refetched_once_it_is_old()
    {
        _provider.Returns(Besiktas);
        var service = CreateService();
        await service.GetAsync("tur.1", "1895");

        _time.Advance(SquadService.RefreshAfter);
        var snapshot = await service.GetAsync("tur.1", "1895");

        Assert.Equal(2, _provider.SquadRequests.Count);
        Assert.Equal(_time.GetUtcNow(), snapshot?.FetchedAtUtc);
    }

    [Fact]
    public async Task Unknown_team_is_null()
    {
        Assert.Null(await CreateService().GetAsync("tur.1", "404"));
    }

    [Fact]
    public async Task League_that_is_not_followed_never_reaches_the_provider()
    {
        Assert.Null(await CreateService().GetAsync("ger.1", "1895"));
        Assert.Empty(_provider.SquadRequests);
    }

    [Fact]
    public async Task Old_squad_is_served_when_the_provider_fails()
    {
        _provider.Returns(Besiktas);
        var service = CreateService();
        var first = await service.GetAsync("tur.1", "1895");

        _time.Advance(SquadService.RefreshAfter);
        _provider.FailSquads = true;

        Assert.Same(first, await service.GetAsync("tur.1", "1895"));
    }

    [Fact]
    public async Task Provider_failure_with_nothing_cached_is_thrown()
    {
        _provider.FailSquads = true;

        await Assert.ThrowsAsync<FootballDataProviderException>(() => CreateService().GetAsync("tur.1", "1895"));
    }
}
