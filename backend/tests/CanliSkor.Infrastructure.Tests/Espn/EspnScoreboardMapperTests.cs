using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Tests.Espn;

// Fixtures are real ESPN responses captured on 2026-10-03.
public class EspnScoreboardMapperTests
{
    private static readonly DateOnly Date = new(2026, 9, 20);

    [Fact]
    public void Maps_finished_matches_from_real_response()
    {
        var response = FixtureLoader.LoadScoreboard("scoreboard-tur1-finished.json");

        var scoreboard = EspnScoreboardMapper.Map(response, "tur.1", Date);

        Assert.Equal(new League("tur.1", "Turkish Super Lig"), scoreboard.League);
        Assert.Equal(Date, scoreboard.Date);
        Assert.Equal(4, scoreboard.Matches.Count);

        var match = Assert.Single(scoreboard.Matches, m => m.Id == "401888287");
        Assert.Equal("tur.1", match.LeagueCode);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 14, 0, 0, TimeSpan.Zero), match.KickoffUtc);
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal("90'+4'", match.Clock);
        Assert.Equal(new Team("436", "Fenerbahce", "Fenerbahce", "https://a.espncdn.com/i/teamlogos/soccer/500/436.png"), match.HomeTeam);
        Assert.Equal("Eyupspor", match.AwayTeam.Name);
        Assert.Equal(new Score(8, 0), match.Score);
    }

    [Fact]
    public void Orders_matches_by_kickoff()
    {
        var response = FixtureLoader.LoadScoreboard("scoreboard-eng1-finished.json");

        var scoreboard = EspnScoreboardMapper.Map(response, "eng.1", Date);

        Assert.Equal(scoreboard.Matches.OrderBy(m => m.KickoffUtc), scoreboard.Matches);
        Assert.Equal("401879269", scoreboard.Matches[0].Id);
    }

    [Fact]
    public void Scheduled_match_has_no_score_or_clock()
    {
        // ESPN reports score "0" and clock "0'" for matches that haven't started.
        var response = FixtureLoader.LoadScoreboard("scoreboard-tur1-scheduled.json");

        var match = Assert.Single(EspnScoreboardMapper.Map(response, "tur.1", Date).Matches);

        Assert.Equal(MatchStatus.Scheduled, match.Status);
        Assert.Null(match.Score);
        Assert.Null(match.Clock);
    }

    [Fact]
    public void Empty_logo_is_mapped_to_null()
    {
        var response = FixtureLoader.LoadScoreboard("scoreboard-tur1-finished.json");

        var match = Assert.Single(EspnScoreboardMapper.Map(response, "tur.1", Date).Matches, m => m.Id == "401888382");

        Assert.Equal("Amed SFK", match.HomeTeam.Name);
        Assert.Null(match.HomeTeam.LogoUrl);
    }

    [Fact]
    public void Live_match_keeps_score_and_clock()
    {
        var response = FixtureLoader.Deserialize(EventJson(state: "in", name: "STATUS_SECOND_HALF", clock: "67'", homeScore: "2", awayScore: "1"));

        var match = Assert.Single(EspnScoreboardMapper.Map(response, "tur.1", Date).Matches);

        Assert.Equal(MatchStatus.Live, match.Status);
        Assert.Equal("67'", match.Clock);
        Assert.Equal(new Score(2, 1), match.Score);
    }

    [Fact]
    public void Skips_malformed_events_instead_of_failing()
    {
        var response = FixtureLoader.Deserialize("""
            { "events": [
                { "id": "1", "date": "not-a-date", "competitions": [] },
                { "id": "2", "date": "2026-09-20T14:00Z", "competitions": [ { "competitors": [] } ] }
            ] }
            """);

        var scoreboard = EspnScoreboardMapper.Map(response, "tur.1", Date);

        Assert.Empty(scoreboard.Matches);
        Assert.Equal("tur.1", scoreboard.League.Name); // falls back to the code when ESPN sends no league info
    }

    [Theory]
    [InlineData("pre", "STATUS_SCHEDULED", MatchStatus.Scheduled)]
    [InlineData("in", "STATUS_FIRST_HALF", MatchStatus.Live)]
    [InlineData("in", "STATUS_HALFTIME", MatchStatus.HalfTime)]
    [InlineData("in", "STATUS_SECOND_HALF", MatchStatus.Live)]
    [InlineData("post", "STATUS_FULL_TIME", MatchStatus.Finished)]
    [InlineData("post", "STATUS_FINAL_PEN", MatchStatus.Finished)]
    [InlineData("post", "STATUS_POSTPONED", MatchStatus.Postponed)]
    [InlineData("post", "STATUS_CANCELED", MatchStatus.Cancelled)]
    [InlineData("in", "STATUS_SOMETHING_NEW", MatchStatus.Live)] // unknown name falls back to state
    [InlineData(null, null, MatchStatus.Scheduled)]
    public void Maps_status(string? state, string? name, MatchStatus expected)
    {
        Assert.Equal(expected, EspnScoreboardMapper.MapStatus(new EspnStatusType(name, state)));
    }

    private static string EventJson(string state, string name, string clock, string homeScore, string awayScore) => $$"""
        { "events": [ {
            "id": "100",
            "date": "2026-09-20T17:00Z",
            "status": { "displayClock": "{{clock}}", "type": { "name": "{{name}}", "state": "{{state}}" } },
            "competitions": [ { "competitors": [
                { "homeAway": "home", "score": "{{homeScore}}", "team": { "id": "1", "displayName": "Home FC", "shortDisplayName": "Home" } },
                { "homeAway": "away", "score": "{{awayScore}}", "team": { "id": "2", "displayName": "Away FC", "shortDisplayName": "Away" } }
            ] } ]
        } ] }
        """;
}
