using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using Microsoft.Extensions.Logging;

namespace CanliSkor.Core.Services;

/// <summary>
/// Possible line-ups for a match that hasn't announced its own yet. The data source has no predictions, so each
/// team is shown as it last started: the eleven and the formation of its most recent played match that has a
/// usable line-up. That is a guess (injuries, suspensions and rotation aren't known), and it says which match it is from.
/// Everything is read through the cached services, so repeat views cost nothing.
/// </summary>
public sealed partial class ExpectedLineupService(
    MatchDetailService details,
    LeagueInfoService leagues,
    ILogger<ExpectedLineupService> logger)
{
    /// <summary>
    /// How many of a team's latest matches are looked at for a line-up. Friendlies often have none, or one without
    /// a formation, so a national team may need to go back a few.
    /// </summary>
    public const int MatchesToTry = 5;

    /// <summary>More players than this side by side isn't a formation anyone plays: the data lacks positions.</summary>
    private const int WidestRow = 6;

    private static readonly PlayerMatchStats NoStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <returns>
    /// Null if the league isn't followed or the match is unknown. A side is null if nothing could be found for it,
    /// and both are once the match has its real line-ups or is no longer to be played.
    /// </returns>
    /// <exception cref="FootballDataProviderException">The match itself could not be loaded.</exception>
    public async Task<ExpectedLineups?> GetAsync(string leagueCode, string matchId, CancellationToken cancellationToken = default)
    {
        var snapshot = await details.GetAsync(leagueCode, matchId, cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        var match = snapshot.Detail.Match;
        if (snapshot.Detail.Lineups is not null || match.Status != MatchStatus.Scheduled)
        {
            return new ExpectedLineups(null, null);
        }

        return new ExpectedLineups(
            await LastStartedAsync(leagueCode, match.HomeTeam.Id, matchId, cancellationToken),
            await LastStartedAsync(leagueCode, match.AwayTeam.Id, matchId, cancellationToken));
    }

    private async Task<ExpectedLineup?> LastStartedAsync(string leagueCode, string teamId, string upcomingMatchId, CancellationToken cancellationToken)
    {
        try
        {
            var team = await leagues.GetTeamAsync(leagueCode, teamId, cancellationToken);
            var latest = (team?.Value.RecentMatches ?? [])
                .Where(m => m.Status == MatchStatus.Finished && m.Id != upcomingMatchId)
                .OrderByDescending(m => m.KickoffUtc)
                .Take(MatchesToTry);

            foreach (var played in latest)
            {
                // Null for a competition that isn't followed (a cup, a club friendly): the next match is tried.
                var detail = (await details.GetAsync(played.LeagueCode, played.Id, cancellationToken))?.Detail;
                var lineup = detail?.Lineups is not { } lineups ? null
                    : detail.Match.HomeTeam.Id == teamId ? lineups.Home
                    : detail.Match.AwayTeam.Id == teamId ? lineups.Away
                    : null;

                if (lineup is not null && IsLaidOut(lineup))
                {
                    return new ExpectedLineup(StartersOnly(lineup), detail!.Match);
                }
            }
        }
        catch (FootballDataProviderException ex)
        {
            // One team's history failing shouldn't take the other team's away.
            LogLoadFailed(ex, teamId);
        }

        return null;
    }

    /// <summary>
    /// True for an eleven that can be drawn on a pitch: a goalkeeper, then at least two rows of outfield players.
    /// Where the provider has no formation for a match, all eleven come in a single row ("11"), or as "7-3".
    /// </summary>
    public static bool IsLaidOut(TeamLineup lineup) =>
        lineup.Rows.Count >= 3
        && lineup.Rows[0].Count == 1
        && lineup.Rows.Sum(row => row.Count) == 11
        && lineup.Rows.All(row => row.Count is >= 1 and <= WidestRow);

    /// <summary>The eleven as they lined up, without what happened in that match: no bench, minutes, goals or cards.</summary>
    private static TeamLineup StartersOnly(TeamLineup lineup) => lineup with
    {
        Rows = lineup.Rows
            .Select(row => (IReadOnlyList<LineupPlayer>)row
                .Select(p => new LineupPlayer(p.Id, p.Name, p.ShortName, p.Jersey, p.Position, CameOnAt: null, WentOffAt: null, NoStats))
                .ToList())
            .ToList(),
        Bench = [],
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading the last line-up of team {TeamId} failed; going on without it")]
    private partial void LogLoadFailed(Exception exception, string teamId);
}
