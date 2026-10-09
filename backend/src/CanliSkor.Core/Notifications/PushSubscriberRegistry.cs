using System.Collections.Concurrent;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// The browsers to notify, in memory. A restart empties it; every browser registers again the next time the
/// site is opened in it, so it fills up again by itself.
/// </summary>
public sealed class PushSubscriberRegistry
{
    /// <summary>A bound on memory, far above what the site sees; beyond it new browsers are turned away.</summary>
    public const int MaxSubscribers = 10_000;

    private readonly ConcurrentDictionary<string, PushSubscriber> _subscribers = new();

    /// <summary>Adds the subscriber, or replaces what was known about it.</summary>
    /// <returns>False if the registry is full and the subscriber is new.</returns>
    public bool Register(PushSubscriber subscriber)
    {
        if (_subscribers.Count >= MaxSubscribers && !_subscribers.ContainsKey(subscriber.Endpoint))
        {
            return false;
        }

        _subscribers[subscriber.Endpoint] = subscriber;
        return true;
    }

    public void Remove(string endpoint) => _subscribers.TryRemove(endpoint, out _);

    public IReadOnlyList<PushSubscriber> All() => [.. _subscribers.Values];
}
