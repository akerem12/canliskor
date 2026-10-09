using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Notifications;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CanliSkor.Api.Endpoints;

/// <summary>
/// What a browser sends to be notified: its push subscription and what it wants to hear about. The browser sends
/// the whole thing again on every visit and after every change, so one call both subscribes and updates.
/// The Android app sends the same with <paramref name="Token"/> in place of the subscription.
/// </summary>
/// <param name="Token">The app's Firebase registration token; a browser leaves it out.</param>
/// <param name="Language">"tr" or "en"; anything else gets English.</param>
/// <param name="TeamIds">Favourite teams, if alerts for them are on.</param>
/// <param name="MatchIds">Matches picked with the bell.</param>
public sealed record PushRegistrationRequest(
    string? Endpoint,
    PushKeysRequest? Keys,
    string? Language,
    IReadOnlyList<string>? TeamIds,
    IReadOnlyList<string>? MatchIds,
    bool KickoffReminder,
    bool LineupAlerts,
    string? Token = null);

public sealed record PushKeysRequest(string? P256dh, string? Auth);

public sealed record PushKeyResponse(string PublicKey);

public static class PushEndpoints
{
    /// <summary>More favourites than anyone has; keeps one request from filling the memory and the database.</summary>
    private const int MaxIds = 200;

    private const int MaxTextLength = 1000;

    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/push").WithTags("Notifications");

        // The key a browser needs to subscribe. It changes if the server's key does, and the browser then subscribes anew.
        group.MapGet("/key", (IPushSender sender) => new PushKeyResponse(sender.PublicKey));

        group.MapPut("/subscription", async Task<Results<NoContent, ValidationProblem, StatusCodeHttpResult>> (
            PushRegistrationRequest request, PushSubscriberRegistry registry, CancellationToken cancellationToken) =>
        {
            // The Android app sends its Firebase token, a browser its push subscription.
            var fromApp = !string.IsNullOrEmpty(request.Token);
            if (fromApp && !PushSubscriber.IsFcmToken(request.Token))
            {
                return Invalid("token", "Not a Firebase registration token.");
            }

            if (!fromApp && (!PushSubscriber.IsPushServiceEndpoint(request.Endpoint) || request.Endpoint!.Length > MaxTextLength))
            {
                return Invalid("endpoint", "Not a browser push service address.");
            }

            if (!fromApp && (!IsKey(request.Keys?.P256dh) || !IsKey(request.Keys?.Auth)))
            {
                return Invalid("keys", "The subscription's p256dh and auth keys are required.");
            }

            var teamIds = Ids(request.TeamIds);
            var matchIds = Ids(request.MatchIds);
            if (teamIds is null || matchIds is null)
            {
                return Invalid("ids", $"At most {MaxIds} teams and {MaxIds} matches.");
            }

            var subscriber = new PushSubscriber(
                fromApp ? request.Token! : request.Endpoint!,
                fromApp ? "" : request.Keys!.P256dh!,
                fromApp ? "" : request.Keys!.Auth!,
                request.Language == "tr" ? "tr" : "en",
                teamIds,
                matchIds,
                request.KickoffReminder,
                request.LineupAlerts,
                fromApp ? PushChannel.Fcm : PushChannel.Web);

            return await registry.RegisterAsync(subscriber, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        });

        // The endpoint (for the app: its token) is the secret that identifies the subscriber, so knowing it is
        // enough to remove it.
        group.MapDelete("/subscription", async (string endpoint, PushSubscriberRegistry registry, CancellationToken cancellationToken) =>
        {
            await registry.RemoveAsync(endpoint, cancellationToken);
            return TypedResults.NoContent();
        });

        return app;
    }

    private static bool IsKey(string? value) => value is { Length: > 0 and <= MaxTextLength };

    /// <returns>Null if there are too many or one isn't an id.</returns>
    private static HashSet<string>? Ids(IReadOnlyList<string>? ids)
    {
        ids ??= [];
        return ids.Count <= MaxIds && ids.All(id => id is { Length: > 0 and <= 40 }) ? [.. ids] : null;
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
