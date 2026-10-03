using System.Globalization;
using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>Pure translation of ESPN's team roster to <see cref="Squad"/>. Players without an id or a name are skipped.</summary>
internal static class EspnRosterMapper
{
    /// <returns>Null if the response doesn't name the team.</returns>
    public static Squad? Map(EspnRosterResponse response, string leagueCode)
    {
        if (string.IsNullOrEmpty(response.Team?.Id) || string.IsNullOrEmpty(response.Team.DisplayName))
        {
            return null;
        }

        var players = (response.Athletes ?? [])
            .Where(a => !string.IsNullOrEmpty(a.Id) && !string.IsNullOrEmpty(a.DisplayName))
            .Select(a => new SquadPlayer(
                a.Id!,
                a.DisplayName!,
                string.IsNullOrEmpty(a.Jersey) ? null : a.Jersey,
                MapPosition(a.Position?.Abbreviation),
                a.Age,
                string.IsNullOrEmpty(a.Citizenship) ? null : a.Citizenship))
            // Unknown positions and players without a number go last.
            .OrderBy(p => p.Position ?? (PlayerPosition)int.MaxValue)
            .ThenBy(p => int.TryParse(p.Jersey, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : int.MaxValue)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        return new Squad(leagueCode, response.Team.Id, response.Team.DisplayName, players);
    }

    private static PlayerPosition? MapPosition(string? abbreviation) => abbreviation switch
    {
        "G" => PlayerPosition.Goalkeeper,
        "D" => PlayerPosition.Defender,
        "M" => PlayerPosition.Midfielder,
        "F" => PlayerPosition.Forward,
        _ => null,
    };
}
