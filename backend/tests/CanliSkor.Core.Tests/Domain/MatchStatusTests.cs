using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Tests.Domain;

public class MatchStatusTests
{
    [Theory]
    [InlineData(MatchStatus.Scheduled, false)]
    [InlineData(MatchStatus.Live, true)]
    [InlineData(MatchStatus.HalfTime, true)]
    [InlineData(MatchStatus.Finished, false)]
    [InlineData(MatchStatus.Postponed, false)]
    [InlineData(MatchStatus.Cancelled, false)]
    public void IsInPlay(MatchStatus status, bool expected) => Assert.Equal(expected, status.IsInPlay());
}
