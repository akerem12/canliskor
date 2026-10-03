using System.Globalization;
using System.Text.RegularExpressions;
using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// Pure translation of ESPN's athlete page to <see cref="PlayerProfile"/>. ESPN has no minutes played and no
/// passing figures for footballers. It has a portrait for very few of them, so portraits are left out altogether.
/// </summary>
internal static partial class EspnAthleteMapper
{
    private const double CentimetresPerInch = 2.54;

    /// <param name="overview">Null if ESPN has no statistics page for the player.</param>
    /// <returns>Null if the response doesn't name the player.</returns>
    public static PlayerProfile? Map(EspnAthleteResponse response, EspnAthleteOverviewResponse? overview)
    {
        var athlete = response.Athlete;
        if (string.IsNullOrEmpty(athlete?.Id) || string.IsNullOrEmpty(athlete.DisplayName))
        {
            return null;
        }

        var team = string.IsNullOrEmpty(athlete.Team?.Id) || string.IsNullOrEmpty(athlete.Team.DisplayName)
            ? null
            : EspnScoreboardMapper.MapTeam(athlete.Team);

        return new PlayerProfile(
            athlete.Id,
            athlete.DisplayName,
            NullIfEmpty(athlete.Jersey),
            EspnRosterMapper.MapPosition(athlete.Position?.Abbreviation),
            NullIfEmpty(athlete.Citizenship),
            NullIfEmpty(athlete.Flag?.Href),
            athlete.Age,
            ParseHeight(athlete.DisplayHeight),
            team,
            MapCompetitions(overview?.Statistics, athlete.StatsSummary));
    }

    private static List<PlayerCompetitionStats> MapCompetitions(EspnAthleteStatistics? statistics, EspnStatsSummary? summary)
    {
        var names = (statistics?.Names ?? []).ToList();
        var teams = (statistics?.Filters?.FirstOrDefault(f => f.Name == "team")?.Options ?? [])
            .Where(o => !string.IsNullOrEmpty(o.Value) && !string.IsNullOrEmpty(o.DisplayValue))
            .DistinctBy(o => o.Value)
            .ToDictionary(o => o.Value!, o => o.DisplayValue!);

        var competitions = new List<(PlayerCompetitionStats Stats, bool IsMain)>();
        foreach (var split in statistics?.Splits ?? [])
        {
            if (string.IsNullOrEmpty(split.DisplayName))
            {
                continue;
            }

            int? Stat(string name)
            {
                var index = names.IndexOf(name);
                return index >= 0 && int.TryParse(split.Stats?.ElementAtOrDefault(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : null;
            }

            // The summary is about one competition, named like the split plus " Stats".
            var isMain = summary?.DisplayName == split.DisplayName + " Stats";

            competitions.Add((new PlayerCompetitionStats(
                split.DisplayName,
                NullIfEmpty(split.LeagueSlug),
                split.TeamId is not null ? teams.GetValueOrDefault(split.TeamId) : null,
                Starts: Stat("starts") ?? 0,
                SubstituteAppearances: isMain ? SubstituteAppearances(summary) : null,
                Goals: Stat("totalGoals") ?? 0,
                Assists: Stat("goalAssists") ?? 0,
                Shots: Stat("totalShots") ?? 0,
                ShotsOnTarget: Stat("shotsOnTarget") ?? 0,
                YellowCards: Stat("yellowCards") ?? 0,
                RedCards: Stat("redCards") ?? 0,
                FoulsCommitted: Stat("foulsCommitted") ?? 0,
                FoulsSuffered: Stat("foulsSuffered") ?? 0,
                Offsides: Stat("offsides") ?? 0,
                CleanSheets: Stat("cleanSheet"),
                Saves: Stat("saves"),
                GoalsConceded: Stat("goalsConceded")), isMain));
        }

        // Stable: after the main league, ESPN's own order.
        return competitions.OrderByDescending(c => c.IsMain).Select(c => c.Stats).ToList();
    }

    /// <summary>"3 (3)" → 3: the number in brackets.</summary>
    private static int? SubstituteAppearances(EspnStatsSummary? summary)
    {
        var value = summary?.Statistics?.FirstOrDefault(s => s.Name == "starts-subIns")?.DisplayValue;
        var match = value is null ? null : StartsAndSubs().Match(value);

        return match is { Success: true } ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>"6' 0\"" → 183.</summary>
    internal static int? ParseHeight(string? displayHeight)
    {
        var match = displayHeight is null ? null : FeetAndInches().Match(displayHeight);
        if (match is not { Success: true })
        {
            return null;
        }

        var inches = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 12
            + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return (int)Math.Round(inches * CentimetresPerInch, MidpointRounding.AwayFromZero);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex(@"^\s*(\d{1,2})'\s*(\d{1,2})""?\s*$")]
    private static partial Regex FeetAndInches();

    [GeneratedRegex(@"^\s*\d{1,4}\s*\((\d{1,4})\)\s*$")]
    private static partial Regex StartsAndSubs();
}
