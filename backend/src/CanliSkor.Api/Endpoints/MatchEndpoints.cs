using CanliSkor.Api.Contracts;
using CanliSkor.Core.Services;
using CanliSkor.Core.Time;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CanliSkor.Api.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/matches").WithTags("Matches");

        // ?date=2026-10-09 (Istanbul date) to browse other days; omitted means today.
        group.MapGet("/", async Task<Results<Ok<MatchDayResponse>, ValidationProblem>> (
            DateOnly? date, MatchQueryService queries, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var day = date ?? IstanbulTime.Today(timeProvider);
            if (!queries.IsBrowsable(day))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["date"] = [$"Only dates within {MatchQueryService.MaxDaysAway} days of today are available."],
                });
            }

            var snapshots = await queries.GetDayAsync(day, ct);
            return TypedResults.Ok(new MatchDayResponse(day, snapshots.Select(s => s.ToResponse()).ToList()));
        });

        group.MapGet("/live", async (MatchQueryService queries, CancellationToken ct) =>
            (await queries.GetLiveAsync(ct)).Select(s => s.ToResponse()).ToList());

        return app;
    }
}
