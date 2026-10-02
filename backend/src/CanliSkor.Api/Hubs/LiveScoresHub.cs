using CanliSkor.Core.Options;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CanliSkor.Api.Hubs;

/// <summary>
/// Clients subscribe per league and receive <see cref="ILiveScoresClient.MatchUpdated"/> for its matches.
/// The hub is receive-only for data: the initial state comes from the REST API, the hub sends only changes.
/// </summary>
public sealed class LiveScoresHub(IOptionsMonitor<FootballOptions> footballOptions) : Hub<ILiveScoresClient>
{
    public const string Path = "/hubs/live-scores";

    public static string GroupName(string leagueCode) => $"league:{leagueCode}";

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
}
