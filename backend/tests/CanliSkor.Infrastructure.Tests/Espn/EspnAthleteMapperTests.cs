using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Tests.Espn;

// Fixtures are real ESPN athlete pages captured on 2026-10-04 (Leroy Sané and the goalkeeper Ugurcan Çakir,
// both Galatasaray), reduced to the fields we read.
public class EspnAthleteMapperTests
{
    private static PlayerProfile Map(string name) => EspnAthleteMapper.Map(
        FixtureLoader.Load<EspnAthleteResponse>($"athlete-{name}.json"),
        FixtureLoader.Load<EspnAthleteOverviewResponse>($"athlete-{name}-overview.json"))!;

    [Fact]
    public void Maps_who_the_player_is()
    {
        var player = Map("sane");

        Assert.Equal("202641", player.Id);
        Assert.Equal("Leroy Sané", player.Name);
        Assert.Equal("10", player.Jersey);
        Assert.Equal(PlayerPosition.Forward, player.Position);
        Assert.Equal("Germany", player.Nationality);
        Assert.Equal("https://a.espncdn.com/i/teamlogos/countries/500/ger.png", player.FlagUrl);
        Assert.Equal(30, player.Age);
        Assert.Equal(183, player.HeightCm);
        Assert.Equal(new Team("432", "Galatasaray", "Galatasaray", "https://a.espncdn.com/i/teamlogos/soccer/500/432.png"), player.Team);
    }

    [Fact]
    public void Player_without_a_club_or_statistics_is_still_a_player()
    {
        var response = new EspnAthleteResponse(new EspnAthleteProfile(
            "45843", "Lionel Messi", "10", new EspnPosition("F"), null, "5' 7\"", 39, "Argentina", null, null));

        var player = EspnAthleteMapper.Map(response, null)!;

        Assert.Equal(170, player.HeightCm);
        Assert.Null(player.FlagUrl);
        Assert.Null(player.Team);
        Assert.Empty(player.Competitions);
    }

    [Fact]
    public void Lists_every_competition_of_the_season_with_the_main_league_first()
    {
        var competitions = Map("sane").Competitions;

        Assert.Equal(
            ["2026-27 Turkish Super Lig", "2026-27 Champions League", "2026 International Friendly", "2026 FIFA World Cup"],
            competitions.Select(c => c.Name));
        Assert.Equal(["tur.1", "uefa.champions", "fifa.friendly", "fifa.world"], competitions.Select(c => c.LeagueCode));
        // Club competitions name the club, internationals the national team.
        Assert.Equal(["Galatasaray", "Galatasaray", "Germany", "Germany"], competitions.Select(c => c.TeamName));
    }

    [Fact]
    public void Competition_carries_the_season_totals()
    {
        Assert.Equal(
            new PlayerCompetitionStats("2026-27 Turkish Super Lig", "tur.1", "Galatasaray",
                Starts: 3, SubstituteAppearances: 3, Goals: 0, Assists: 0, Shots: 8, ShotsOnTarget: 3,
                YellowCards: 1, RedCards: 0, FoulsCommitted: 2, FoulsSuffered: 6, Offsides: 0,
                CleanSheets: null, Saves: null, GoalsConceded: null),
            Map("sane").Competitions[0]);
    }

    [Fact]
    public void Substitute_appearances_are_only_known_for_the_main_league()
    {
        Assert.All(Map("sane").Competitions.Skip(1), c => Assert.Null(c.SubstituteAppearances));
    }

    [Fact]
    public void Goalkeeper_has_clean_sheets_saves_and_goals_conceded()
    {
        var league = Map("cakir").Competitions[0];

        Assert.Equal(PlayerPosition.Goalkeeper, Map("cakir").Position);
        Assert.Equal("2026-27 Turkish Super Lig", league.Name);
        Assert.Equal(5, league.Starts);
        Assert.Equal(0, league.SubstituteAppearances);
        Assert.Equal(2, league.CleanSheets);
        Assert.Equal(8, league.Saves);
        Assert.Equal(8, league.GoalsConceded);
    }

    [Fact]
    public void Player_without_statistics_has_no_competitions()
    {
        // ESPN answers "statistics": {} for a player who hasn't played.
        var athlete = FixtureLoader.Load<EspnAthleteResponse>("athlete-sane.json");

        Assert.Empty(EspnAthleteMapper.Map(athlete, new EspnAthleteOverviewResponse(new EspnAthleteStatistics(null, null, null)))!.Competitions);
        Assert.Empty(EspnAthleteMapper.Map(athlete, null)!.Competitions);
    }

    [Fact]
    public void Response_without_a_player_is_rejected()
    {
        Assert.Null(EspnAthleteMapper.Map(new EspnAthleteResponse(null), null));
    }

    [Theory]
    [InlineData("6' 0\"", 183)]
    [InlineData("5' 11\"", 180)]
    [InlineData("6'3", 191)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("tall", null)]
    public void Height_in_feet_and_inches_becomes_centimetres(string? display, int? expected)
    {
        Assert.Equal(expected, EspnAthleteMapper.ParseHeight(display));
    }
}
