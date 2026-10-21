using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using CanliSkor.Core.Notifications;
using CanliSkor.Infrastructure.Push;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanliSkor.Infrastructure.Tests.Push;

public sealed class WebPushSenderTests
{
    private static readonly PushNotification Goal =
        new("Fenerbahce [3] - 0 Alanyaspor", "⚽ 87' Vedat Muriqi", "/?league=tur.1&match=7", "7:event:g1", TimeSpan.FromMinutes(15));

    [Fact]
    public async Task Sent_when_the_push_service_takes_it()
    {
        var outcome = await Sender(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created))).SendAsync(Phone(), Goal);

        Assert.Equal(PushOutcome.Sent, outcome);
    }

    [Fact]
    public async Task Failed_when_the_push_service_answers_with_an_error_so_it_is_tried_again()
    {
        var outcome = await Sender(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))).SendAsync(Phone(), Goal);

        Assert.Equal(PushOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Gone_when_the_phone_no_longer_has_the_subscription()
    {
        var outcome = await Sender(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Gone))).SendAsync(Phone(), Goal);

        Assert.Equal(PushOutcome.Gone, outcome);
    }

    [Fact]
    public async Task Unconfirmed_when_no_answer_comes_in_time_so_it_is_not_sent_twice()
    {
        // What HttpClient throws when its timeout runs out.
        var outcome = await Sender(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"))
            .SendAsync(Phone(), Goal);

        Assert.Equal(PushOutcome.Unconfirmed, outcome);
    }

    [Theory]
    // The connection broke after the message went out.
    [InlineData(HttpRequestError.ResponseEnded, true)]
    [InlineData(HttpRequestError.Unknown, true)]
    // The push service was never reached: safe to try again.
    [InlineData(HttpRequestError.NameResolutionError, false)]
    [InlineData(HttpRequestError.ConnectionError, false)]
    [InlineData(HttpRequestError.SecureConnectionError, false)]
    public void A_broken_connection_may_have_delivered_it_one_that_never_opened_has_not(HttpRequestError error, bool mayHaveArrived)
    {
        var exception = new HttpRequestException(error, "push", new SocketException());

        Assert.Equal(mayHaveArrived, WebPushSender.MayHaveArrived(exception));
    }

    [Fact]
    public void Bad_keys_from_the_browser_never_reached_anyone()
    {
        Assert.False(WebPushSender.MayHaveArrived(new ArgumentException("bad p256dh")));
    }

    private static WebPushSender Sender(Func<HttpRequestMessage, Task<HttpResponseMessage>> pushService)
    {
        var keys = VapidKeys.FromSecret("test-secret");
        var authentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey) { Subject = "https://canliskor.onrender.com" };
        return new WebPushSender(new HttpClient(new FakePushService(pushService)), keys, authentication, NullLogger<WebPushSender>.Instance);
    }

    /// <summary>A browser's subscription, with real keys so the message can be encrypted for it.</summary>
    private static PushSubscriber Phone()
    {
        using var browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = browser.ExportParameters(includePrivateParameters: false).Q;
        byte[] point = [0x04, .. q.X!, .. q.Y!];

        return new PushSubscriber(
            "https://web.push.apple.com/QExample", Base64Url.EncodeToString(point), Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
            "tr", new HashSet<string> { "436" }, new HashSet<string>(), KickoffReminder: true, LineupAlerts: true);
    }

    private sealed class FakePushService(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => answer(request);
    }
}
