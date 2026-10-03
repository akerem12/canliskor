using CanliSkor.Api.Contracts;

namespace CanliSkor.Api.Hubs;

/// <summary>Messages the server sends to clients. Strongly typed, so method names can't drift from the contract.</summary>
public interface ILiveScoresClient
{
    Task MatchUpdated(MatchUpdatedMessage message);

    /// <summary>The full, current detail of a match the client has open. Same shape as the REST detail.</summary>
    Task MatchDetailUpdated(MatchDetailResponse detail);
}
