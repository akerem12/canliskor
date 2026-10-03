using CanliSkor.Core.Domain;
using CanliSkor.Infrastructure.Espn;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Tests.Espn;

// Fixtures are real ESPN summaries captured on 2026-10-03, reduced to the sections we read
// (header, boxscore, keyEvents). Valencia 2-3 Real Sociedad has every event type we map.
public class EspnSummaryMapperTests
{
    private static MatchDetail MapFinished() =>
        EspnSummaryMapper.Map(FixtureLoader.LoadSummary("summary-esp1-finished.json"), "esp.1")!;

    [Fact]
    public void Maps_the_match_from_the_header()
    {
        var match = MapFinished().Match;

        Assert.Equal("401882858", match.Id);
        Assert.Equal("esp.1", match.LeagueCode);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 19, 0, 0, TimeSpan.Zero), match.KickoffUtc);
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal("Valencia", match.HomeTeam.Name);
        Assert.Equal("https://a.espncdn.com/i/teamlogos/soccer/500/94.png", match.HomeTeam.LogoUrl);
        Assert.Equal("Real Sociedad", match.AwayTeam.Name);
        Assert.Equal(new Score(2, 3), match.Score);
    }

    [Fact]
    public void Keeps_goals_cards_and_substitutions_in_order()
    {
        var events = MapFinished().Events;

        // Kickoff, half time, delays etc. are left out: 5 goals, 4 yellows, 1 red, 10 substitutions.
        Assert.Equal(20, events.Count);
        Assert.Equal(new MatchEvent(MatchEventType.YellowCard, "11'", TeamSide.Home, "Justin de Haas", null, "282086"), events[0]);
        Assert.Equal(new MatchEvent(MatchEventType.RedCard, "90'+6'", TeamSide.Away, "Orri Óskarsson", null, "341367"), events[^1]);
    }

    [Fact]
    public void Goals_carry_scorer_assist_kind_and_player_ids()
    {
        var goals = MapFinished().Events
            .Where(e => e.Type is MatchEventType.Goal or MatchEventType.PenaltyGoal or MatchEventType.OwnGoal)
            .ToList();

        Assert.Equal(
        [
            new MatchEvent(MatchEventType.Goal, "26'", TeamSide.Away, "Luka Sucic", "Job Ochieng", "306263", "406379"),
            new MatchEvent(MatchEventType.Goal, "57'", TeamSide.Home, "Aaron Mayol", "Hugo Duro", "419048", "267477"),
            // A penalty and an own goal have nobody assisting.
            new MatchEvent(MatchEventType.PenaltyGoal, "75'", TeamSide.Away, "Carlos Soler", null, "235072"),
            // Scored by a Real Sociedad player, so it counts for Valencia.
            new MatchEvent(MatchEventType.OwnGoal, "84'", TeamSide.Home, "Luken Beitia", null, "396607"),
            new MatchEvent(MatchEventType.Goal, "90'+1'", TeamSide.Away, "Ander Barrenetxea", "Carlos Soler", "283941", "235072"),
        ], goals);

        // Every goal accounted for: the events add up to the final score.
        Assert.Equal(2, goals.Count(g => g.Side == TeamSide.Home));
        Assert.Equal(3, goals.Count(g => g.Side == TeamSide.Away));
    }

    [Fact]
    public void Substitutions_list_the_player_coming_on_first()
    {
        var sub = MapFinished().Events.First(e => e.Type == MatchEventType.Substitution);

        Assert.Equal(new MatchEvent(MatchEventType.Substitution, "45'", TeamSide.Home, "David Otorbi", "Filip Ugrinic", "377768", "248681"), sub);
    }

    [Fact]
    public void Maps_the_shown_statistics_in_display_order()
    {
        Assert.Equal(
        [
            new MatchStat(MatchStatType.Possession, 54.1, 45.9),
            new MatchStat(MatchStatType.Shots, 22, 21),
            new MatchStat(MatchStatType.ShotsOnTarget, 5, 6),
            new MatchStat(MatchStatType.Corners, 7, 5),
            new MatchStat(MatchStatType.Fouls, 9, 12),
            new MatchStat(MatchStatType.Offsides, 2, 2),
            new MatchStat(MatchStatType.YellowCards, 2, 1),
            new MatchStat(MatchStatType.RedCards, 0, 1),
            new MatchStat(MatchStatType.Saves, 3, 4),
        ], MapFinished().Stats);
    }

    [Fact]
    public void Scheduled_match_has_no_events_and_ignores_season_statistics()
    {
        var detail = EspnSummaryMapper.Map(FixtureLoader.LoadSummary("summary-tur1-scheduled.json"), "tur.1")!;

        Assert.Equal(MatchStatus.Scheduled, detail.Match.Status);
        Assert.Null(detail.Match.Score);
        Assert.Empty(detail.Events);
        Assert.Empty(detail.Stats);
    }

    [Fact]
    public void Odds_come_from_the_pickcenter_section()
    {
        var summary = FixtureLoader.LoadSummary("summary-tur1-scheduled.json") with
        {
            Pickcenter = [new EspnOdds(new EspnOddsProvider("DraftKings"), null, new EspnTeamOdds(-450), new EspnTeamOdds(950), new EspnTeamOdds(500))],
        };

        Assert.Equal(new MatchOdds(1.22m, 6.00m, 10.50m, "DraftKings"), EspnSummaryMapper.Map(summary, "tur.1")!.Match.Odds);
    }

    [Fact]
    public void Summary_without_odds_has_none()
    {
        Assert.Null(MapFinished().Match.Odds);
    }

    [Fact]
    public void Match_of_another_league_is_rejected()
    {
        // ESPN serves any match under any league's URL.
        Assert.Null(EspnSummaryMapper.Map(FixtureLoader.LoadSummary("summary-esp1-finished.json"), "tur.1"));
    }
}
