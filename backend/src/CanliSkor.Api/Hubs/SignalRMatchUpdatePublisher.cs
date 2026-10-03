using CanliSkor.Api.Contracts;
using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Domain;
using Microsoft.AspNetCore.SignalR;

namespace CanliSkor.Api.Hubs;

/// <summary>Sends each change to the group of the match's league, and each detail to the group of its match.</summary>
internal sealed class SignalRMatchUpdatePublisher(IHubContext<LiveScoresHub, ILiveScoresClient> hub) : IMatchUpdatePublisher
{
    public async Task PublishAsync(IReadOnlyList<MatchChange> changes, CancellationToken cancellationToken = default)
    {
        foreach (var change in changes)
        {
            await hub.Clients.Group(LiveScoresHub.GroupName(change.Match.LeagueCode)).MatchUpdated(change.ToMessage());
        }
    }

    public Task PublishDetailAsync(MatchDetailSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var match = snapshot.Detail.Match;
        return hub.Clients.Group(LiveScoresHub.MatchGroupName(match.LeagueCode, match.Id)).MatchDetailUpdated(snapshot.ToResponse());
    }
}
