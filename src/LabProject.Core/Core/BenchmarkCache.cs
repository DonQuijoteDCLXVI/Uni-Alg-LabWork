using System.Diagnostics;
using LabProject.Db;

namespace LabProject.Core;

/// <summary>
/// BenchmarkCache - кэш замеров поверх БД:
///  1. Если в БД уже есть `numRuns` сырых запусков для связки (algorithm, n[, m]) -
///     берёт их и просто усредняет (не считая заново, это и есть "кэш").
///  2. Если данных нет (или их меньше numRuns, или forceRecalculate) - запускает
///     функцию numRuns раз, измеряет время, сохраняет каждый запуск и возвращает
///     усреднённый результат.
///
/// Контракт измеряемой функции: Func&lt;int, long?&gt; - принимает n и возвращает
/// количество шагов (Часть IV) либо null, если для алгоритма меряется только
/// время. Время всегда замеряется попутно (Stopwatch) и сохраняется.
/// </summary>
public sealed class BenchmarkCache
{
    private readonly IResultStore _store;

    public BenchmarkCache(IResultStore store) => _store = store;

    public IResultStore Store => _store;

    /// <summary>
    /// Главный метод: усреднённый результат замеров для (algorithm, n[, m]),
    /// с использованием кэша БД, если это возможно.
    /// </summary>
    public BenchmarkResult GetOrCompute(
        string algorithm,
        int n,
        Func<int, long?> func,
        int? m = null,
        int numRuns = 5,
        bool forceRecalculate = false)
    {
        if (!forceRecalculate)
        {
            var cached = LoadFromDb(algorithm, n, m, numRuns);
            if (cached is not null)
                return cached;
        }

        // Кэша нет / он неполный / запрошен принудительный пересчёт: старые сырые
        // записи этой точки удаляем, чтобы не копить дубликаты run_number (в
        // исходной Python-версии так делалось только при force_recalculate, из-за
        // чего неполный кэш "достраивался" дублями).
        _store.DeleteRuns(algorithm, n, m);

        return ComputeAndStore(algorithm, n, func, m, numRuns);
    }

    /// <summary>
    /// Усреднённые результаты по ВСЕМ n (и m), которые уже есть в БД для
    /// алгоритма, независимо от числа сохранённых запусков. Используется
    /// режимом "Сравнить алгоритмы".
    /// </summary>
    public IReadOnlyList<BenchmarkResult> LoadHistory(string algorithm)
    {
        var rows = _store.GetAllRuns(algorithm);

        var grouped = new Dictionary<(int N, int? M), (List<double> Times, List<double> Steps)>();
        foreach (var row in rows)
        {
            var key = (row.N, row.M);
            if (!grouped.TryGetValue(key, out var bucket))
            {
                bucket = (new List<double>(), new List<double>());
                grouped[key] = bucket;
            }
            if (row.TimeSeconds is double t)
                bucket.Times.Add(t);
            if (row.Steps is long s)
                bucket.Steps.Add(s);
        }

        var results = new List<BenchmarkResult>();
        foreach (var ((n, m), (times, steps)) in grouped
                     .OrderBy(kv => kv.Key.N).ThenBy(kv => kv.Key.M ?? 0))
        {
            results.Add(new BenchmarkResult(
                Algorithm: algorithm,
                N: n,
                M: m,
                NumRuns: times.Count > 0 ? times.Count : steps.Count,
                MeanTime: times.Count > 0 ? Stats.Mean(times) : null,
                StdTime: times.Count > 1 ? Stats.PopulationStdDev(times) : (times.Count > 0 ? 0.0 : null),
                MeanSteps: steps.Count > 0 ? Stats.Mean(steps) : null,
                StdSteps: steps.Count > 1 ? Stats.PopulationStdDev(steps) : (steps.Count > 0 ? 0.0 : null),
                FromCache: true));
        }
        return results;
    }

    /// <summary>Алгоритмы, для которых в БД уже есть хоть какие-то данные.</summary>
    public static IReadOnlyList<string> ListAlgorithmsWithData(IResultStore store)
        => store.ListAlgorithms();

    // ------------------------------------------------------------------ //
    // Внутренняя механика
    // ------------------------------------------------------------------ //

    /// <summary>Собирает результат из уже существующих записей; null, если запусков меньше numRuns.</summary>
    private BenchmarkResult? LoadFromDb(string algorithm, int n, int? m, int numRuns)
    {
        var rows = _store.GetRuns(algorithm, n, m, numRuns);
        if (rows.Count < numRuns)
            return null; // кэш неполный - надо считать

        var times = rows.Where(r => r.TimeSeconds.HasValue).Select(r => r.TimeSeconds!.Value).ToList();
        var steps = rows.Where(r => r.Steps.HasValue).Select(r => (double)r.Steps!.Value).ToList();

        return new BenchmarkResult(
            Algorithm: algorithm,
            N: n,
            M: m,
            NumRuns: numRuns,
            MeanTime: times.Count > 0 ? Stats.Mean(times) : null,
            StdTime: times.Count > 1 ? Stats.PopulationStdDev(times) : (times.Count > 0 ? 0.0 : null),
            MeanSteps: steps.Count > 0 ? Stats.Mean(steps) : null,
            StdSteps: steps.Count > 1 ? Stats.PopulationStdDev(steps) : (steps.Count > 0 ? 0.0 : null),
            FromCache: true);
    }

    private BenchmarkResult ComputeAndStore(
        string algorithm, int n, Func<int, long?> func, int? m, int numRuns)
    {
        var times = new List<double>();
        var stepsList = new List<double>();
        var rowsToInsert = new List<RawRun>();

        // Замеряем все запуски, НЕ трогая БД между ними: запись на диск стоит
        // десятки миллисекунд и иначе искажала бы/замедляла эксперимент.
        for (int run = 1; run <= numRuns; run++)
        {
            var (elapsed, steps) = RunSingle(func, n);
            times.Add(elapsed);
            if (steps.HasValue)
                stepsList.Add(steps.Value);
            rowsToInsert.Add(new RawRun(algorithm, n, m, run, elapsed, steps));
        }

        // Одна транзакция (один commit) на все запуски этой точки
        _store.InsertRuns(rowsToInsert);

        return new BenchmarkResult(
            Algorithm: algorithm,
            N: n,
            M: m,
            NumRuns: numRuns,
            MeanTime: times.Count > 0 ? Stats.Mean(times) : null,
            StdTime: times.Count > 1 ? Stats.PopulationStdDev(times) : 0.0,
            MeanSteps: stepsList.Count > 0 ? Stats.Mean(stepsList) : null,
            StdSteps: stepsList.Count > 1 ? Stats.PopulationStdDev(stepsList) : (stepsList.Count > 0 ? 0.0 : null),
            FromCache: false);
    }

    /// <summary>Выполняет func(n) один раз, измеряя время высокоточным таймером.</summary>
    private static (double Elapsed, long? Steps) RunSingle(Func<int, long?> func, int n)
    {
        long start = Stopwatch.GetTimestamp();
        long? steps = func(n);
        double elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
        return (elapsed, steps);
    }
}
