using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanliSkor.Core.Notifications;
using Microsoft.Extensions.Logging;

namespace CanliSkor.Infrastructure.Push;

/// <summary>
/// Sends notifications to the Android app through Firebase Cloud Messaging (the HTTP v1 API). Android shows the
/// notification by itself while the app is closed or in the background; a tap opens the app, which reads the
/// page to open from the message's data (frontend/src/native/notifications.ts).
/// </summary>
internal sealed partial class FcmPushSender(HttpClient httpClient, FirebaseAccessTokens tokens, ILogger<FcmPushSender> logger)
{
    /// <summary>The notification channel the app creates on the phone; its settings there decide sound and pop-up.</summary>
    public const string AndroidChannelId = "matches";

    public async Task<PushOutcome> SendAsync(PushSubscriber subscriber, PushNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await tokens.GetAsync(cancellationToken);
            if (accessToken is null)
            {
                // Not configured (said once at startup). It stays open and is sent once the key is there.
                return PushOutcome.Failed;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{tokens.ProjectId}/messages:send");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new
            {
                message = new
                {
                    token = subscriber.Endpoint,
                    notification = new { title = notification.Title, body = notification.Body },
                    // Read by the app: where a tap leads, and the same again for when the app is open and has to
                    // show the notification itself.
                    data = new { url = notification.Url, tag = notification.Tag, title = notification.Title, body = notification.Body },
                    android = new
                    {
                        priority = "HIGH",
                        ttl = $"{Math.Max(60, (int)notification.TimeToLive.TotalSeconds)}s",
                        // The same tag replaces the earlier notification instead of adding a second one.
                        notification = new { tag = notification.Tag, channel_id = AndroidChannelId },
                    },
                },
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return PushOutcome.Sent;
            }

            var problem = await response.Content.ReadAsStringAsync(cancellationToken);
            if (IsGone(response.StatusCode, problem))
            {
                return PushOutcome.Gone;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                tokens.Forget();
            }

            LogRefused((int)response.StatusCode, problem.Length > 300 ? problem[..300] : problem);
            return PushOutcome.Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogSendFailed(ex);
            return PushOutcome.Failed;
        }
    }

    /// <summary>
    /// True if Firebase says this phone will never be reached with this token: the app was uninstalled or got a
    /// new token, the token belongs to another project, or it isn't a token at all. Anything else (our key lacking
    /// a permission, a malformed message, Firebase being down) is our problem and must not cost a subscriber.
    /// </summary>
    internal static bool IsGone(HttpStatusCode status, string problem) =>
        problem.Contains("UNREGISTERED", StringComparison.Ordinal)
        || problem.Contains("SENDER_ID_MISMATCH", StringComparison.Ordinal)
        || (status == HttpStatusCode.BadRequest && problem.Contains("registration token", StringComparison.OrdinalIgnoreCase));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Firebase refused a notification with {Status}: {Problem}")]
    private partial void LogRefused(int status, string problem);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending a notification through Firebase failed")]
    private partial void LogSendFailed(Exception exception);
}
