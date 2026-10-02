using System.Text.Json.Serialization;
using CanliSkor.Api.Endpoints;
using CanliSkor.Api.Hubs;
using CanliSkor.Api.Workers;
using CanliSkor.Core;
using CanliSkor.Core.Abstractions;
using CanliSkor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCore(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<ScoreboardPollingWorker>();

// Enums as strings ("Live", not 1): readable, and reordering the enum can't break clients.
// Same setting for REST and SignalR, so both deliver identical match JSON.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IMatchUpdatePublisher, SignalRMatchUpdatePublisher>();

var app = builder.Build();

app.MapLeagueEndpoints();
app.MapMatchEndpoints();
app.MapHub<LiveScoresHub>(LiveScoresHub.Path);

app.Run();

// Exposes Program to WebApplicationFactory in integration tests.
public partial class Program;
