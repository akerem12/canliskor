using CanliSkor.Core.Notifications;

namespace CanliSkor.Core.Abstractions;

/// <summary>
/// Where subscribers are kept so they outlive a restart. Core only knows "load, save, remove"; the database lives
/// in Infrastructure. No method throws for a storage problem: notifications carry on from memory without it.
/// </summary>
public interface IPushSubscriberStore
{
    /// <returns>Null if the storage couldn't be read; worth another try later.</returns>
    Task<IReadOnlyList<PushSubscriber>?> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds the subscriber, or replaces what was stored about it.</summary>
    /// <returns>False if it couldn't be stored.</returns>
    Task<bool> SaveAsync(PushSubscriber subscriber, CancellationToken cancellationToken = default);

    Task RemoveAsync(string endpoint, CancellationToken cancellationToken = default);
}
