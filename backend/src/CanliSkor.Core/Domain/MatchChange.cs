namespace CanliSkor.Core.Domain;

[Flags]
public enum MatchChangeKind
{
    None = 0,
    Score = 1,
    Status = 2,
    Clock = 4,
}

/// <summary>A match whose state differs from the previous poll, and what changed.</summary>
public sealed record MatchChange(Match Match, MatchChangeKind Kinds);
