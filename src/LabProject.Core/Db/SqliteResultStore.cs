using Microsoft.Data.Sqlite;

namespace LabProject.Db;

/// <summary>
/// Подключение к SQLite и схема БД (полностью совместима со схемой исходного
/// Python-проекта: файл data/results.db можно скопировать из старой версии).
///
/// Таблица `results` хранит КАЖДЫЙ отдельный запуск (сырые данные), а не
/// усреднённые значения - усреднение и дисперсию считаем поверх сырых данных.
///
/// Поля: id, algorithm, n, m (только для Части II, иначе NULL), run_number,
/// time_seconds (может быть NULL), steps (может быть NULL), run_date.
///
/// ВАЖНО про потоки: соединение SqliteConnection нельзя использовать из
/// нескольких потоков одновременно, поэтому каждый воркер открывает СВОЁ
/// соединение (new SqliteResultStore(path)) внутри фонового потока.
/// </summary>
public sealed class SqliteResultStore : IResultStore
{
    /// <summary>По умолчанию - data/results.db рядом с исполняемым файлом.</summary>
    public static string DefaultDbPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "data", "results.db");

    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS results (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            algorithm    TEXT NOT NULL,
            n            INTEGER NOT NULL,
            m            INTEGER,
            run_number   INTEGER NOT NULL,
            time_seconds REAL,
            steps        INTEGER,
            run_date     TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    // Индекс ускоряет самый частый запрос кэша: все запуски для (algorithm, n, m).
    private const string CreateIndexSql = """
        CREATE INDEX IF NOT EXISTS idx_results_algo_n_m
        ON results (algorithm, n, m);
        """;

    private readonly SqliteConnection _conn;

    public string DbPath { get; }

    public SqliteResultStore(string? dbPath = null)
    {
        DbPath = dbPath ?? DefaultDbPath;
        string? dir = Path.GetDirectoryName(Path.GetFullPath(DbPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={DbPath}");
        _conn.Open();

        Exec("PRAGMA foreign_keys = ON;");
        // Меньше принудительных сбросов на диск - заметно быстрее запись результатов
        Exec("PRAGMA synchronous = NORMAL;");

        CreateTables();
    }

    private void Exec(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void CreateTables()
    {
        Exec(CreateTableSql);
        Exec(CreateIndexSql);
        MigrateAddMColumnIfMissing();
    }

    /// <summary>Лёгкая миграция: добавляет колонку m, если таблица создана старой версией.</summary>
    private void MigrateAddMColumnIfMissing()
    {
        var columns = GetColumns();
        if (!columns.Contains("m"))
            Exec("ALTER TABLE results ADD COLUMN m INTEGER;");
    }

    /// <summary>Имена колонок таблицы results (PRAGMA table_info).</summary>
    public HashSet<string> GetColumns()
    {
        var columns = new HashSet<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(results);";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(reader.GetOrdinal("name")));
        return columns;
    }

    private static object DbValue<T>(T? value) where T : struct
        => value.HasValue ? (object)value.Value : DBNull.Value;

    private static RawRun ReadRun(SqliteDataReader r) => new(
        Algorithm: r.GetString(0),
        N: r.GetInt32(1),
        M: r.IsDBNull(2) ? null : r.GetInt32(2),
        RunNumber: r.GetInt32(3),
        TimeSeconds: r.IsDBNull(4) ? null : r.GetDouble(4),
        Steps: r.IsDBNull(5) ? null : r.GetInt64(5));

    public IReadOnlyList<RawRun> GetRuns(string algorithm, int n, int? m, int limit)
    {
        using var cmd = _conn.CreateCommand();
        // "m IS $m" корректно сравнивает и с NULL
        cmd.CommandText = """
            SELECT algorithm, n, m, run_number, time_seconds, steps
            FROM results
            WHERE algorithm = $a AND n = $n AND m IS $m
            ORDER BY run_number ASC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$a", algorithm);
        cmd.Parameters.AddWithValue("$n", n);
        cmd.Parameters.AddWithValue("$m", DbValue(m));
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<RawRun>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadRun(reader));
        return list;
    }

    public IReadOnlyList<RawRun> GetAllRuns(string algorithm)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT algorithm, n, m, run_number, time_seconds, steps
            FROM results
            WHERE algorithm = $a
            ORDER BY n ASC
            """;
        cmd.Parameters.AddWithValue("$a", algorithm);

        var list = new List<RawRun>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadRun(reader));
        return list;
    }

    public void InsertRuns(IReadOnlyList<RawRun> runs)
    {
        if (runs.Count == 0)
            return;

        using var tx = _conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO results (algorithm, n, m, run_number, time_seconds, steps)
            VALUES ($a, $n, $m, $run, $time, $steps)
            """;
        var pA = cmd.Parameters.Add("$a", SqliteType.Text);
        var pN = cmd.Parameters.Add("$n", SqliteType.Integer);
        var pM = cmd.Parameters.Add("$m", SqliteType.Integer);
        var pRun = cmd.Parameters.Add("$run", SqliteType.Integer);
        var pTime = cmd.Parameters.Add("$time", SqliteType.Real);
        var pSteps = cmd.Parameters.Add("$steps", SqliteType.Integer);

        foreach (var r in runs)
        {
            pA.Value = r.Algorithm;
            pN.Value = r.N;
            pM.Value = DbValue(r.M);
            pRun.Value = r.RunNumber;
            pTime.Value = DbValue(r.TimeSeconds);
            pSteps.Value = DbValue(r.Steps);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void DeleteRuns(string algorithm, int n, int? m)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM results WHERE algorithm = $a AND n = $n AND m IS $m";
        cmd.Parameters.AddWithValue("$a", algorithm);
        cmd.Parameters.AddWithValue("$n", n);
        cmd.Parameters.AddWithValue("$m", DbValue(m));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<string> ListAlgorithms()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT algorithm FROM results ORDER BY algorithm ASC;";
        var list = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return list;
    }

    public void ClearAll() => Exec("DELETE FROM results;");

    public void Dispose()
    {
        _conn.Dispose();
        // Освобождаем файл БД (иначе пул соединений может удерживать его, мешая удалению)
        SqliteConnection.ClearAllPools();
    }
}
