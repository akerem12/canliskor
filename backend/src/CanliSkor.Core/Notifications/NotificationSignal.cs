namespace CanliSkor.Core.Notifications;

/// <summary>
/// Lets the poller wake the notifier the moment a scoreboard changes (a goal, kickoff, half time), instead of
/// the change waiting for the notifier's next regular round. Raising it while nobody waits is kept for the next
/// wait, so a change during a round is never missed.
/// </summary>
public sealed class NotificationSignal
{
    private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when <see cref="Raise"/> is next called. Take it before a round, wait on it after.</summary>
    public Task Next => Volatile.Read(ref _next).Task;

    public void Raise() =>
        Interlocked.Exchange(ref _next, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
}
