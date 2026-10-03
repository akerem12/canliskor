using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Tests.Espn;

// The fixture is a real ESPN roster captured on 2026-10-03 (Besiktas), reduced to the fields we read.
public class EspnRosterMapperTests
{
    private static Squad MapBesiktas() =>
        EspnRosterMapper.Map(FixtureLoader.LoadRoster("roster-tur1-besiktas.json"), "tur.1")!;

    [Fact]
    public void Maps_the_team_and_every_player()
    {
        var squad = MapBesiktas();

        Assert.Equal("tur.1", squad.LeagueCode);
        Assert.Equal("1895", squad.TeamId);
        Assert.Equal("Besiktas", squad.TeamName);
        Assert.Equal(30, squad.Players.Count);
    }

    [Fact]
    public void Player_carries_number_position_age_and_nationality()
    {
        Assert.Equal(
            new SquadPlayer("205750", "Alexander Nübel", "1", PlayerPosition.Goalkeeper, 30, "Germany"),
            MapBesiktas().Players[0]);
    }

    [Fact]
    public void Orders_by_position_then_shirt_number()
    {
        var players = MapBesiktas().Players;

        Assert.Equal(
            [PlayerPosition.Goalkeeper, PlayerPosition.Defender, PlayerPosition.Midfielder, PlayerPosition.Forward],
            players.Select(p => p.Position!.Value).Distinct());
        Assert.Equal(["1", "80", "96"], players.Where(p => p.Position == PlayerPosition.Goalkeeper).Select(p => p.Jersey));
    }

    [Fact]
    public void Missing_details_are_null()
    {
        // ESPN has no age or citizenship for this youth player.
        var sevim = MapBesiktas().Players.Single(p => p.Name == "Ozan Sevim");

        Assert.Null(sevim.Age);
        Assert.Null(sevim.Nationality);
    }

    [Fact]
    public void Players_without_number_or_position_go_last_and_nameless_ones_are_skipped()
    {
        var response = new EspnRosterResponse(new EspnTeam("1", "Team", "Team", null),
        [
            new EspnRosterAthlete("3", "No Position", "5", null, null, null),
            new EspnRosterAthlete("2", "No Number", "", new EspnPosition("D"), null, ""),
            new EspnRosterAthlete("1", "Defender", "4", new EspnPosition("D"), 25, "Türkiye"),
            new EspnRosterAthlete("4", null, "9", new EspnPosition("F"), null, null),
        ]);

        var squad = EspnRosterMapper.Map(response, "tur.1")!;

        Assert.Equal(["Defender", "No Number", "No Position"], squad.Players.Select(p => p.Name));
        Assert.Null(squad.Players[1].Jersey);
        Assert.Null(squad.Players[1].Nationality);
    }

    [Fact]
    public void Response_without_a_team_is_rejected()
    {
        Assert.Null(EspnRosterMapper.Map(new EspnRosterResponse(null, []), "tur.1"));
    }
}
