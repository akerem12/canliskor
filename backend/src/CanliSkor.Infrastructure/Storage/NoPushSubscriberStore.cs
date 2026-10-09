using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Notifications;

namespace CanliSkor.Infrastructure.Storage;

/// <summary>
/// Used when no database is configured (a plain <c>dotnet run</c>, the tests): subscribers live in memory only
/// and a restart forgets them, until each browser opens the site again.
/// </summary>
internal sealed class NoPushSubscriberStore : IPushSubscriberStore
{
    public Task<IReadOnlyList<PushSubscriber>?> LoadAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PushSubscriber>?>([]);

    public Task<bool> SaveAsync(PushSubscriber subscriber, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task RemoveAsync(string endpoint, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
