using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;
using Microsoft.Data.Sqlite;

namespace Cooldown.Data;

/// <summary>Stores play sessions in a local SQLite file. Timestamps are Unix milliseconds (UTC).</summary>
public sealed class SqliteSessionStore : ISessionStore, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Lock _gate = new();

    public SqliteSessionStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _connection = new SqliteConnection($"Data Source={path}");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS sessions (
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                app_id   INTEGER NOT NULL,
                start_ms INTEGER NOT NULL,
                end_ms   INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_sessions_end ON sessions (end_ms);
            """;
        cmd.ExecuteNonQuery();
    }

    public PlaySession Begin(int appId, DateTimeOffset at)
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO sessions (app_id, start_ms, end_ms) VALUES ($app, $t, $t);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$app", appId);
            cmd.Parameters.AddWithValue("$t", at.ToUnixTimeMilliseconds());
            var id = (long)cmd.ExecuteScalar()!;
            return new PlaySession(id, appId, at, at);
        }
    }

    public void Extend(long sessionId, DateTimeOffset to)
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE sessions SET end_ms = $t WHERE id = $id;";
            cmd.Parameters.AddWithValue("$t", to.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$id", sessionId);
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<PlaySession> GetSessionsSince(DateTimeOffset since)
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT id, app_id, start_ms, end_ms FROM sessions WHERE end_ms >= $since;";
            cmd.Parameters.AddWithValue("$since", since.ToUnixTimeMilliseconds());

            var sessions = new List<PlaySession>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                sessions.Add(new PlaySession(
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    FromMs(reader.GetInt64(2), since.Offset),
                    FromMs(reader.GetInt64(3), since.Offset)));
            }
            return sessions;
        }
    }

    public void Dispose() => _connection.Dispose();

    private static DateTimeOffset FromMs(long ms, TimeSpan offset) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).ToOffset(offset);
}
