using System.Net;
using System.Text.Json;
using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Notifications;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;

namespace CanliSkor.Infrastructure.Push;

/// <summary>
/// Sends notifications with the Web Push protocol: an encrypted message to the browser vendor's push service,
/// which wakes the site's service worker (frontend/public/sw.js) on the device, and that shows the notification.
/// </summary>
internal sealed partial class WebPushSender : IPushSender
{
    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    private readonly PushServiceClient _client;
    private readonly VapidAuthentication _authentication;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(HttpClient httpClient, VapidKeys keys, VapidAuthentication authentication, ILogger<WebPushSender> logger)
    {
        // No waiting out a push service's "retry after": the notifier runs again in half a minute anyway.
        _client = new PushServiceClient(httpClient) { AutoRetryAfter = false };
        _authentication = authentication;
        _logger = logger;
        PublicKey = keys.PublicKey;
    }

    public string PublicKey { get; }

    public async Task<PushOutcome> SendAsync(PushSubscriber subscriber, PushNotification notification, CancellationToken cancellationToken = default)
    {
        var subscription = new PushSubscription { Endpoint = subscriber.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, subscriber.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, subscriber.Auth);

        // What sw.js reads.
        var payload = JsonSerializer.Serialize(new { notification.Title, notification.Body, notification.Url, notification.Tag }, PayloadJson);
        var message = new PushMessage(payload)
        {
            TimeToLive = Math.Max(60, (int)notification.TimeToLive.TotalSeconds),
            Urgency = PushMessageUrgency.High,
        };

        try
        {
            await _client.RequestPushMessageDeliveryAsync(subscription, message, _authentication, cancellationToken);
            return PushOutcome.Sent;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone or HttpStatusCode.Forbidden)
        {
            // 404/410: the browser no longer has the subscription. 403: it was made for another key of ours.
            return PushOutcome.Gone;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Bad keys from the client, a push service that is down or slow, a timeout.
            LogSendFailed(ex, new Uri(subscriber.Endpoint).Host);
            return PushOutcome.Failed;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending a notification through {PushHost} failed")]
    private partial void LogSendFailed(Exception exception, string pushHost);
}
