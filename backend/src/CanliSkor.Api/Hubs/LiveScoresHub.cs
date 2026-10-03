using System.Text.RegularExpressions;
using CanliSkor.Core.Options;
using CanliSkor.Core.Polling;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CanliSkor.Api.Hubs;

/// <summary>
/// Clients subscribe per league and receive <see cref="ILiveScoresClient.MatchUpdated"/> for its matches. A client
/// with a match page open also subscribes to that match and receives <see cref="ILiveScoresClient.MatchDetailUpdated"/>
/// while it is in play. The hub is receive-only for data: the initial state comes from the REST API.
/// </summary>
public sealed partial class LiveScoresHub(IOptionsMonitor<FootballOptions> footballOptions, MatchViewerRegistry viewers) : Hub<ILiveScoresClient>
{
    public const string Path = "/hubs/live-scores";

    public static string GroupName(string leagueCode) => $"league:{leagueCode}";

    public static string MatchGroupName(string leagueCode, string matchId) => $"match:{leagueCode}:{matchId}";

    // Same rule as the REST endpoint: ESPN match ids are numeric.
    [GeneratedRegex("^[0-9]{1,15}$")]
    private static partial Regex MatchIdPattern();

    public Task SubscribeToLeague(string leagueCode)
    {
        // Only followed leagues: otherwise clients could create unlimited groups that never receive anything.
        if (!footballOptions.CurrentValue.Leagues.Contains(leagueCode))
        {
            throw new HubException($"Unknown league '{leagueCode}'.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(leagueCode));
    }

    public Task UnsubscribeFromLeague(string leagueCode) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(leagueCode));

    /// <summary>The client has this match's page open: while it is in play, the poller keeps its detail current and pushes it.</summary>
    public Task SubscribeToMatch(string leagueCode, string matchId)
    {
        if (!footballOptions.CurrentValue.Leagues.Contains(leagueCode) || !MatchIdPattern().IsMatch(matchId))
        {
            throw new HubException($"Unknown match '{matchId}' in league '{leagueCode}'.");
        }

        if (!viewers.Watch(Context.ConnectionId, leagueCode, matchId))
        {
            throw new HubException("Too many matches open on this connection.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, MatchGroupName(leagueCode, matchId));
    }

    public Task UnsubscribeFromMatch(string leagueCode, string matchId)
    {
        viewers.Unwatch(Context.ConnectionId, leagueCode, matchId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, MatchGroupName(leagueCode, matchId));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        viewers.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
