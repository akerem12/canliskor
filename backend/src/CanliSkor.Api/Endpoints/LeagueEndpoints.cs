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

        // Followed leagues with their names, in display order.
        app.MapGet("/api/competitions", async (LeagueInfoService leagues, CancellationToken ct) =>
            (await leagues.GetLeaguesAsync(ct)).Select(l => new LeagueResponse(l.Code, l.Name)).ToList())
            .WithTags("Leagues");

        // The league table. Competitions without one (friendlies) answer with no groups.
        app.MapGet("/api/leagues/{code}/standings", async Task<Results<Ok<StandingsResponse>, NotFound, ProblemHttpResult>> (
            string code, LeagueInfoService leagues, CancellationToken ct) =>
        {
            try
            {
                return await leagues.GetStandingsAsync(code, ct) is { } standings
                    ? TypedResults.Ok(standings.ToResponse())
                    : TypedResults.NotFound();
            }
            catch (FootballDataProviderException)
            {
                return TypedResults.Problem("The standings are temporarily unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
            .WithTags("Leagues");

        // A team: its results and every coming fixture, in all competitions it plays in.
        app.MapGet("/api/leagues/{code}/teams/{teamId:regex(^[0-9]{{1,15}}$)}", async Task<Results<Ok<TeamProfileResponse>, NotFound, ProblemHttpResult>> (
            string code, string teamId, LeagueInfoService leagues, CancellationToken ct) =>
        {
            try
            {
                return await leagues.GetTeamAsync(code, teamId, ct) is { } team
                    ? TypedResults.Ok(team.ToResponse())
                    : TypedResults.NotFound();
            }
            catch (FootballDataProviderException)
            {
                return TypedResults.Problem("The team is temporarily unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
            .WithTags("Teams");

        // A team's squad for the current season. Same rules as match details: numeric ids, followed leagues only.
        app.MapGet("/api/leagues/{code}/teams/{teamId:regex(^[0-9]{{1,15}}$)}/squad", async Task<Results<Ok<SquadResponse>, NotFound, ProblemHttpResult>> (
            string code, string teamId, SquadService squads, CancellationToken ct) =>
        {
            try
            {
                return await squads.GetAsync(code, teamId, ct) is { } snapshot
                    ? TypedResults.Ok(snapshot.ToResponse())
                    : TypedResults.NotFound();
            }
            catch (FootballDataProviderException)
            {
                return TypedResults.Problem("The squad is temporarily unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
            .WithTags("Teams");

        return app;
    }
}
