using System.Collections.Concurrent;
using CanliSkor.Core.Abstractions;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// The browsers to notify. The notifier reads them from memory; every change is also written to the store, and
/// after a restart <see cref="LoadAsync"/> brings them back, so nobody has to open the site again to stay subscribed.
/// </summary>
public sealed class PushSubscriberRegistry(IPushSubscriberStore store)
{
    /// <summary>A bound on memory, far above what the site sees; beyond it new browsers are turned away.</summary>
    public const int MaxSubscribers = 10_000;

    private readonly ConcurrentDictionary<string, Entry> _subscribers = new();

    /// <summary>Adds the subscriber, or replaces what was known about it.</summary>
    /// <returns>False if the registry is full and the subscriber is new.</returns>
    public async Task<bool> RegisterAsync(PushSubscriber subscriber, CancellationToken cancellationToken = default)
    {
        var known = _subscribers.GetValueOrDefault(subscriber.Endpoint);
        if (known is null && _subscribers.Count >= MaxSubscribers)
        {
            return false;
        }

        // Browsers repeat themselves every few minutes; only a real change is worth a write.
        if (known is { Stored: true } && known.Subscriber.SameAs(subscriber))
        {
            return true;
        }

        // In memory first: the subscriber is notified even if the store is down, and the browser's next
        // repeat tries the store again.
        var pending = new Entry(subscriber, Stored: false);
        _subscribers[subscriber.Endpoint] = pending;
        if (await store.SaveAsync(subscriber, cancellationToken))
        {
            _subscribers.TryUpdate(subscriber.Endpoint, pending with { Stored = true }, pending);
        }

        return true;
    }

    public async Task RemoveAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        _subscribers.TryRemove(endpoint, out _);
        await store.RemoveAsync(endpoint, cancellationToken);
    }

    public IReadOnlyList<PushSubscriber> All() => [.. _subscribers.Values.Select(entry => entry.Subscriber)];

    /// <summary>Brings back the stored subscribers. Whoever registered in the meantime is newer and is kept.</summary>
    /// <returns>How many were stored; null if the store couldn't be read.</returns>
    public async Task<int?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await store.LoadAllAsync(cancellationToken);
        if (stored is null)
        {
            return null;
        }

        foreach (var subscriber in stored)
        {
            _subscribers.TryAdd(subscriber.Endpoint, new Entry(subscriber, Stored: true));
        }

        return stored.Count;
    }

    /// <param name="Stored">The store has exactly this; false after a failed write.</param>
    private sealed record Entry(PushSubscriber Subscriber, bool Stored);
}
