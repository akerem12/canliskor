using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Tests.Espn;

// Fixtures are real ESPN responses captured on 2026-10-03, reduced to the fields we read:
// Besiktas' Süper Lig results and fixtures, and the Süper Lig table.
public class EspnTeamMapperTests
{
    private static TeamProfile MapBesiktas() => EspnTeamMapper.MapProfile(
        FixtureLoader.Load<EspnScheduleResponse>("schedule-tur1-besiktas-results.json"),
        FixtureLoader.Load<EspnScheduleResponse>("schedule-tur1-besiktas-fixtures.json"),
        "tur.1")!;

    private static LeagueStandings MapStandings() =>
        EspnTeamMapper.MapStandings(FixtureLoader.Load<EspnStandingsResponse>("standings-tur1.json"), "tur.1")!;

    [Fact]
    public void Maps_who_the_team_is()
    {
        var profile = MapBesiktas();

        Assert.Equal("tur.1", profile.LeagueCode);
        Assert.Equal(new Team("1895", "Besiktas", "Besiktas", "https://a.espncdn.com/i/teamlogos/soccer/500/1895.png"), profile.Team);
        Assert.False(profile.IsNationalTeam);
        Assert.Equal("3rd in Turkish Super Lig", profile.StandingSummary);
        Assert.Equal([new League("tur.1", "Turkish Super Lig")], profile.Competitions);
    }

    [Fact]
    public void Stadium_is_where_the_home_matches_are_played()
    {
        var profile = MapBesiktas();

        Assert.Equal("Vodafone Park", profile.Stadium);
        Assert.Equal("Istanbul", profile.StadiumCity);
    }

    [Fact]
    public void Recent_matches_are_the_results_newest_first()
    {
        var recent = MapBesiktas().RecentMatches;

        Assert.Equal(6, recent.Count);
        Assert.All(recent, m => Assert.Equal(MatchStatus.Finished, m.Status));
        Assert.Equal(recent.OrderByDescending(m => m.KickoffUtc), recent);

        // Amed SFK 3-2 Besiktas, away.
        var latest = recent[0];
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 17, 0, 0, TimeSpan.Zero), latest.KickoffUtc);
        Assert.Equal("tur.1", latest.LeagueCode);
        Assert.Equal("Amed SFK", latest.HomeTeam.Name);
        Assert.Equal("Besiktas", latest.AwayTeam.Name);
        Assert.Equal(new Score(3, 2), latest.Score);
        Assert.Equal("Diyarbakir Stadyumu", latest.Venue);
    }

    [Fact]
    public void Upcoming_matches_are_the_fixtures_soonest_first()
    {
        var upcoming = MapBesiktas().UpcomingMatches;

        Assert.Equal(28, upcoming.Count);
        Assert.Equal(upcoming.OrderBy(m => m.KickoffUtc), upcoming);

        var next = upcoming[0];
        Assert.Equal(MatchStatus.Scheduled, next.Status);
        Assert.Null(next.Score);
        Assert.Equal("Besiktas", next.HomeTeam.Name);
        Assert.Equal("Kocaelispor", next.AwayTeam.Name);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 16, 0, 0, TimeSpan.Zero), next.KickoffUtc);
    }

    [Fact]
    public void Matches_keep_their_own_competition_and_the_competitions_are_named()
    {
        static EspnScheduleEvent Event(string id, string date, string slug, string name) => new(id, date, new EspnLeague(name, name, slug),
        [
            new EspnScheduleCompetition(new EspnStatus(null, new EspnStatusType("STATUS_SCHEDULED", "pre")), null,
            [
                new EspnScheduleCompetitor("home", new EspnTeam("432", "Galatasaray", "Galatasaray", null), null),
                new EspnScheduleCompetitor("away", new EspnTeam("83", "Barcelona", "Barcelona", null), null),
            ]),
        ]);
        var team = new EspnScheduleTeam("432", "Galatasaray", null, "2nd in Turkish Super Lig", IsNational: false);
        var fixtures = new EspnScheduleResponse(team,
        [
            Event("2", "2026-10-13T19:00Z", "uefa.champions", "UEFA Champions League"),
            Event("1", "2026-10-09T17:00Z", "tur.1", "Turkish Super Lig"),
            Event("3", "2026-10-17T17:00Z", "tur.1", "Turkish Super Lig"),
        ]);

        var profile = EspnTeamMapper.MapProfile(new EspnScheduleResponse(team, []), fixtures, "tur.1")!;

        Assert.Equal(["tur.1", "uefa.champions", "tur.1"], profile.UpcomingMatches.Select(m => m.LeagueCode));
        Assert.Equal(
            [new League("uefa.champions", "UEFA Champions League"), new League("tur.1", "Turkish Super Lig")],
            profile.Competitions);
    }

    [Fact]
    public void National_team_has_no_stadium()
    {
        var results = FixtureLoader.Load<EspnScheduleResponse>("schedule-tur1-besiktas-results.json");
        var national = results with { Team = results.Team! with { IsNational = true } };

        var profile = EspnTeamMapper.MapProfile(national, new EspnScheduleResponse(null, []), "uefa.nations")!;

        Assert.True(profile.IsNationalTeam);
        Assert.Null(profile.Stadium);
    }

    [Fact]
    public void Schedule_without_a_team_is_rejected()
    {
        Assert.Null(EspnTeamMapper.MapProfile(new EspnScheduleResponse(null, []), new EspnScheduleResponse(null, []), "tur.1"));
    }

    [Fact]
    public void Maps_the_table_in_order()
    {
        var standings = MapStandings();

        Assert.Equal("Turkish Super Lig", standings.LeagueName);
        var rows = Assert.Single(standings.Groups).Rows;
        Assert.Equal(18, rows.Count);
        Assert.Equal(Enumerable.Range(1, 18), rows.Select(r => r.Rank));
    }

    [Fact]
    public void Row_carries_the_numbers_and_what_the_position_means()
    {
        var besiktas = MapStandings().Groups[0].Rows.Single(r => r.Team.Id == "1895");

        Assert.Equal(
            new StandingsRow(
                Rank: 3,
                new Team("1895", "Besiktas", "Besiktas", "https://a.espncdn.com/i/teamlogos/soccer/500/1895.png"),
                Played: 6, Wins: 4, Draws: 0, Losses: 2, GoalsFor: 14, GoalsAgainst: 7, GoalDifference: 7, Points: 12,
                Note: "Europa League qualifying", NoteColor: "#b2bfd0"),
            besiktas);
    }

    [Fact]
    public void Competition_without_a_table_has_no_groups()
    {
        var standings = EspnTeamMapper.MapStandings(new EspnStandingsResponse("International Friendly", []), "fifa.friendly")!;

        Assert.Empty(standings.Groups);
    }

    [Fact]
    public void National_teams_in_a_table_have_no_club_crest()
    {
        var response = new EspnStandingsResponse("UEFA Nations League",
        [
            new EspnStandingsGroup("Group A1", new EspnStandingsTable(
            [
                new EspnStandingsEntry(new EspnStandingsTeam("465", "Türkiye", "Türkiye", IsNational: true), null,
                    [new EspnStandingsStat("rank", 1), new EspnStandingsStat("points", 7)]),
                new EspnStandingsEntry(new EspnStandingsTeam(null, "Nameless", null, false), null, []),
            ])),
        ]);

        var row = Assert.Single(Assert.Single(EspnTeamMapper.MapStandings(response, "uefa.nations")!.Groups).Rows);

        Assert.Equal("Group A1", EspnTeamMapper.MapStandings(response, "uefa.nations")!.Groups[0].Name);
        Assert.Null(row.Team.LogoUrl);
        Assert.Equal(7, row.Points);
        Assert.Null(row.Note);
    }
}
