using System.Net;
using CanliSkor.Core.Abstractions;
using CanliSkor.Infrastructure.Espn;

namespace CanliSkor.Infrastructure.Tests.Espn;

public class EspnFootballDataProviderTests
{
    private static readonly Uri BaseAddress = new("https://espn.test/soccer/");

    [Fact]
    public async Task Requests_the_previous_and_the_same_espn_day()
    {
        var handler = new StubHandler(_ => Json(FixtureLoader.ReadJson("scoreboard-tur1-finished.json")));
        var provider = CreateProvider(handler);

        var scoreboard = await provider.GetScoreboardAsync("tur.1", new DateOnly(2026, 9, 20));

        Assert.Equal(
            ["https://espn.test/soccer/tur.1/scoreboard?dates=20260919", "https://espn.test/soccer/tur.1/scoreboard?dates=20260920"],
            handler.RequestUris.Select(u => u.ToString()));
        Assert.Equal(4, scoreboard.Matches.Count); // same response twice: no duplicates
        Assert.Equal(new DateOnly(2026, 9, 20), scoreboard.Date);
    }

    [Fact]
    public async Task Keeps_exactly_the_matches_kicking_off_on_the_istanbul_date()
    {
        // ESPN lists by US Eastern date. Previous ESPN day: one game moved to 23:00 UTC = 02:00 on the 20th in Istanbul.
        var previousDay = FixtureLoader.ReadJson("scoreboard-eng1-finished.json").Replace("2026-09-19T16:30Z", "2026-09-19T23:00Z");
        // Same ESPN day: two games moved to 21:30 UTC = 00:30 on the 21st in Istanbul.
        var sameDay = FixtureLoader.ReadJson("scoreboard-tur1-finished.json").Replace("2026-09-20T17:00Z", "2026-09-20T21:30Z");
        var provider = CreateProvider(new StubHandler(request =>
            Json(request.RequestUri!.Query.Contains("20260919") ? previousDay : sameDay)));

        var scoreboard = await provider.GetScoreboardAsync("tur.1", new DateOnly(2026, 9, 20));

        Assert.Equal(["401879270", "401888285", "401888287"], scoreboard.Matches.Select(m => m.Id).Order());
        Assert.Equal("401879270", scoreboard.Matches[0].Id); // ordered by kickoff
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
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }
}
