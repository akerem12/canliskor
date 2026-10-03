namespace CanliSkor.Core.Domain;

/// <summary>One match with what happened in it: goals, cards and substitutions in order, plus team statistics.</summary>
/// <param name="Stats">Empty before kickoff.</param>
public sealed record MatchDetail(Match Match, IReadOnlyList<MatchEvent> Events, IReadOnlyList<MatchStat> Stats);

/// <param name="Clock">Match minute as displayed, e.g. "57'" or "45'+2'".</param>
/// <param name="Side">The team the event counts for. An own goal counts for the team that benefits from it.</param>
/// <param name="Player">Scorer, booked player, or the player coming on.</param>
/// <param name="RelatedPlayer">Assist provider, or the player going off. Null if none or unknown.</param>
public sealed record MatchEvent(MatchEventType Type, string Clock, TeamSide Side, string? Player, string? RelatedPlayer);

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
