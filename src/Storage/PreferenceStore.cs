using System.Data;
using System.Data.Common;
using HidePlayers.Contract;

namespace HidePlayers.Storage;

/// <summary>Stores personal choices in MariaDB, processing operations in invocation order.</summary>
public sealed class PreferenceStore(Func<IDbConnection> connectionFactory)
{
    private readonly object _queueLock = new();
    private Task _tail = Task.CompletedTask;
    private Task? _initialization;

    public Task InitializeAsync()
    {
        lock (_queueLock)
        {
            return InitializeLocked();
        }
    }

    public Task<(HidePreferences Preferences, HideMode LastActiveMode)?> LoadAsync(ulong steamId)
    {
        lock (_queueLock)
        {
            var initialization = InitializeLocked();
            return EnqueueLocked<(HidePreferences Preferences, HideMode LastActiveMode)?>(async () =>
            {
                await initialization.ConfigureAwait(false);
                await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT mode, hide_dead, last_active_mode
                    FROM hideplayers_preferences WHERE steam_id = @steam_id
                    """;
                AddParameter(command, "@steam_id", DbType.UInt64, steamId);
                await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                if (!await reader.ReadAsync().ConfigureAwait(false))
                {
                    return null;
                }

                return (
                    new HidePreferences(Enum.Parse<HideMode>(reader.GetString(0)), reader.GetBoolean(1)),
                    Enum.Parse<HideMode>(reader.GetString(2)));
            });
        }
    }

    public Task SaveAsync(ulong steamId, HidePreferences preferences, HideMode lastActiveMode)
    {
        lock (_queueLock)
        {
            var initialization = InitializeLocked();
            return EnqueueLocked(async () =>
            {
                await initialization.ConfigureAwait(false);
                await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO hideplayers_preferences (steam_id, mode, hide_dead, last_active_mode)
                    VALUES (@steam_id, @mode, @hide_dead, @last_active_mode)
                    ON DUPLICATE KEY UPDATE mode = @mode, hide_dead = @hide_dead, last_active_mode = @last_active_mode
                    """;
                AddParameter(command, "@steam_id", DbType.UInt64, steamId);
                AddParameter(command, "@mode", DbType.String, preferences.Mode.ToString());
                AddParameter(command, "@hide_dead", DbType.Boolean, preferences.HideDead);
                AddParameter(command, "@last_active_mode", DbType.String, lastActiveMode.ToString());
                return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            });
        }
    }

    /// <summary>Awaits operations queued before this call; callers must stop accepting new work first.</summary>
    public Task FlushAsync()
    {
        lock (_queueLock)
        {
            return _tail;
        }
    }

    private Task InitializeLocked() => _initialization ??= EnqueueLocked(async () =>
    {
        await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS hideplayers_preferences (
                steam_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
                mode VARCHAR(16) NOT NULL,
                hide_dead BOOLEAN NOT NULL,
                last_active_mode VARCHAR(16) NOT NULL DEFAULT 'All'
            )
            """;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        command.CommandText = """
            ALTER TABLE hideplayers_preferences
            ADD COLUMN IF NOT EXISTS last_active_mode VARCHAR(16) NOT NULL DEFAULT 'All'
            """;
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    });

    private Task<T> EnqueueLocked<T>(Func<Task<T>> operation)
    {
        var previous = _tail;
        var next = Task.Run(async () =>
        {
            try
            {
                await previous.ConfigureAwait(false);
            }
            catch
            {
                // The caller receives the previous failure; independent queued operations still run.
            }

            return await operation().ConfigureAwait(false);
        });
        _tail = next;
        return next;
    }

    private async Task<DbConnection> OpenConnectionAsync()
    {
        var connection = (DbConnection)connectionFactory();
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync().ConfigureAwait(false);
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
