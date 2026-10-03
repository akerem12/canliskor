using System.Globalization;
using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// Pure translation of ESPN's betting lines to <see cref="MatchOdds"/>. ESPN quotes American odds
/// ("-450", "+950"); we expose the decimal odds used in Europe (1.22, 10.50).
/// </summary>
internal static class EspnOddsMapper
{
    /// <returns>The first bookmaker's prices that are complete; null if no line has all three results priced.</returns>
    public static MatchOdds? Map(IReadOnlyList<EspnOdds?>? odds)
    {
        foreach (var line in odds ?? [])
        {
            if (line is null)
            {
                continue;
            }

            var home = Price(line.Moneyline?.Home) ?? ToDecimal(line.HomeTeamOdds?.MoneyLine);
            var draw = Price(line.Moneyline?.Draw) ?? ToDecimal(line.DrawOdds?.MoneyLine);
            var away = Price(line.Moneyline?.Away) ?? ToDecimal(line.AwayTeamOdds?.MoneyLine);

            if (home is not null && draw is not null && away is not null)
            {
                return new MatchOdds(home.Value, draw.Value, away.Value, string.IsNullOrWhiteSpace(line.Provider?.Name) ? null : line.Provider.Name);
            }
        }

        return null;
    }

    /// <summary>The closing price is the current one; the opening price stands in if it is missing.</summary>
    private static decimal? Price(EspnMoneylinePrices? prices) =>
        ParseAmerican(prices?.Close?.Odds) ?? ParseAmerican(prices?.Open?.Odds);

    private static decimal? ParseAmerican(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // "EVEN" is +100: the stake is doubled.
        if (text.Trim().Equals("EVEN", StringComparison.OrdinalIgnoreCase))
        {
            return 2.00m;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var american)
            ? ToDecimal(american)
            : null;
    }

    /// <summary>+950 pays 950 on a stake of 100 (10.50); -450 needs a stake of 450 to win 100 (1.22).</summary>
    internal static decimal? ToDecimal(double? american) => american switch
    {
        null or 0 => null,
        // Guards against junk such as "-1": real American odds are never between -100 and +100.
        > -100 and < 100 => null,
        > 0 => Math.Round(1 + (decimal)american.Value / 100, 2),
        _ => Math.Round(1 + 100 / (decimal)-american.Value, 2),
    };
}
