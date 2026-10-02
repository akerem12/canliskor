using System.Text.Json.Serialization;
using CanliSkor.Api.Endpoints;
using CanliSkor.Api.Workers;
using CanliSkor.Core;
using CanliSkor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCore(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<ScoreboardPollingWorker>();

// Enums as strings ("Live", not 1): readable, and reordering the enum can't break clients.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.MapMatchEndpoints();

app.Run();

// Exposes Program to WebApplicationFactory in integration tests.
public partial class Program;
