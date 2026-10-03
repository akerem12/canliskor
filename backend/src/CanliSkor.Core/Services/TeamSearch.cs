using System.Globalization;
using System.Text;
using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Services;

/// <summary>A team found by name, with the league it was found in (the one its page opens under).</summary>
public sealed record TeamSearchResult(League League, Team Team);

/// <summary>
/// Finds teams by name. Pure: the caller supplies the leagues and their teams. Matching ignores case and accents,
/// so "besiktas" finds "Beşiktaş" and "sao" finds "São Paulo".
/// </summary>
public static class TeamSearch
{
    public const int MinQueryLength = 2;
    public const int MaxResults = 20;

    /// <param name="leagues">
    /// In display order. A team that plays in several is listed once: under its domestic league if that is among
    /// them (so a club's page opens with its league table rather than a cup group), else under the first.
    /// </param>
    public static IReadOnlyList<TeamSearchResult> Find(string? query, IEnumerable<(League League, IReadOnlyList<Team> Teams)> leagues)
    {
        var wanted = Normalize(query);
        if (wanted.Length < MinQueryLength)
        {
            return [];
        }

        return leagues
            // OrderBy is stable: within domestic leagues and within the rest, display order is kept.
            .OrderBy(l => IsDomestic(l.League.Code) ? 0 : 1)
            .SelectMany(l => l.Teams.Select(team => new TeamSearchResult(l.League, team)))
            .DistinctBy(r => r.Team.Id)
            .Select(r => (Result: r, Name: Normalize(r.Team.Name)))
            .Where(x => x.Name.Contains(wanted, StringComparison.Ordinal))
            // Names that start with the query first ("Gal" → Galatasaray before Portugal), then alphabetically.
            .OrderBy(x => x.Name.StartsWith(wanted, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Take(MaxResults)
            .Select(x => x.Result)
            .ToList();
    }

    /// <summary>Domestic league codes are a country and a tier ("tur.1", "bra.2"); cups and internationals aren't ("uefa.champions").</summary>
    private static bool IsDomestic(string leagueCode)
    {
        var dot = leagueCode.IndexOf('.');
        return dot == 3 && leagueCode.Length > 4 && leagueCode[4..].All(char.IsAsciiDigit);
    }

    /// <summary>Lower case, accents removed, the Turkish dotless ı as i, outer spaces trimmed.</summary>
    internal static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().ToLowerInvariant().Replace('ı', 'i').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
