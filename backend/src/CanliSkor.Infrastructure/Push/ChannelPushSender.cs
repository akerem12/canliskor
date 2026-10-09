using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Notifications;

namespace CanliSkor.Infrastructure.Push;

/// <summary>Sends each notification the way its subscriber is reached: a browser by Web Push, the Android app by Firebase.</summary>
internal sealed class ChannelPushSender(WebPushSender web, FcmPushSender fcm) : IPushSender
{
    public string PublicKey => web.PublicKey;

    public Task<PushOutcome> SendAsync(PushSubscriber subscriber, PushNotification notification, CancellationToken cancellationToken = default) =>
        subscriber.Channel == PushChannel.Fcm
            ? fcm.SendAsync(subscriber, notification, cancellationToken)
            : web.SendAsync(subscriber, notification, cancellationToken);
}
