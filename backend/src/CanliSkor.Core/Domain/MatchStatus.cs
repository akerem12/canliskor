namespace CanliSkor.Core.Domain;

/// <summary>
/// Our own match lifecycle, independent of any provider's status codes.
/// </summary>
public enum MatchStatus
{
    Scheduled,
    Live,
    HalfTime,
    Finished,
    Postponed,
    Cancelled,
}

public static class MatchStatusExtensions
{
    public static bool IsInPlay(this MatchStatus status) =>
        status is MatchStatus.Live or MatchStatus.HalfTime;
}
