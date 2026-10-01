using System.Globalization;
using Microsoft.Data.Sqlite;
using Spotter.Core.Logs;
using Spotter.Core.Quests;

namespace Spotter.Data.Progress;

/// <summary>
/// Quest observations and small key/value settings in one SQLite file. Observations are only ever added; the
/// current state is computed from them (see <see cref="QuestProgress"/>), so every status can say where it came from.
/// </summary>
public sealed class ProgressStore : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly Lock _gate = new();

    public ProgressStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
        _db.Open();
        Execute("PRAGMA journal_mode=WAL;");
        Execute("""
            CREATE TABLE IF NOT EXISTS observations (
                id INTEGER PRIMARY KEY,
                mode TEXT NOT NULL,
                quest_id TEXT NOT NULL,
                state TEXT NOT NULL,
                source TEXT NOT NULL,
                observed_at TEXT NOT NULL,
                evidence TEXT NOT NULL,
                UNIQUE (source, evidence, quest_id, state)
            );
            CREATE INDEX IF NOT EXISTS observations_mode ON observations (mode);
            CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            """);
    }

    /// <summary>Stores observations not seen before; returns how many were new.</summary>
    public int Add(IEnumerable<QuestObservation> observations)
    {
        lock (_gate)
        {
            using var tx = _db.BeginTransaction();
            using var cmd = _db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO observations (mode, quest_id, state, source, observed_at, evidence)
                VALUES ($mode, $quest, $state, $source, $at, $evidence)
                """;
            var mode = cmd.Parameters.Add("$mode", SqliteType.Text);
            var quest = cmd.Parameters.Add("$quest", SqliteType.Text);
            var state = cmd.Parameters.Add("$state", SqliteType.Text);
            var source = cmd.Parameters.Add("$source", SqliteType.Text);
            var at = cmd.Parameters.Add("$at", SqliteType.Text);
            var evidence = cmd.Parameters.Add("$evidence", SqliteType.Text);
            var added = 0;
            foreach (var o in observations)
            {
                mode.Value = o.Mode.ToString();
                quest.Value = o.QuestId;
                state.Value = o.State.ToString();
                source.Value = o.Source.ToString();
                at.Value = o.At.ToString("O", CultureInfo.InvariantCulture);
                evidence.Value = o.Evidence;
                added += cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return added;
        }
    }

    public IReadOnlyList<QuestObservation> Load(GameMode mode)
    {
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT quest_id, state, source, observed_at, evidence FROM observations WHERE mode = $mode";
            cmd.Parameters.AddWithValue("$mode", mode.ToString());
            using var reader = cmd.ExecuteReader();
            var list = new List<QuestObservation>();
            while (reader.Read())
            {
                if (!Enum.TryParse<QuestState>(reader.GetString(1), out var state) ||
                    !Enum.TryParse<ObservationSource>(reader.GetString(2), out var source))
                    continue;
                list.Add(new QuestObservation(mode, reader.GetString(0), state, source,
                    DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), reader.GetString(4)));
            }
            return list;
        }
    }

    public string? GetSetting(string key)
    {
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = $key";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }
    }

    public void SetSetting(string key, string value)
    {
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }
    }

    private void Execute(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}
