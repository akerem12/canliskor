using System.Text.Json;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Tests.Espn;

internal static class FixtureLoader
{
    public static string ReadJson(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));

    public static EspnScoreboardResponse LoadScoreboard(string fileName) =>
        Deserialize(ReadJson(fileName));

    public static EspnScoreboardResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<EspnScoreboardResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    public static EspnRosterResponse LoadRoster(string fileName) =>
        JsonSerializer.Deserialize<EspnRosterResponse>(ReadJson(fileName), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    public static T Load<T>(string fileName) =>
        JsonSerializer.Deserialize<T>(ReadJson(fileName), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    public static EspnSummaryResponse LoadSummary(string fileName) =>
        JsonSerializer.Deserialize<EspnSummaryResponse>(ReadJson(fileName), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
