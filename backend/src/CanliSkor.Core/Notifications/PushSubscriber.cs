using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// A browser that asked for match notifications: where to send them (the browser's push subscription) and what
/// about. The browser sends all of it again whenever the site is opened; in between it is kept in the store.
/// </summary>
/// <param name="Endpoint">The push service's address for this browser; also what identifies the subscriber.</param>
/// <param name="P256dh">The browser's public key, base64url.</param>
/// <param name="Auth">The browser's authentication secret, base64url.</param>
/// <param name="Language">"tr" or "en": the language of the notifications' text.</param>
/// <param name="TeamIds">Favourite teams: every match of theirs counts.</param>
/// <param name="MatchIds">Single matches picked with the bell, whoever plays.</param>
public sealed record PushSubscriber(
    string Endpoint,
    string P256dh,
    string Auth,
    string Language,
    IReadOnlySet<string> TeamIds,
    IReadOnlySet<string> MatchIds,
    bool KickoffReminder,
    bool LineupAlerts)
{
    // Chrome and its relatives, Firefox, Edge, Safari. Nothing else is ever sent a request by us.
    private static readonly string[] PushHosts =
    [
        "fcm.googleapis.com", "android.googleapis.com", ".push.services.mozilla.com", ".notify.windows.com", ".push.apple.com",
    ];

    /// <summary>The same in every field. The record's own equality compares the two sets by reference.</summary>
    public bool SameAs(PushSubscriber other) =>
        Endpoint == other.Endpoint
        && P256dh == other.P256dh
        && Auth == other.Auth
        && Language == other.Language
        && KickoffReminder == other.KickoffReminder
        && LineupAlerts == other.LineupAlerts
        && TeamIds.SetEquals(other.TeamIds)
        && MatchIds.SetEquals(other.MatchIds);

    public bool Follows(Match match) =>
        MatchIds.Contains(match.Id) || TeamIds.Contains(match.HomeTeam.Id) || TeamIds.Contains(match.AwayTeam.Id);

    /// <summary>
    /// True for an https address of a browser vendor's push service. The endpoint comes from the client and we
    /// send requests to it, so anything else is refused.
    /// </summary>
    public static bool IsPushServiceEndpoint(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && PushHosts.Any(host => host.StartsWith('.')
            ? uri.Host.EndsWith(host, StringComparison.OrdinalIgnoreCase)
            : uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
}

/// <summary>What a notification shows, and the page a tap on it opens.</summary>
/// <param name="Url">Relative to the site, e.g. "/?league=tur.1&amp;match=401888379".</param>
/// <param name="Tag">Notifications with the same tag replace each other on the device instead of piling up.</param>
/// <param name="TimeToLive">How long the push service keeps trying a device that is offline.</param>
public sealed record PushNotification(string Title, string Body, string Url, string Tag, TimeSpan TimeToLive);

public enum PushOutcome
{
    Sent,

    /// <summary>The browser dropped the subscription (site data cleared, permission taken back): forget it.</summary>
    Gone,

    /// <summary>Didn't get through this time; worth another try.</summary>
    Failed,
}
