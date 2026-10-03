using CanliSkor.Api.Contracts;
using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Options;
using CanliSkor.Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace CanliSkor.Api.Endpoints;

public static class LeagueEndpoints
{
    public static IEndpointRouteBuilder MapLeagueEndpoints(this IEndpointRouteBuilder app)
    {
        // Followed league codes, in display order. Clients use them to subscribe to the hub
        // before loading matches, so no update can slip through between the two.
        app.MapGet("/api/leagues", (IOptionsMonitor<FootballOptions> options) => options.CurrentValue.Leagues)
            .WithTags("Leagues");

        // Events and statistics of one match. ESPN match ids are numeric; anything else is a 404 without a provider call.
        app.MapGet("/api/leagues/{code}/matches/{id:regex(^[0-9]{{1,15}}$)}", async Task<Results<Ok<MatchDetailResponse>, NotFound, ProblemHttpResult>> (
            string code, string id, MatchDetailService details, CancellationToken ct) =>
        {
            try
            {
                return await details.GetAsync(code, id, ct) is { } snapshot
                    ? TypedResults.Ok(snapshot.ToResponse())
                    : TypedResults.NotFound();
            }
            catch (FootballDataProviderException)
            {
                return TypedResults.Problem("Match details are temporarily unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
            .WithTags("Matches");

        return app;
    }
}
