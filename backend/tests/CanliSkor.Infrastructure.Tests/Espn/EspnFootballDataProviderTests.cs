using System.Net;
using CanliSkor.Core.Abstractions;
using CanliSkor.Infrastructure.Espn;

namespace CanliSkor.Infrastructure.Tests.Espn;

public class EspnFootballDataProviderTests
{
    private static readonly Uri BaseAddress = new("https://espn.test/soccer/");

    [Fact]
    public async Task Requests_league_scoreboard_for_given_date()
    {
        var handler = new StubHandler(_ => Json(FixtureLoader.ReadJson("scoreboard-tur1-finished.json")));
        var provider = CreateProvider(handler);

        var scoreboard = await provider.GetScoreboardAsync("tur.1", new DateOnly(2026, 9, 20));

        Assert.Equal("https://espn.test/soccer/tur.1/scoreboard?dates=20260920", handler.LastRequestUri?.ToString());
        Assert.Equal(4, scoreboard.Matches.Count);
    }

    [Fact]
    public async Task Wraps_http_errors_in_provider_exception()
    {
        var provider = CreateProvider(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var ex = await Assert.ThrowsAsync<FootballDataProviderException>(
            () => provider.GetScoreboardAsync("tur.1", new DateOnly(2026, 9, 20)));
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Wraps_invalid_json_in_provider_exception()
    {
        var provider = CreateProvider(new StubHandler(_ => Json("<html>not json</html>")));

        await Assert.ThrowsAsync<FootballDataProviderException>(
            () => provider.GetScoreboardAsync("tur.1", new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public async Task Does_not_wrap_caller_cancellation()
    {
        var provider = CreateProvider(new StubHandler(_ => Json("{}")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetScoreboardAsync("tur.1", new DateOnly(2026, 9, 20), cts.Token));
    }

    private static EspnFootballDataProvider CreateProvider(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = BaseAddress });

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }
}
