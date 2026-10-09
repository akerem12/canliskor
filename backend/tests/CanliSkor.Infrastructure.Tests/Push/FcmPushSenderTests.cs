using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanliSkor.Core.Notifications;
using CanliSkor.Infrastructure.Push;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanliSkor.Infrastructure.Tests.Push;

public sealed class FcmPushSenderTests : IDisposable
{
    private const string DeviceToken = "dYv3example-token_0123456789:APA91bExampleExampleExample";

    private static readonly PushNotification Reminder =
        new("Match Reminder: Home vs Away", "Kickoff in 30 minutes!", "/?league=tur.1&match=7", "7:reminder", TimeSpan.FromMinutes(30));

    private readonly RSA _key = RSA.Create(2048);
    private readonly FakeGoogle _google = new();

    public void Dispose() => _key.Dispose();

    [Fact]
    public async Task Sends_the_notification_to_the_phone_with_an_access_token_from_google()
    {
        var outcome = await Sender().SendAsync(Phone(), Reminder, CancellationToken.None);

        Assert.Equal(PushOutcome.Sent, outcome);
        var send = Assert.Single(_google.Sends);
        Assert.Equal("https://fcm.googleapis.com/v1/projects/canliskor-test/messages:send", send.Url);
        Assert.Equal("Bearer access-1", send.Authorization);

        var message = send.Body.GetProperty("message");
        Assert.Equal(DeviceToken, message.GetProperty("token").GetString());
        Assert.Equal("Match Reminder: Home vs Away", message.GetProperty("notification").GetProperty("title").GetString());
        Assert.Equal("/?league=tur.1&match=7", message.GetProperty("data").GetProperty("url").GetString());
        var android = message.GetProperty("android");
        Assert.Equal("1800s", android.GetProperty("ttl").GetString());
        Assert.Equal("7:reminder", android.GetProperty("notification").GetProperty("tag").GetString());
        Assert.Equal("matches", android.GetProperty("notification").GetProperty("channel_id").GetString());
    }

    [Fact]
    public async Task The_note_to_google_is_signed_with_the_accounts_key()
    {
        await Sender().SendAsync(Phone(), Reminder, CancellationToken.None);

        var parts = Assert.Single(_google.Assertions).Split('.');
        Assert.True(_key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        Assert.Equal("push@canliskor-test.iam.gserviceaccount.com", claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal("https://oauth2.googleapis.com/token", claims.RootElement.GetProperty("aud").GetString());
    }

    [Fact]
    public async Task The_access_token_is_asked_for_once_and_reused()
    {
        var sender = Sender();

        await sender.SendAsync(Phone(), Reminder, CancellationToken.None);
        await sender.SendAsync(Phone(), Reminder, CancellationToken.None);

        Assert.Single(_google.Assertions);
        Assert.Equal(2, _google.Sends.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"status":"NOT_FOUND","details":[{"errorCode":"UNREGISTERED"}]}}""", PushOutcome.Gone)]
    [InlineData(HttpStatusCode.Forbidden, """{"error":{"status":"PERMISSION_DENIED","details":[{"errorCode":"SENDER_ID_MISMATCH"}]}}""", PushOutcome.Gone)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"The registration token is not a valid FCM registration token"}}""", PushOutcome.Gone)]
    // Our own doing: none of these may cost a subscriber.
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"Invalid JSON payload received."}}""", PushOutcome.Failed)]
    [InlineData(HttpStatusCode.Forbidden, """{"error":{"status":"PERMISSION_DENIED","message":"Firebase Cloud Messaging API has not been used"}}""", PushOutcome.Failed)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", PushOutcome.Failed)]
    public async Task What_firebase_answers_decides_whether_the_phone_is_kept(HttpStatusCode status, string body, PushOutcome expected)
    {
        _google.SendAnswer = (status, body);

        Assert.Equal(expected, await Sender().SendAsync(Phone(), Reminder, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("""{"project_id":"p","client_email":"e","private_key":"not a key"}""")]
    public async Task Without_a_usable_service_account_nothing_is_sent_and_nobody_is_dropped(string? serviceAccount)
    {
        var outcome = await Sender(serviceAccount, configured: false).SendAsync(Phone(), Reminder, CancellationToken.None);

        Assert.Equal(PushOutcome.Failed, outcome);
        Assert.Empty(_google.Assertions);
        Assert.Empty(_google.Sends);
    }

    private FcmPushSender Sender(string? serviceAccount = null, bool configured = true)
    {
        serviceAccount ??= configured
            ? JsonSerializer.Serialize(new
            {
                project_id = "canliskor-test",
                client_email = "push@canliskor-test.iam.gserviceaccount.com",
                private_key = _key.ExportPkcs8PrivateKeyPem(),
            })
            : null;

        var tokens = new FirebaseAccessTokens(
            new SingleClientFactory(_google),
            Options.Create(new FirebaseOptions { ServiceAccount = serviceAccount }),
            TimeProvider.System,
            NullLogger<FirebaseAccessTokens>.Instance);
        return new FcmPushSender(new HttpClient(_google, disposeHandler: false), tokens, NullLogger<FcmPushSender>.Instance);
    }

    private static PushSubscriber Phone() =>
        new(DeviceToken, "", "", "en", new HashSet<string>(), new HashSet<string>(), true, true, PushChannel.Fcm);

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>Stands in for both of Google's servers: the one handing out access tokens and Firebase itself.</summary>
    private sealed class FakeGoogle : HttpMessageHandler
    {
        public List<string> Assertions { get; } = [];

        public List<(string Url, string Authorization, JsonElement Body)> Sends { get; } = [];

        public (HttpStatusCode Status, string Body) SendAnswer { get; set; } = (HttpStatusCode.OK, """{"name":"projects/canliskor-test/messages/1"}""");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri!.Host == "oauth2.googleapis.com")
            {
                var form = body.Split('&').Select(pair => pair.Split('=')).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
                Assertions.Add(form["assertion"]);
                return Json(HttpStatusCode.OK, $$"""{"access_token":"access-{{Assertions.Count}}","expires_in":3600}""");
            }

            Sends.Add((request.RequestUri.ToString(), request.Headers.Authorization!.ToString(), JsonDocument.Parse(body).RootElement));
            return Json(SendAnswer.Status, SendAnswer.Body);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
