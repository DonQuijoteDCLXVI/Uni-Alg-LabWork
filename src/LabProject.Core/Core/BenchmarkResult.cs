namespace LabProject.Core;

/// <summary>Агрегированный результат серии запусков для одного (algorithm, n[, m]).</summary>
public sealed record BenchmarkResult(
    string Algorithm,
    int N,
    int NumRuns,
    double? MeanTime,
    double? StdTime,
    double? MeanSteps,
    double? StdSteps,
    bool FromCache,      // true, если данные были взяты из БД, а не вычислены заново
    int? M = null)       // используется только для Части II (матрицы)
{
    public override string ToString()
    {
        var parts = new List<string> { $"BenchmarkResult(algorithm='{Algorithm}', n={N}" };
        if (MeanTime is double t)
            parts.Add($"mean_time={t:G6}s");
        if (MeanSteps is double s)
            parts.Add($"mean_steps={s:F1}");
        parts.Add($"from_cache={FromCache})");
        return string.Join(", ", parts);
    }
}
