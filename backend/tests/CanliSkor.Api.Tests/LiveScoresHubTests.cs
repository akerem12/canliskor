using System.Text.Json;
using CanliSkor.Api.Hubs;
using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CanliSkor.Api.Tests;

/// <summary>
/// Real SignalR client against the in-memory server: subscribe, publish through the app's own
/// <see cref="IMatchUpdatePublisher"/>, and assert the JSON the client receives.
/// </summary>
public class LiveScoresHubTests(MatchEndpointsTests.Factory factory) : IClassFixture<MatchEndpointsTests.Factory>, IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly List<JsonElement> _received = [];
    private readonly SemaphoreSlim _receivedSignal = new(0);
    private HubConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, LiveScoresHub.Path), o =>
            {
                // TestServer has no real sockets; long polling runs over its in-memory HTTP handler.
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        _connection.On<JsonElement>(nameof(ILiveScoresClient.MatchUpdated), message =>
        {
            lock (_received) _received.Add(message);
            _receivedSignal.Release();
        });

        await _connection.StartAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Subscriber_receives_match_updates_for_its_league()
    {
        await _connection.InvokeAsync(nameof(LiveScoresHub.SubscribeToLeague), "tur.1");

        await PublishAsync(Change("tur.1", "42", MatchChangeKind.Score));

        var message = await NextMessageAsync();
        Assert.True(message.GetProperty("scoreChanged").GetBoolean());
        Assert.False(message.GetProperty("statusChanged").GetBoolean());

        var match = message.GetProperty("match");
        Assert.Equal("42", match.GetProperty("id").GetString());
        Assert.Equal("Live", match.GetProperty("status").GetString()); // same string enums as REST
        Assert.Equal("2026-10-09T20:00:00+03:00", match.GetProperty("kickoff").GetString());
        Assert.Equal(1, match.GetProperty("score").GetProperty("home").GetInt32());
    }

    [Fact]
    public async Task Updates_for_other_leagues_are_not_received()
    {
        await _connection.InvokeAsync(nameof(LiveScoresHub.SubscribeToLeague), "eng.1");

        // Messages on one connection arrive in order, so if the eng.1 message is the first one
        // we get, the tur.1 message sent before it was never delivered.
        await PublishAsync(Change("tur.1", "1", MatchChangeKind.Score));
        await PublishAsync(Change("eng.1", "2", MatchChangeKind.Score));

        var message = await NextMessageAsync();
        Assert.Equal("eng.1", message.GetProperty("match").GetProperty("leagueCode").GetString());
    }

    [Fact]
    public async Task Subscribing_to_an_unknown_league_fails()
    {
        var ex = await Assert.ThrowsAsync<HubException>(() => _connection.InvokeAsync(nameof(LiveScoresHub.SubscribeToLeague), "xyz.9"));

        Assert.Contains("Unknown league", ex.Message);
    }

    private Task PublishAsync(MatchChange change) =>
        factory.Services.GetRequiredService<IMatchUpdatePublisher>().PublishAsync([change]);

    private async Task<JsonElement> NextMessageAsync()
    {
        Assert.True(await _receivedSignal.WaitAsync(Timeout), "No MatchUpdated message received");
        lock (_received) return _received[0];
    }

    private static MatchChange Change(string leagueCode, string matchId, MatchChangeKind kinds) => new(
        new Match(matchId, leagueCode, new DateTimeOffset(2026, 10, 9, 17, 0, 0, TimeSpan.Zero), MatchStatus.Live, "20'",
            new Team("432", "Galatasaray", "Galatasaray", null),
            new Team("6870", "Kasimpasa", "Kasimpasa", null),
            new Score(1, 0)),
        kinds);
}
