using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Abstractions;

/// <summary>
/// Pushes match changes to connected clients. Core only knows "publish"; the transport (SignalR) lives in the Api.
/// </summary>
public interface IMatchUpdatePublisher
{
    Task PublishAsync(IReadOnlyList<MatchChange> changes, CancellationToken cancellationToken = default);
}
