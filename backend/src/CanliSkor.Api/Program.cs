using CanliSkor.Core.Abstractions;
using CanliSkor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// TEMPORARY (step 1): calls ESPN directly so the provider can be tried out.
// Replaced in step 2 by cache-backed endpoints fed by the background poller.
app.MapGet("/debug/scoreboard/{leagueCode}", async (string leagueCode, DateOnly? date, IFootballDataProvider provider, CancellationToken ct) =>
    await provider.GetScoreboardAsync(leagueCode, date ?? DateOnly.FromDateTime(DateTime.UtcNow), ct));

app.Run();
