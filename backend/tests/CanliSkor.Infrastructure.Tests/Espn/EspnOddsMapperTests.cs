using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Tests.Espn;

public class EspnOddsMapperTests
{
    private static EspnOdds Line(string? home, string? draw, string? away, string? provider = "DraftKings") => new(
        new EspnOddsProvider(provider),
        new EspnMoneyline(Prices(home), Prices(away), Prices(draw)));

    private static EspnMoneylinePrices Prices(string? close, string? open = null) =>
        new(open is null ? null : new EspnMoneylinePrice(open), close is null ? null : new EspnMoneylinePrice(close));

    [Theory]
    [InlineData(-450, 1.22)]
    [InlineData(950, 10.50)]
    [InlineData(500, 6.00)]
    [InlineData(100, 2.00)]
    [InlineData(-100, 2.00)]
    [InlineData(-110, 1.91)]
    public void American_odds_become_decimal_odds(double american, double expected)
    {
        Assert.Equal((decimal)expected, EspnOddsMapper.ToDecimal(american));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(50d)]
    public void Prices_that_are_not_american_odds_are_rejected(double? american)
    {
        Assert.Null(EspnOddsMapper.ToDecimal(american));
    }

    [Fact]
    public void Maps_the_three_results_and_the_bookmaker()
    {
        Assert.Equal(new MatchOdds(1.22m, 6.00m, 10.50m, "DraftKings"), EspnOddsMapper.Map([Line("-450", "+500", "+950")]));
    }

    [Fact]
    public void Even_money_is_two()
    {
        Assert.Equal(new MatchOdds(2.00m, 3.40m, 3.60m, null), EspnOddsMapper.Map([Line("EVEN", "+240", "+260", provider: "")]));
    }

    [Fact]
    public void Opening_price_stands_in_for_a_missing_closing_price()
    {
        var line = new EspnOdds(null, new EspnMoneyline(Prices(null, open: "-200"), Prices("+500"), Prices("+300")));

        Assert.Equal(new MatchOdds(1.50m, 4.00m, 6.00m, null), EspnOddsMapper.Map([line]));
    }

    [Fact]
    public void Incomplete_lines_are_skipped_for_the_next_complete_one()
    {
        var odds = EspnOddsMapper.Map([null, Line("-450", null, "+950"), Line("+150", "+220", "+170", "Bet365")]);

        Assert.Equal(new MatchOdds(2.50m, 3.20m, 2.70m, "Bet365"), odds);
    }

    [Fact]
    public void No_complete_line_means_no_odds()
    {
        Assert.Null(EspnOddsMapper.Map(null));
        Assert.Null(EspnOddsMapper.Map([]));
        Assert.Null(EspnOddsMapper.Map([null]));
        Assert.Null(EspnOddsMapper.Map([Line("-450", "junk", "+950")]));
    }

    [Theory]
    [InlineData(1.22, 10.50, TeamSide.Home)]
    [InlineData(3.10, 2.20, TeamSide.Away)]
    public void The_shorter_price_is_the_favourite(double home, double away, TeamSide favourite)
    {
        Assert.Equal(favourite, new MatchOdds((decimal)home, 3.50m, (decimal)away, null).Favorite);
    }

    [Fact]
    public void Level_prices_have_no_favourite()
    {
        Assert.Null(new MatchOdds(2.60m, 3.10m, 2.60m, null).Favorite);
    }
}
