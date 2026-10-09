using CanliSkor.Core.Notifications;

namespace CanliSkor.Core.Abstractions;

/// <summary>
/// Delivers a notification to one browser, also while the site is closed. Core only knows "send";
/// the transport (Web Push) lives in Infrastructure.
/// </summary>
public interface IPushSender
{
    /// <summary>The key browsers subscribe with (base64url); a subscription only works with the key it was made for.</summary>
    string PublicKey { get; }

    /// <summary>Never throws for a delivery problem: the outcome says what happened.</summary>
    Task<PushOutcome> SendAsync(PushSubscriber subscriber, PushNotification notification, CancellationToken cancellationToken = default);
}
