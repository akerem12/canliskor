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

    /// <summary>The events without their ids, which <see cref="Events_keep_espn_ids"/> covers.</summary>
    private static List<MatchEvent> EventsOf(MatchDetail detail) => [.. detail.Events.Select(e => e with { Id = null })];

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
        var events = EventsOf(MapFinished());

        // Kickoff, half time, delays etc. are left out: 5 goals, 4 yellows, 1 red, 10 substitutions.
        Assert.Equal(20, events.Count);
        Assert.Equal(new MatchEvent(MatchEventType.YellowCard, "11'", TeamSide.Home, "Justin de Haas", null, "282086"), events[0]);
        Assert.Equal(new MatchEvent(MatchEventType.RedCard, "90'+6'", TeamSide.Away, "Orri Óskarsson", null, "341367"), events[^1]);
    }

    [Fact]
    public void Goals_carry_scorer_assist_kind_and_player_ids()
    {
        var goals = EventsOf(MapFinished())
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
    public void Events_keep_espn_ids()
    {
        var events = MapFinished().Events;

        Assert.Equal("52750336", events[0].Id);
        Assert.Equal("52762040", events[^1].Id);
        Assert.All(events, e => Assert.NotNull(e.Id));
    }

    [Theory]
    [InlineData("penalty---saved")]
    [InlineData("penalty---missed")]
    [InlineData("penalty---hit-woodwork")]
    public void Missed_penalties_come_from_the_commentary_in_match_order(string kind)
    {
        var summary = FixtureLoader.LoadSummary("summary-esp1-finished.json");
        var missed = new EspnCommentaryPlay(
            "99", new EspnKeyEventType(kind), new EspnClock("50'", 2940), new EspnCommentaryTeam("Real Sociedad"),
            [new EspnParticipant(new EspnAthlete("Carlos Soler", "235072"))]);
        // Other commentary is no event, and neither is a penalty of a team that isn't playing.
        var commentary = new List<EspnCommentary>
        {
            new(null),
            new(missed with { Id = "1", Type = new EspnKeyEventType("shot-off-target") }),
            new(missed with { Id = "2", Team = new EspnCommentaryTeam("Barcelona") }),
            new(missed),
        };

        var events = EspnSummaryMapper.Map(summary with { Commentary = commentary }, "esp.1")!.Events;

        var penalty = Assert.Single(events, e => e.Type == MatchEventType.PenaltyMissed);
        Assert.Equal(new MatchEvent(MatchEventType.PenaltyMissed, "50'", TeamSide.Away, "Carlos Soler", null, "235072", Id: "99"), penalty);
        // After everything up to the 49th minute, before everything from the 50th on.
        var before = summary.KeyEvents!.Count(k => k.Clock?.Value <= 2940 && MapFinished().Events.Any(e => e.Id == k.Id));
        Assert.Equal(before, events.ToList().IndexOf(penalty));
    }

    [Theory]
    // ESPN's first entry on Fenerbahçe - Alanyaspor named the taker...
    [InlineData("Mason Greenwood", "7", "Valencia", TeamSide.Home, "Mason Greenwood")]
    // ...and its correction the goalkeeper who saved it: then the other team missed, taker unknown.
    [InlineData("Alex Remiro", "1", "Real Sociedad", TeamSide.Home, null)]
    public void A_saved_penalty_is_the_takers_team_s_miss_whoever_the_entry_names(
        string named, string namedId, string namedTeam, TeamSide missedBy, string? taker)
    {
        var summary = FixtureLoader.LoadSummary("summary-esp1-finished.json");
        var save = new EspnCommentary(
            new EspnCommentaryPlay("s1", new EspnKeyEventType("penalty---saved"), new EspnClock("17'", 1000), new EspnCommentaryTeam(namedTeam),
                [new EspnParticipant(new EspnAthlete(named, namedId))]),
            $"{named} ({namedTeam}) Penalty - Saved at 17'");
        // Real Sociedad (away) have Alex Remiro in goal.
        var rosters = new List<EspnRoster>
        {
            new("away", null, null, [new EspnRosterEntry(true, "1", new EspnAthlete("Alex Remiro", "1"), new EspnPosition("G"), false, false, null, null, null)]),
        };

        var missed = EspnSummaryMapper.Map(summary with { Commentary = [save], Rosters = rosters }, "esp.1")!.Events
            .Single(e => e.Type == MatchEventType.PenaltyMissed);

        Assert.Equal((missedBy, taker), (missed.Side, missed.Player));
    }

    [Fact]
    public void A_miss_entered_twice_as_the_takers_miss_and_the_goalkeepers_save_is_one_miss()
    {
        // Fenerbahçe - Alanyaspor as ESPN had it in the end; the commentary gives names only, no ids.
        var summary = FixtureLoader.LoadSummary("summary-esp1-finished.json");
        EspnCommentary Line(string id, string type, string team, string player, string text) => new(
            new EspnCommentaryPlay(id, new EspnKeyEventType(type), new EspnClock("17'", 1000), new EspnCommentaryTeam(team),
                [new EspnParticipant(new EspnAthlete(player))]),
            text);
        var commentary = new List<EspnCommentary>
        {
            Line("1", "penalty---missed", "Valencia", "Hugo Duro", "Hugo Duro (Valencia) Penalty - Missed at 17'"),
            Line("2", "penalty---saved", "Real Sociedad", "Alex Remiro", "Alex Remiro (Real Sociedad) Penalty - Saved at 17'"),
        };
        var rosters = new List<EspnRoster>
        {
            new("away", null, null, [new EspnRosterEntry(true, "1", new EspnAthlete("Álex Remiro", "1"), new EspnPosition("G"), false, false, null, null, null)]),
        };

        var missed = EspnSummaryMapper.Map(summary with { Commentary = commentary, Rosters = rosters }, "esp.1")!.Events
            .Where(e => e.Type == MatchEventType.PenaltyMissed)
            .ToList();

        var only = Assert.Single(missed);
        Assert.Equal((TeamSide.Home, "Hugo Duro"), (only.Side, only.Player));
    }

    [Fact]
    public void A_penalty_given_comes_from_the_commentary_for_the_team_that_was_fouled()
    {
        // Valencia (home) against Real Sociedad (away).
        var summary = FixtureLoader.LoadSummary("summary-esp1-finished.json");
        var commentary = new List<EspnCommentary>
        {
            // With the foul: the play names the team that gave it away exactly.
            new(new EspnCommentaryPlay("p1", new EspnKeyEventType("foul"), new EspnClock("49'", 2937), new EspnCommentaryTeam("Valencia"), null),
                "Penalty conceded by Dean Huijsen (Valencia) after a foul in the penalty area.", 49, new EspnClock("49'", 2937)),
            // The line after it says the same again; it is no second penalty.
            new(new EspnCommentaryPlay("p1", new EspnKeyEventType("foul"), new EspnClock("49'", 2937), new EspnCommentaryTeam("Valencia"), null),
                "Penalty Real Sociedad. Takefusa Kubo draws a foul in the penalty area.", 50, new EspnClock("49'", 2937)),
            // A handball comes without a play, and the text spells the team its own way.
            new(null, "Penalty conceded by Igor Zubeldia (Real Sociedad de Fútbol) with a handball in the penalty area.", 70, new EspnClock("70'", 4150)),
            // A team that isn't playing: nobody's penalty.
            new(null, "Penalty conceded by Someone (Barcelona) with a handball in the penalty area.", 71, new EspnClock("71'", 4200)),
        };

        var penalties = EspnSummaryMapper.Map(summary with { Commentary = commentary }, "esp.1")!.Events
            .Where(e => e.Type == MatchEventType.PenaltyAwarded)
            .ToList();

        Assert.Equal(
        [
            new MatchEvent(MatchEventType.PenaltyAwarded, "49'", TeamSide.Away, null, "Dean Huijsen", Id: "p1"),
            new MatchEvent(MatchEventType.PenaltyAwarded, "70'", TeamSide.Home, null, "Igor Zubeldia", Id: "commentary:70"),
        ], penalties);
    }

    [Fact]
    public void A_penalty_var_takes_back_comes_from_the_commentary_for_the_team_it_was_for()
    {
        // Valencia (home) against Real Sociedad (away).
        var summary = FixtureLoader.LoadSummary("summary-esp1-finished.json");
        EspnCommentary Var(string type, string text, string? team, int sequence, string clock, double seconds) =>
            new(new EspnCommentaryPlay($"v{sequence}", new EspnKeyEventType(type), new EspnClock(clock, seconds), team is null ? null : new EspnCommentaryTeam(team), null),
                text, sequence, new EspnClock(clock, seconds));
        var commentary = new List<EspnCommentary>
        {
            // As in Villarreal - Elche, 17 Oct 2026.
            Var("var---referee-decision-cancelled", "VAR Decision: No Penalty Real Sociedad.", "Real Sociedad", 30, "41'", 2460),
            // Without the play's team, the text's spelling of it is enough.
            Var("var---referee-decision-cancelled", "VAR Decision: No Penalty Valencia.", null, 60, "63'", 3739),
            // A penalty VAR gives: not taken back (its "Penalty conceded by" line announces it).
            Var("var---referee-decision-cancelled", "VAR Decision: Penalty Valencia.", "Valencia", 70, "70'", 4150),
            // The referee's "no penalty" upheld: there never was one.
            Var("var---referee-decision-confirmed", "VAR Decision: No Penalty Valencia.", "Valencia", 75, "75'", 4500),
            // Another VAR decision.
            Var("var---referee-decision-cancelled", "VAR Decision: No Goal Valencia.", "Valencia", 80, "80'", 4800),
        };

        var cancelled = EspnSummaryMapper.Map(summary with { Commentary = commentary }, "esp.1")!.Events
            .Where(e => e.Type == MatchEventType.PenaltyCancelled)
            .ToList();

        Assert.Equal(
        [
            new MatchEvent(MatchEventType.PenaltyCancelled, "41'", TeamSide.Away, null, null, Id: "v30"),
            new MatchEvent(MatchEventType.PenaltyCancelled, "63'", TeamSide.Home, null, null, Id: "v60"),
        ], cancelled);
    }

    [Fact]
    public void Substitutions_list_the_player_coming_on_first()
    {
        var sub = EventsOf(MapFinished()).First(e => e.Type == MatchEventType.Substitution);

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
    public void Maps_venue_referee_and_attendance()
    {
        var summary = FixtureLoader.LoadSummary("summary-tur1-scheduled.json") with
        {
            GameInfo = new EspnGameInfo(
                new EspnVenue("Diyarbakir Stadyumu", new EspnVenueAddress("Diyarbakir", "Türkiye")),
                Attendance: 28150,
                [new EspnOfficial("Fourth Official", new EspnOfficialPosition("Fourth Official")), new EspnOfficial("Ali Sansalan", new EspnOfficialPosition("Referee"))]),
        };

        Assert.Equal(
            new MatchInfo("Diyarbakir Stadyumu", "Diyarbakir", "Türkiye", "Ali Sansalan", 28150),
            EspnSummaryMapper.Map(summary, "tur.1")!.Info);
    }

    [Fact]
    public void Unknown_parts_of_the_match_info_are_null()
    {
        // As ESPN sends it for most leagues: a stadium, an attendance of 0 and no officials.
        var summary = FixtureLoader.LoadSummary("summary-tur1-scheduled.json") with
        {
            GameInfo = new EspnGameInfo(new EspnVenue("GSP Stadium", new EspnVenueAddress("Nicosia")), Attendance: 0, Officials: null),
        };

        Assert.Equal(new MatchInfo("GSP Stadium", "Nicosia", null, null, null), EspnSummaryMapper.Map(summary, "tur.1")!.Info);
    }

    [Fact]
    public void Summary_without_game_info_has_no_match_info()
    {
        var summary = FixtureLoader.LoadSummary("summary-tur1-scheduled.json") with { GameInfo = new EspnGameInfo(null, 0, []) };

        Assert.Null(EspnSummaryMapper.Map(summary, "tur.1")!.Info);
    }

    [Fact]
    public void Previous_meetings_are_the_finished_head_to_head_matches_newest_first()
    {
        static EspnSeriesEvent Meeting(string id, string date, bool completed, string home, string homeScore, string away, string awayScore) => new(
            id,
            date,
            new EspnSeriesStatus(completed),
            [
                new EspnCompetitor("home", homeScore, new EspnTeam(home, home, null, $"https://logos/{home}.png")),
                new EspnCompetitor("away", awayScore, new EspnTeam(away, away, null, null)),
            ],
            "2025-26 Turkish Super Lig");

        var scheduled = FixtureLoader.LoadSummary("summary-tur1-scheduled.json");
        var summary = scheduled with
        {
            Seasonseries =
            [
                new EspnSeries("something-else", [Meeting("1", "2020-01-01T17:00Z", true, "A", "9", "B", "9")]),
                new EspnSeries("head-to-head",
                [
                    Meeting("20", "2025-03-01T17:00Z", true, "B", "0", "A", "2"),
                    Meeting("30", "2026-02-01T17:00Z", true, "A", "3", "B", "1"),
                    // Not played yet, the match itself, and one without a score: all left out.
                    Meeting("40", "2026-12-01T17:00Z", false, "A", "0", "B", "0"),
                    Meeting(scheduled.Header!.Id!, "2026-10-01T17:00Z", true, "A", "1", "B", "1"),
                    Meeting("50", "2024-02-01T17:00Z", true, "A", "", "B", ""),
                ]),
            ],
        };

        var meetings = EspnSummaryMapper.Map(summary, "tur.1")!.PreviousMeetings!;

        Assert.Equal(["30", "20"], meetings.Select(m => m.Id));
        Assert.Equal(new DateTimeOffset(2026, 2, 1, 17, 0, 0, TimeSpan.Zero), meetings[0].KickoffUtc);
        Assert.Equal("2025-26 Turkish Super Lig", meetings[0].Competition);
        Assert.Equal(new Team("A", "A", "A", "https://logos/A.png"), meetings[0].HomeTeam);
        Assert.Equal(new Score(3, 1), meetings[0].Score);
        Assert.Equal(new Score(0, 2), meetings[1].Score);
    }

    [Fact]
    public void Previous_meetings_name_the_teams_as_the_match_does()
    {
        var scheduled = FixtureLoader.LoadSummary("summary-tur1-scheduled.json");
        var match = EspnSummaryMapper.Map(scheduled, "tur.1")!.Match;
        var summary = scheduled with
        {
            Seasonseries =
            [
                new EspnSeries("head-to-head",
                [
                    new EspnSeriesEvent("20", "2025-03-01T17:00Z", new EspnSeriesStatus(true),
                    [
                        new EspnCompetitor("home", "1", new EspnTeam(match.AwayTeam.Id, "A sparser name", null, null)),
                        new EspnCompetitor("away", "0", new EspnTeam(match.HomeTeam.Id, "Another one", null, null)),
                    ], null),
                ]),
            ],
        };

        var meeting = Assert.Single(EspnSummaryMapper.Map(summary, "tur.1")!.PreviousMeetings!);

        Assert.Equal(match.AwayTeam, meeting.HomeTeam);
        Assert.Equal(match.HomeTeam, meeting.AwayTeam);
        Assert.Null(meeting.Competition);
    }

    [Fact]
    public void Summary_without_a_series_has_no_previous_meetings()
    {
        Assert.Empty(MapFinished().PreviousMeetings!);
    }

    [Fact]
    public void Match_of_another_league_is_rejected()
    {
        // ESPN serves any match under any league's URL.
        Assert.Null(EspnSummaryMapper.Map(FixtureLoader.LoadSummary("summary-esp1-finished.json"), "tur.1"));
    }
}
