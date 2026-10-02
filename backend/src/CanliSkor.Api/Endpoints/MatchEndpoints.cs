using CanliSkor.Api.Contracts;
using CanliSkor.Core.Services;
using CanliSkor.Core.Time;

namespace CanliSkor.Api.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/matches").WithTags("Matches");

        group.MapGet("/", async (MatchQueryService queries, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var snapshots = await queries.GetTodayAsync(ct);
            return new MatchDayResponse(IstanbulTime.Today(timeProvider), snapshots.Select(s => s.ToResponse()).ToList());
        });

        group.MapGet("/live", async (MatchQueryService queries, CancellationToken ct) =>
            (await queries.GetLiveAsync(ct)).Select(s => s.ToResponse()).ToList());

        return app;
    }
}
