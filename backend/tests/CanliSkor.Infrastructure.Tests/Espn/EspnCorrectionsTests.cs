using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn;

namespace CanliSkor.Infrastructure.Tests.Espn;

// ESPN has Galatasaray's Gabriel Sara under the record of Gabriel Gomes (268478), a defender at FC Imabari.
public class EspnCorrectionsTests
{
    private const string WrongRecord = "268478";

    private static readonly PlayerMatchStats NoStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void The_squad_shows_the_right_name_and_position_and_moves_him_to_midfield()
    {
        var squad = new Squad("tur.1", "432", "Galatasaray",
        [
            new SquadPlayer("1", "Keeper", "1", PlayerPosition.Goalkeeper, 30, "Uruguay"),
            new SquadPlayer("6", "Defender", "6", PlayerPosition.Defender, 30, "Colombia"),
            new SquadPlayer(WrongRecord, "Gabriel Gomes", "8", PlayerPosition.Defender, 27, "Brazil", Season: new SquadPlayerSeason(7, 2, 2)),
            new SquadPlayer("10", "Midfielder", "10", PlayerPosition.Midfielder, 25, "Türkiye"),
        ]);

        var corrected = EspnCorrections.Apply(squad);

        Assert.Equal(["Keeper", "Defender", "Gabriel Sara", "Midfielder"], corrected.Players.Select(p => p.Name));
        var sara = corrected.Players[2];
        Assert.Equal(PlayerPosition.Midfielder, sara.Position);
        // The age is the other person's; the season's numbers are his.
        Assert.Null(sara.Age);
        Assert.Equal(new SquadPlayerSeason(7, 2, 2), sara.Season);
    }

    [Fact]
    public void Line_ups_and_events_name_him_right()
    {
        var match = new Match("1", "tur.1", DateTimeOffset.UnixEpoch, MatchStatus.Finished, null,
            new Team("432", "Galatasaray", "Galatasaray", null), new Team("2", "Goztepe", "Goztepe", null), new Score(1, 0));
        var player = new LineupPlayer(WrongRecord, "Gabriel Gomes", "G. Gomes", "8", PlayerPosition.Midfielder, null, null, NoStats);
        var lineup = new TeamLineup("4-2-3-1", null, [[player]], []);
        var detail = new MatchDetail(match,
            [
                new MatchEvent(MatchEventType.Goal, "30'", TeamSide.Home, "Gabriel Gomes", "Someone", WrongRecord, "7"),
                new MatchEvent(MatchEventType.Goal, "60'", TeamSide.Home, "Someone", "Gabriel Gomes", "7", WrongRecord),
            ],
            [],
            new MatchLineups(lineup, lineup with { Rows = [], Bench = [player] }));

        var corrected = EspnCorrections.Apply(detail);

        Assert.Equal("Gabriel Sara", corrected.Events[0].Player);
        Assert.Equal("Gabriel Sara", corrected.Events[1].RelatedPlayer);
        Assert.Equal("Someone", corrected.Events[0].RelatedPlayer);
        Assert.Equal(("Gabriel Sara", "G. Sara"), (corrected.Lineups!.Home.Rows[0][0].Name, corrected.Lineups.Home.Rows[0][0].ShortName));
        Assert.Equal("Gabriel Sara", corrected.Lineups.Away.Bench[0].Name);
    }

    [Fact]
    public void The_profile_is_his_galatasaray_season_without_the_other_persons_details()
    {
        var profile = new PlayerProfile(WrongRecord, "Gabriel Gomes", null, PlayerPosition.Defender, "Brazil", null, 27, 188,
            new Team("132161", "FC IMABARI", "IMABARI", null),
            [Season("Galatasaray"), Season("Brazil")]);

        var corrected = EspnCorrections.Apply(profile);

        Assert.Equal("Gabriel Sara", corrected.Name);
        Assert.Equal(PlayerPosition.Midfielder, corrected.Position);
        Assert.Equal("432", corrected.Team?.Id);
        Assert.Null(corrected.Age);
        Assert.Null(corrected.HeightCm);
        Assert.Equal(["Galatasaray"], corrected.Competitions.Select(c => c.TeamName));
    }

    [Fact]
    public void Everybody_else_is_left_alone()
    {
        var profile = new PlayerProfile("1", "Somebody", "9", PlayerPosition.Forward, null, null, 30, 180, null, []);

        Assert.Same(profile, EspnCorrections.Apply(profile));
    }

    private static PlayerCompetitionStats Season(string team) =>
        new("2026-27 Turkish Super Lig", "tur.1", team, 7, 0, 2, 2, 10, 5, 1, 0, 6, 9, 1, null, null, null);
}
