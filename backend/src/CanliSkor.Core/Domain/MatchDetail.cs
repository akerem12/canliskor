namespace CanliSkor.Core.Domain;

/// <summary>
/// One match with what happened in it: goals, cards and substitutions in order, team statistics, and who played.
/// </summary>
/// <param name="Stats">Empty before kickoff.</param>
/// <param name="Lineups">Null until the line-ups are announced (about an hour before kickoff).</param>
/// <param name="Info">Where the match is played and who referees it. Null if the provider says nothing at all.</param>
/// <param name="PreviousMeetings">Earlier matches between the two teams, newest first. Null means none are known.</param>
public sealed record MatchDetail(
    Match Match,
    IReadOnlyList<MatchEvent> Events,
    IReadOnlyList<MatchStat> Stats,
    MatchLineups? Lineups = null,
    MatchInfo? Info = null,
    IReadOnlyList<PreviousMeeting>? PreviousMeetings = null);

/// <summary>What surrounds a match. Every part is null when the provider doesn't say.</summary>
/// <param name="Attendance">The official crowd; null until it is reported.</param>
public sealed record MatchInfo(string? Venue, string? City, string? Country, string? Referee, int? Attendance);

/// <summary>A finished earlier match between the same two teams, in any competition.</summary>
/// <param name="Competition">As the provider names it, e.g. "2025-26 English Premier League". Null if unknown.</param>
public sealed record PreviousMeeting(
    string Id,
    DateTimeOffset KickoffUtc,
    string? Competition,
    Team HomeTeam,
    Team AwayTeam,
    Score Score);

/// <param name="Clock">Match minute as displayed, e.g. "57'" or "45'+2'".</param>
/// <param name="Side">The team the event counts for. An own goal counts for the team that benefits from it.</param>
/// <param name="Player">Scorer, booked player, or the player coming on.</param>
/// <param name="RelatedPlayer">Assist provider, or the player going off. Null if none or unknown.</param>
/// <param name="PlayerId">The provider's id of <paramref name="Player"/>, for opening their profile. Null if unknown.</param>
/// <param name="RelatedPlayerId">The provider's id of <paramref name="RelatedPlayer"/>. Null if unknown.</param>
public sealed record MatchEvent(
    MatchEventType Type,
    string Clock,
    TeamSide Side,
    string? Player,
    string? RelatedPlayer,
    string? PlayerId = null,
    string? RelatedPlayerId = null);

public enum MatchEventType
{
    Goal,
    PenaltyGoal,
    OwnGoal,
    YellowCard,
    RedCard,
    Substitution,
}

public enum TeamSide
{
    Home,
    Away,
}

/// <param name="Home">Possession is a percentage (e.g. 42.7); everything else is a count.</param>
public sealed record MatchStat(MatchStatType Type, double Home, double Away);

/// <summary>Declared in display order.</summary>
public enum MatchStatType
{
    Possession,
    Shots,
    ShotsOnTarget,
    Corners,
    Fouls,
    Offsides,
    YellowCards,
    RedCards,
    Saves,
}

/// <summary>A match detail plus when we fetched it.</summary>
public sealed record MatchDetailSnapshot(MatchDetail Detail, DateTimeOffset FetchedAtUtc);
