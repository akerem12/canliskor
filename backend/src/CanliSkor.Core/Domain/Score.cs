namespace CanliSkor.Core.Domain;

public sealed record Score(int Home, int Away)
{
    public override string ToString() => $"{Home}-{Away}";
}
