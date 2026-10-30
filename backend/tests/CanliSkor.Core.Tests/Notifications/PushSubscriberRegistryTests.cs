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
    public async Task The_same_device_at_a_new_address_replaces_its_old_self()
    {
        const string renewed = "https://fcm.googleapis.com/fcm/send/renewed";
        await _registry.RegisterAsync(Subscriber(deviceId: "phone-0001"));
        await _registry.RegisterAsync(Subscriber(endpoint: "https://fcm.googleapis.com/fcm/send/someone-else", deviceId: "phone-0002"));

        await _registry.RegisterAsync(Subscriber(endpoint: renewed, deviceId: "phone-0001"));

        Assert.Equal([renewed, "https://fcm.googleapis.com/fcm/send/someone-else"], _registry.All().Select(s => s.Endpoint).Order());
        Assert.False(_store.Stored.ContainsKey(Endpoint));
        Assert.Equal("phone-0001", _store.Stored[renewed].DeviceId);
    }

    [Fact]
    public async Task Subscribers_without_a_device_id_are_told_apart_by_address()
    {
        await _registry.RegisterAsync(Subscriber());
        await _registry.RegisterAsync(Subscriber(endpoint: "https://fcm.googleapis.com/fcm/send/other"));

        Assert.Equal(2, _registry.All().Count);
    }

    [Fact]
    public async Task Loading_from_a_store_that_is_down_says_so()
    {
        _store.Down = true;

        Assert.Null(await _registry.LoadAsync());
    }

    private static PushSubscriber Subscriber(string endpoint = Endpoint, string[]? teamIds = null, string language = "en", string? deviceId = null) =>
        new(endpoint, "p256dh", "auth", language, new HashSet<string>(teamIds ?? []), new HashSet<string>(), true, true, DeviceId: deviceId);
}
