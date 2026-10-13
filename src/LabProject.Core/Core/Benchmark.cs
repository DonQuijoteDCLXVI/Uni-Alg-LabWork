using LabProject.Algorithms;

namespace LabProject.Core;

public enum Metric { Time, Steps }

/// <summary>
/// Описание алгоритма в реестре.
/// <para>Run(n, m) выполняет ОДИН замеряемый прогон (вместе с генерацией входных
/// данных, как и в исходной версии) и возвращает число шагов (Часть IV) или
/// null, если для алгоритма меряется только время. Параметр m используется
/// только если NeedsM (умножение матриц), иначе игнорируется.</para>
/// </summary>
public sealed record AlgorithmSpec(
    string Key,                 // уникальный идентификатор, хранится в БД как algorithm
    string DisplayName,         // человекочитаемое имя для GUI
    string Part,                // "I" | "II" | "III" | "IV"
    Metric Metric,              // время / шаги
    Func<int, int, long?> Run,
    bool NeedsM = false,        // true только для умножения матриц (Часть II)
    string? DefaultModel = null // ключ из Approximation.ModelFunctions для авто-подбора
)
{
    public string MetricLabel => Metric == Metric.Time ? "время, с" : "количество шагов";

    /// <summary>Название без префикса части ("I. Сумма вектора" -> "Сумма вектора").</summary>
    public string ShortName => DisplayName.StartsWith(Part + ". ", StringComparison.Ordinal)
        ? DisplayName[(Part.Length + 2)..]
        : DisplayName;
}

/// <summary>Приёмник результатов, чтобы JIT/оптимизатор не выбросил "ненужные" вычисления.</summary>
internal static class Sink
{
    public static object? Value;
}

/// <summary>
/// ALGORITHM_REGISTRY - единый источник правды о том, какие алгоритмы есть в
/// системе, к какой части задания относятся, какая у них метрика и какая
/// теоретическая модель f(n) ожидается по умолчанию. GUI строит выпадающий
/// список именно по этому реестру: добавление нового алгоритма - правка в
/// одном месте.
/// </summary>
public static class AlgorithmRegistry
{
    // Фиксированное основание для степенных функций (Часть IV) - значение base
    // не влияет на число шагов, важен только показатель степени n.
    private const double PowerBase = 2.0;

    // x, в которой вычисляется полином (Часть I) - намеренно близко к 1, чтобы
    // при naive-подсчёте x^i не было переполнения на больших степенях.
    private const double PolynomialX = 1.0001;

    private static long? Consume(object? result)
    {
        Sink.Value = result;
        return null;
    }

    private static long? ConsumeSteps((double Result, long Steps) r)
    {
        Sink.Value = r.Result;
        return r.Steps;
    }

    public static IReadOnlyList<AlgorithmSpec> All { get; } = new List<AlgorithmSpec>
    {
        // ---------------------------- Часть I: векторы ---------------------------- //
        new("vector_sum", "I. Сумма вектора", "I", Metric.Time,
            (n, _) => Consume(Vectors.VectorSum(Vectors.GenerateRandomVector(n))),
            DefaultModel: "n"),

        new("vector_product", "I. Произведение вектора", "I", Metric.Time,
            // значения близко к 1, чтобы произведение не переполнялось на больших n
            (n, _) => Consume(Vectors.VectorProduct(Vectors.GenerateRandomVector(n, 0.99, 1.01))),
            DefaultModel: "n"),

        new("polynomial_naive", "I. Полином (наивный)", "I", Metric.Time,
            (n, _) => Consume(Vectors.PolynomialNaive(Vectors.GenerateRandomVector(n, -1, 1), PolynomialX)),
            DefaultModel: "n^2"),

        new("polynomial_horner", "I. Полином (Горнер)", "I", Metric.Time,
            (n, _) => Consume(Vectors.PolynomialHorner(Vectors.GenerateRandomVector(n, -1, 1), PolynomialX)),
            DefaultModel: "n"),

        new("bubble_sort", "I. Bubble sort", "I", Metric.Time,
            (n, _) => Consume(Vectors.BubbleSort(Vectors.GenerateRandomVector(n))),
            DefaultModel: "n^2"),

        new("quick_sort", "I. Quick sort", "I", Metric.Time,
            (n, _) => Consume(Vectors.QuickSort(Vectors.GenerateRandomVector(n))),
            DefaultModel: "n_log_n"),

        // Ключ "timsort" сохранён для совместимости со старой БД.
        new("timsort", "I. Timsort (встроенная сортировка)", "I", Metric.Time,
            (n, _) => Consume(Vectors.TimsortSort(Vectors.GenerateRandomVector(n))),
            DefaultModel: "n_log_n"),

        // ---------------------------- Часть II: матрицы ---------------------------- //
        new("matrix_multiply", "II. Умножение матриц O(n^3)", "II", Metric.Time,
            (n, m) => Consume(Matrices.GenerateAndMultiply(n, m)),
            NeedsM: true,
            DefaultModel: null), // для 3D-поверхности одномерная аппроксимация не строится

        // ------------------------- Часть III: свой алгоритм ------------------------ //
        new("custom_lcs", "III. LCS (заглушка, заменить на выбранный вариант)", "III", Metric.Time,
            (n, _) => Consume(Custom.LcsRandom(n)),
            DefaultModel: "n^2"),

        new("floyd_warshall", "III. Алгоритм Флойда-Уоршелла", "III", Metric.Time,
            (n, _) => Consume(Graphs.FloydWarshallRandom(n)),
            DefaultModel: "n^3"),

        new("fft", "III. Быстрое преобразование Фурье", "III", Metric.Time,
            (n, _) => Consume(Fft.FftRandom(n)),
            DefaultModel: "n_log_n"),

        new("kmp_search", "III. Алгоритм Кнута-Морриса-Пратта", "III", Metric.Time,
            (n, _) => Consume(StringSearch.KmpRandom(n)),
            DefaultModel: "n"),

        // ---------------------------- Часть IV: степень ---------------------------- //
        new("power_naive", "IV. Степень: наивная (итеративная)", "IV", Metric.Steps,
            (n, _) => ConsumeSteps(Power.PowerNaive(PowerBase, n)),
            DefaultModel: "n"),

        new("power_recursive", "IV. Степень: рекурсивная", "IV", Metric.Steps,
            (n, _) => ConsumeSteps(Power.PowerRecursive(PowerBase, n)),
            DefaultModel: "n"),

        new("power_binary", "IV. Степень: бинарная (быстрая)", "IV", Metric.Steps,
            (n, _) => ConsumeSteps(Power.PowerBinary(PowerBase, n)),
            DefaultModel: "log_n"),
    };

    private static readonly Dictionary<string, AlgorithmSpec> ByKey =
        All.ToDictionary(s => s.Key);

    public static bool Contains(string key) => ByKey.ContainsKey(key);

    public static bool TryGet(string key, out AlgorithmSpec spec) => ByKey.TryGetValue(key, out spec!);

    /// <summary>Достаёт AlgorithmSpec по ключу, с понятной ошибкой если ключ не найден.</summary>
    public static AlgorithmSpec Get(string key)
    {
        if (ByKey.TryGetValue(key, out var spec))
            return spec;
        throw new KeyNotFoundException(
            $"Алгоритм '{key}' не зарегистрирован. Доступные ключи: {string.Join(", ", ByKey.Keys)}");
    }
}

/// <summary>Раннеры экспериментов (связка "реестр алгоритмов" + "кэш замеров").</summary>
public static class BenchmarkRunner
{
    /// <summary>
    /// Небольшой "прогрев" перед экспериментом: первый вызов метода в .NET
    /// включает JIT-компиляцию и иначе исказил бы самую первую точку графика.
    /// Результаты прогрева в БД НЕ сохраняются.
    /// </summary>
    public static void WarmUp(AlgorithmSpec spec)
    {
        for (int i = 0; i < 3; i++)
            spec.Run(16, spec.NeedsM ? 16 : 0);
    }

    /// <summary>
    /// Прогоняет серию замеров для одного алгоритма по списку n (Части I, III, IV;
    /// и Часть II при фиксированном mValue - срез 3D-поверхности).
    /// progress(сделано, всего) вызывается после каждого n.
    /// </summary>
    public static List<BenchmarkResult> RunExperiment1D(
        BenchmarkCache cache,
        AlgorithmSpec spec,
        IReadOnlyList<int> nValues,
        int numRuns = 5,
        bool forceRecalculate = false,
        int? mValue = null,
        Action<int, int>? progress = null)
    {
        if (spec.NeedsM && mValue is null)
            throw new ArgumentException($"Алгоритм '{spec.Key}' требует указания m_value");

        WarmUp(spec);

        int m = mValue ?? 0;
        Func<int, long?> func = n => spec.Run(n, m);

        var results = new List<BenchmarkResult>(nValues.Count);
        int total = nValues.Count;
        for (int i = 0; i < total; i++)
        {
            results.Add(cache.GetOrCompute(
                algorithm: spec.Key,
                n: nValues[i],
                func: func,
                m: spec.NeedsM ? mValue : null,
                numRuns: numRuns,
                forceRecalculate: forceRecalculate));
            progress?.Invoke(i + 1, total);
        }
        return results;
    }

    /// <summary>
    /// Специально для Части II: замеры по ПОЛНОЙ сетке (n, m) для построения
    /// 3D-поверхности. results[i][j] соответствует nValues[i], mValues[j].
    /// </summary>
    public static List<List<BenchmarkResult>> RunExperiment2DGrid(
        BenchmarkCache cache,
        AlgorithmSpec spec,
        IReadOnlyList<int> nValues,
        IReadOnlyList<int> mValues,
        int numRuns = 5,
        bool forceRecalculate = false,
        Action<int, int>? progress = null)
    {
        if (!spec.NeedsM)
            throw new ArgumentException($"Алгоритм '{spec.Key}' не поддерживает сетку (n, m)");

        WarmUp(spec);

        var grid = new List<List<BenchmarkResult>>(nValues.Count);
        int total = nValues.Count * mValues.Count;
        int done = 0;

        foreach (int n in nValues)
        {
            var row = new List<BenchmarkResult>(mValues.Count);
            foreach (int m in mValues)
            {
                int mm = m;
                row.Add(cache.GetOrCompute(
                    algorithm: spec.Key,
                    n: n,
                    func: nn => spec.Run(nn, mm),
                    m: m,
                    numRuns: numRuns,
                    forceRecalculate: forceRecalculate));
                done++;
                progress?.Invoke(done, total);
            }
            grid.Add(row);
        }
        return grid;
    }

    /// <summary>Извлекает нужную метрику (mean_time или mean_steps) из списка результатов.</summary>
    public static List<double?> MetricValues(IEnumerable<BenchmarkResult> results, Metric metric)
        => results.Select(r => metric == Metric.Time ? r.MeanTime : r.MeanSteps).ToList();
}

/// <summary>Вспомогательные функции, которые в Python жили прямо в main_window.py.</summary>
public static class RangeUtil
{
    /// <summary>
    /// Список значений от start до maxValue с шагом step, ГАРАНТИРОВАННО
    /// включающий maxValue последней точкой (даже если он не попадает ровно на
    /// шаг) - иначе график был бы "обрезан" раньше, чем ожидает пользователь.
    /// </summary>
    public static List<int> BuildRange(int maxValue, int step, int start = 1)
    {
        step = Math.Max(1, step);
        start = Math.Max(1, start);
        if (start > maxValue)
            return new List<int> { maxValue };

        var values = new List<int>();
        for (long v = start; v <= maxValue; v += step)
            values.Add((int)v);
        if (values[^1] != maxValue)
            values.Add(maxValue);
        return values;
    }

    /// <summary>Форматирует длительность: мс для коротких, иначе "с" / "мин с".</summary>
    public static string FormatDuration(double seconds)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (seconds < 1)
            return string.Format(inv, "{0:F0} мс", seconds * 1000);
        if (seconds < 60)
            return string.Format(inv, "{0:F2} с", seconds);
        int minutes = (int)(seconds / 60);
        double sec = seconds - minutes * 60;
        return string.Format(inv, "{0} мин {1:F1} с", minutes, sec);
    }
}
