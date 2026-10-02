using CanliSkor.Core.Options;
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

        return app;
    }
}
