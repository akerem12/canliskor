using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn;

namespace CanliSkor.Infrastructure.Tests.Espn;

// The fixture is a real ESPN summary captured on 2026-10-03, reduced to the sections and fields we read:
// Amed SFK 3-2 Besiktas, 4-2-3-1 against 4-1-4-1, nine substitutions and a substitute sent off.
public class EspnLineupMapperTests
{
    private static MatchLineups MapFinished() =>
        EspnSummaryMapper.Map(FixtureLoader.LoadSummary("summary-tur1-lineups.json"), "tur.1")!.Lineups!;

    private static string[][] Names(TeamLineup lineup) =>
        lineup.Rows.Select(row => row.Select(p => p.Name).ToArray()).ToArray();

    [Fact]
    public void Home_team_lines_up_in_its_formation_left_to_right()
    {
        var home = MapFinished().Home;

        Assert.Equal("4-2-3-1", home.Formation);
        Assert.Equal(
        [
            ["Alban Lafont"],
            ["Umut Meras", "David Bates", "Lumbardh Dellova", "Ermal Krasniqi"],
            ["Rayan Raveloson", "Furkan Soyalp"],
            ["Mohamed Khalil", "Dia Saba", "Samuel Ballet"],
            ["Gift Orban"],
        ], Names(home));
    }

    [Fact]
    public void Away_team_lines_up_in_its_formation_left_to_right()
    {
        var away = MapFinished().Away;

        Assert.Equal("4-1-4-1", away.Formation);
        Assert.Equal(
        [
            ["Alexander Nübel"],
            ["Ridvan Yilmaz", "Tiago Djaló", "Emmanuel Agbadou", "Amir Murillo"],
            ["Salih Özcan"],
            ["Ilhan Fakili", "Orkun Kökçü", "Junior Olaitan", "Ernest Poku"],
            ["Dusan Vlahovic"],
        ], Names(away));
    }

    [Fact]
    public void Maps_shirt_colours_and_bench()
    {
        var lineups = MapFinished();

        Assert.Equal("#990000", lineups.Home.ShirtColor);
        Assert.Equal("#ffffff", lineups.Away.ShirtColor);
        Assert.Equal(10, lineups.Home.Bench.Count);
        Assert.Equal(10, lineups.Away.Bench.Count);
        Assert.Equal("Amadou Cissé", lineups.Home.Bench[0].Name);
    }

    [Fact]
    public void Starter_carries_identity_position_and_statistics()
    {
        var orban = MapFinished().Home.Rows[^1].Single();

        Assert.Equal(
            new LineupPlayer("356402", "Gift Orban", "G. Orban", "99", PlayerPosition.Forward, CameOnAt: null, WentOffAt: "76'",
                new PlayerMatchStats(Goals: 1, Assists: 1, Shots: 3, ShotsOnTarget: 2, FoulsCommitted: 3, FoulsSuffered: 1,
                    Offsides: 0, YellowCards: 0, RedCards: 0, OwnGoals: 0, Saves: 0, GoalsConceded: 1)),
            orban);
    }

    [Fact]
    public void Positions_follow_the_rows()
    {
        var home = MapFinished().Home;

        Assert.Equal(
        [
            PlayerPosition.Goalkeeper, PlayerPosition.Defender, PlayerPosition.Midfielder,
            PlayerPosition.Midfielder, PlayerPosition.Forward,
        ], home.Rows.Select(row => row.Select(p => p.Position).Distinct().Single()));

        var keeper = home.Rows[0].Single();
        Assert.Equal(5, keeper.Stats.Saves);
        Assert.Equal(2, keeper.Stats.GoalsConceded);
        Assert.Null(keeper.WentOffAt);
    }

    [Fact]
    public void Substitute_who_came_on_takes_the_position_of_the_player_replaced()
    {
        var bench = MapFinished().Home.Bench;

        // Cissé replaced Khalil (attacking midfield) at half time and was sent off later.
        var cisse = bench.Single(p => p.Name == "Amadou Cissé");
        Assert.Equal(PlayerPosition.Midfielder, cisse.Position);
        Assert.Equal("45'", cisse.CameOnAt);
        Assert.Null(cisse.WentOffAt);
        Assert.Equal(1, cisse.Stats.RedCards);

        // Bülbül replaced the left back.
        Assert.Equal(PlayerPosition.Defender, bench.Single(p => p.Name == "Ali Bülbül").Position);
    }

    [Fact]
    public void Unused_substitute_has_no_position_or_minutes()
    {
        var unused = MapFinished().Home.Bench.Single(p => p.Name == "Mbaye Diagne");

        Assert.Null(unused.Position);
        Assert.Null(unused.CameOnAt);
        Assert.Null(unused.WentOffAt);
    }

    [Fact]
    public void Scheduled_match_without_announced_lineups_has_none()
    {
        // ESPN already sends both rosters then, but with the team only.
        var detail = EspnSummaryMapper.Map(FixtureLoader.LoadSummary("summary-tur1-scheduled.json"), "tur.1")!;

        Assert.Null(detail.Lineups);
    }

    [Fact]
    public void Summary_without_rosters_has_no_lineups()
    {
        var detail = EspnSummaryMapper.Map(FixtureLoader.LoadSummary("summary-esp1-finished.json"), "esp.1")!;

        Assert.Null(detail.Lineups);
    }

    [Theory]
    // Back three with wing-backs listed as midfielders.
    [InlineData("3-4-2-1", "CD-L CD CD-R LM CM-L CM-R RM AM-L AM-R F", "CD-L CD CD-R|LM CM-L CM-R RM|AM-L AM-R|F")]
    // Holding midfielder between the lines; ESPN's order doesn't matter.
    [InlineData("4-1-4-1", "F RM LM CM-R CM-L DM RB LB CD-R CD-L", "LB CD-L CD-R RB|DM|LM CM-L CM-R RM|F")]
    [InlineData("4-4-2", "CD-L CD-R LB RB LM RM CM-L CM-R CF-L CF-R", "LB CD-L CD-R RB|LM CM-L CM-R RM|CF-L CF-R")]
    [InlineData("4-3-3", "LB CD-L CD-R RB CM-L CM CM-R LF F RF", "LB CD-L CD-R RB|CM-L CM CM-R|LF F RF")]
    [InlineData("5-3-2", "LB CD-L CD CD-R RB CM-L CM CM-R CF-L CF-R", "LB CD-L CD CD-R RB|CM-L CM CM-R|CF-L CF-R")]
    public void Arranges_positions_into_formation_rows(string formation, string positions, string expectedRows)
    {
        var rows = EspnLineupMapper.Arrange(positions.Split(' '), p => p, EspnLineupMapper.ParseFormation(formation));

        Assert.Equal(expectedRows, string.Join('|', rows.Select(row => string.Join(' ', row))));
    }

    [Theory]
    // Formation missing.
    [InlineData(null)]
    // Not a formation.
    [InlineData("4-4-x")]
    // Doesn't add up to the ten outfield players.
    [InlineData("4-4-3")]
    public void Falls_back_to_rows_by_depth_when_the_formation_is_unusable(string? formation)
    {
        var positions = "CD-L CD-R LB RB DM CM-L CM-R AM F F".Split(' ');

        var rows = EspnLineupMapper.Arrange(positions, p => p, EspnLineupMapper.ParseFormation(formation));

        Assert.Equal("LB CD-L CD-R RB|DM|CM-L CM-R|AM|F F", string.Join('|', rows.Select(row => string.Join(' ', row))));
    }

    [Fact]
    public void Team_reduced_to_ten_starters_still_lines_up()
    {
        // Nine outfield players against a 4-4-2: rows by depth instead of a row with a hole in it.
        var positions = "LB CD-L CD-R RB LM CM RM CF-L CF-R".Split(' ');

        var rows = EspnLineupMapper.Arrange(positions, p => p, EspnLineupMapper.ParseFormation("4-4-2"));

        Assert.Equal([4, 3, 2], rows.Select(r => r.Count));
    }

    [Fact]
    public void Unknown_position_goes_to_central_midfield()
    {
        var positions = "LB CD-L CD-R RB LM ?? RM CF-L CF-R F".Split(' ');

        var rows = EspnLineupMapper.Arrange(positions, p => p, EspnLineupMapper.ParseFormation("4-3-3"));

        Assert.Equal("LM ?? RM", string.Join(' ', rows[1]));
    }
}
