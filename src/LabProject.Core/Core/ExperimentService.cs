using System.Diagnostics;
using LabProject.Db;

namespace LabProject.Core;

/// <summary>
/// Результат эксперимента. Для Части II (NeedsM) заполнен Grid (сетка n x m),
/// для остальных частей - плоский Series.
/// </summary>
public sealed record ExperimentOutcome(
    AlgorithmSpec Spec,
    IReadOnlyList<BenchmarkResult>? Series,
    IReadOnlyList<IReadOnlyList<BenchmarkResult>>? Grid,
    double ElapsedSeconds);

/// <summary>
/// Фоновое выполнение экспериментов (аналог QThread-воркеров исходного GUI).
///
/// Каждый вызов получает ФАБРИКУ хранилища и открывает своё собственное
/// соединение с БД уже в рабочем потоке (соединения SQLite нельзя делить
/// между потоками). Эксперименты идут в потоке с большим стеком (см. BigStack).
/// </summary>
public static class ExperimentService
{
    public static Task<ExperimentOutcome> RunAsync(
        Func<IResultStore> storeFactory,
        AlgorithmSpec spec,
        IReadOnlyList<int> nValues,
        int numRuns,
        bool forceRecalculate,
        IReadOnlyList<int>? mValues = null,
        IProgress<(int Done, int Total)>? progress = null)
    {
        return BigStack.RunAsync(() =>
        {
            long wallStart = Stopwatch.GetTimestamp();
            using var store = storeFactory();
            var cache = new BenchmarkCache(store);
            Action<int, int>? report = progress is null ? null : (d, t) => progress.Report((d, t));

            ExperimentOutcome outcome;
            if (spec.NeedsM)
            {
                if (mValues is null || mValues.Count == 0)
                    throw new ArgumentException("Для этого алгоритма нужно указать значения m");

                var grid = BenchmarkRunner.RunExperiment2DGrid(
                    cache, spec, nValues, mValues, numRuns, forceRecalculate, report);
                outcome = new ExperimentOutcome(
                    spec, null, grid.Select(r => (IReadOnlyList<BenchmarkResult>)r).ToList(), 0);
            }
            else
            {
                var series = BenchmarkRunner.RunExperiment1D(
                    cache, spec, nValues, numRuns, forceRecalculate, null, report);
                outcome = new ExperimentOutcome(spec, series, null, 0);
            }

            return outcome with { ElapsedSeconds = Stopwatch.GetElapsedTime(wallStart).TotalSeconds };
        });
    }

    /// <summary>История замеров для нескольких алгоритмов (режим "Сравнить алгоритмы").</summary>
    public static Task<Dictionary<string, IReadOnlyList<BenchmarkResult>>> LoadHistoryAsync(
        Func<IResultStore> storeFactory, IEnumerable<string> algorithmKeys)
    {
        var keys = algorithmKeys.ToList();
        return Task.Run(() =>
        {
            using var store = storeFactory();
            var cache = new BenchmarkCache(store);
            return keys.ToDictionary(k => k, k => cache.LoadHistory(k));
        });
    }

    /// <summary>Ключи алгоритмов, которые можно сравнивать с текущим: есть данные в БД,
    /// та же метрика (время со временем, шаги с шагами), без второго параметра m.</summary>
    public static Task<List<string>> ListComparableAsync(Func<IResultStore> storeFactory, AlgorithmSpec current)
    {
        return Task.Run(() =>
        {
            using var store = storeFactory();
            return store.ListAlgorithms()
                .Where(k => AlgorithmRegistry.TryGet(k, out var s) && !s.NeedsM && s.Metric == current.Metric)
                .ToList();
        });
    }
}
