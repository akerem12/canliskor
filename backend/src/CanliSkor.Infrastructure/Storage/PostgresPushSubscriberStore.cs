using CanliSkor.Core.Abstractions;
using CanliSkor.Core.Notifications;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanliSkor.Infrastructure.Storage;

/// <summary>
/// Subscribers in a Postgres table, one row each. It is read once at startup and written only when a subscriber
/// really changes, so the database is asleep most of the time (the free plan counts the hours it is awake).
/// </summary>
internal sealed partial class PostgresPushSubscriberStore(NpgsqlDataSource dataSource, ILogger<PostgresPushSubscriberStore> logger)
    : IPushSubscriberStore
{
    private const string CreateTable = """
        create table if not exists push_subscribers (
            endpoint         text primary key,
            p256dh           text not null,
            auth             text not null,
            language         text not null,
            team_ids         text[] not null,
            match_ids        text[] not null,
            kickoff_reminder boolean not null,
            lineup_alerts    boolean not null,
            updated_at       timestamptz not null default now(),
            channel          text not null default 'Web'
        )
        """;

    // For a table made before the Android app existed.
    private const string AddChannel = "alter table push_subscribers add column if not exists channel text not null default 'Web'";

    private readonly SemaphoreSlim _tableLock = new(1, 1);
    private bool _tableExists;

    public async Task<IReadOnlyList<PushSubscriber>?> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureTableAsync(cancellationToken);
            await using var command = dataSource.CreateCommand(
                "select endpoint, p256dh, auth, language, team_ids, match_ids, kickoff_reminder, lineup_alerts, channel from push_subscribers");
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var subscribers = new List<PushSubscriber>();
            while (await reader.ReadAsync(cancellationToken))
            {
                // A channel this version doesn't know was written by a newer one: not ours to send to.
                if (!Enum.TryParse<PushChannel>(reader.GetString(8), out var channel))
                {
                    continue;
                }

                subscribers.Add(new PushSubscriber(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    new HashSet<string>(reader.GetFieldValue<string[]>(4)),
                    new HashSet<string>(reader.GetFieldValue<string[]>(5)),
                    reader.GetBoolean(6),
                    reader.GetBoolean(7),
                    channel));
            }

            return subscribers;
        }
        catch (Exception ex) when (IsStorageProblem(ex, cancellationToken))
        {
            LogFailed(ex, "Loading the subscribers");
            return null;
        }
    }

    public async Task<bool> SaveAsync(PushSubscriber subscriber, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureTableAsync(cancellationToken);
            await using var command = dataSource.CreateCommand("""
                insert into push_subscribers (endpoint, p256dh, auth, language, team_ids, match_ids, kickoff_reminder, lineup_alerts, channel)
                values ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                on conflict (endpoint) do update set
                    p256dh = excluded.p256dh, auth = excluded.auth, language = excluded.language,
                    team_ids = excluded.team_ids, match_ids = excluded.match_ids,
                    kickoff_reminder = excluded.kickoff_reminder, lineup_alerts = excluded.lineup_alerts,
                    channel = excluded.channel, updated_at = now()
                """);
            command.Parameters.Add(new() { Value = subscriber.Endpoint });
            command.Parameters.Add(new() { Value = subscriber.P256dh });
            command.Parameters.Add(new() { Value = subscriber.Auth });
            command.Parameters.Add(new() { Value = subscriber.Language });
            command.Parameters.Add(new() { Value = subscriber.TeamIds.ToArray() });
            command.Parameters.Add(new() { Value = subscriber.MatchIds.ToArray() });
            command.Parameters.Add(new() { Value = subscriber.KickoffReminder });
            command.Parameters.Add(new() { Value = subscriber.LineupAlerts });
            command.Parameters.Add(new() { Value = subscriber.Channel.ToString() });
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsStorageProblem(ex, cancellationToken))
        {
            LogFailed(ex, "Saving a subscriber");
            return false;
        }
    }

    public async Task RemoveAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureTableAsync(cancellationToken);
            await using var command = dataSource.CreateCommand("delete from push_subscribers where endpoint = $1");
            command.Parameters.Add(new() { Value = endpoint });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex) when (IsStorageProblem(ex, cancellationToken))
        {
            // The row comes back at the next start; the push service then says it is gone and it is removed again.
            LogFailed(ex, "Removing a subscriber");
        }
    }

    /// <summary>The table is made by the first call that gets through, so a new database needs no setup step.</summary>
    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (_tableExists)
        {
            return;
        }

        await _tableLock.WaitAsync(cancellationToken);
        try
        {
            if (!_tableExists)
            {
                foreach (var statement in new[] { CreateTable, AddChannel })
                {
                    await using var command = dataSource.CreateCommand(statement);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                _tableExists = true;
            }
        }
        finally
        {
            _tableLock.Release();
        }
    }

    // Everything but our own shutdown: the database asleep or unreachable, a timeout, a dropped connection.
    private static bool IsStorageProblem(Exception exception, CancellationToken cancellationToken) =>
        exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Action} in the database failed")]
    private partial void LogFailed(Exception exception, string action);
}
