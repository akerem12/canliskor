using CanliSkor.Core.Notifications;

namespace CanliSkor.Core.Tests.Notifications;

public class PushSubscriberRegistryTests
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/abc";

    private readonly FakePushSubscriberStore _store = new();
    private readonly PushSubscriberRegistry _registry;

    public PushSubscriberRegistryTests() => _registry = new PushSubscriberRegistry(_store);

    [Fact]
    public async Task A_new_subscriber_is_stored()
    {
        await _registry.RegisterAsync(Subscriber(teamIds: ["432"]));

        Assert.Single(_registry.All());
        Assert.Equal(["432"], _store.Stored[Endpoint].TeamIds);
    }

    [Fact]
    public async Task A_browser_repeating_itself_costs_no_write_but_a_change_does()
    {
        await _registry.RegisterAsync(Subscriber(teamIds: ["432", "436"]));
        await _registry.RegisterAsync(Subscriber(teamIds: ["436", "432"]));
        Assert.Equal(1, _store.Saves);

        await _registry.RegisterAsync(Subscriber(teamIds: ["432"]));

        Assert.Equal(2, _store.Saves);
        Assert.Equal(["432"], _store.Stored[Endpoint].TeamIds);
    }

    [Fact]
    public async Task With_the_store_down_the_subscriber_is_still_notified_and_stored_on_its_next_repeat()
    {
        _store.Down = true;
        Assert.True(await _registry.RegisterAsync(Subscriber()));
        Assert.Single(_registry.All());
        Assert.Empty(_store.Stored);

        _store.Down = false;
        await _registry.RegisterAsync(Subscriber());

        Assert.Single(_store.Stored);
    }

    [Fact]
    public async Task Removing_forgets_the_subscriber_in_the_store_too()
    {
        await _registry.RegisterAsync(Subscriber());

        await _registry.RemoveAsync(Endpoint);

        Assert.Empty(_registry.All());
        Assert.Empty(_store.Stored);
    }

    [Fact]
    public async Task Loading_brings_back_the_stored_ones_and_keeps_whoever_registered_since()
    {
        const string other = "https://fcm.googleapis.com/fcm/send/other";
        _store.Stored[Endpoint] = Subscriber(language: "en");
        _store.Stored[other] = Subscriber(endpoint: other);
        var registry = new PushSubscriberRegistry(_store);
        await registry.RegisterAsync(Subscriber(language: "tr"));

        var loaded = await registry.LoadAsync();

        Assert.Equal(2, loaded);
        Assert.Equal(2, registry.All().Count);
        Assert.Equal("tr", registry.All().Single(s => s.Endpoint == Endpoint).Language);
    }

    [Fact]
    public async Task Loading_from_a_store_that_is_down_says_so()
    {
        _store.Down = true;

        Assert.Null(await _registry.LoadAsync());
    }

    private static PushSubscriber Subscriber(string endpoint = Endpoint, string[]? teamIds = null, string language = "en") =>
        new(endpoint, "p256dh", "auth", language, new HashSet<string>(teamIds ?? []), new HashSet<string>(), true, true);
}
