namespace LabProject.Core;

/// <summary>Аналоги statistics.fmean / statistics.pstdev из Python.</summary>
public static class Stats
{
    /// <summary>Среднее арифметическое.</summary>
    public static double Mean(IReadOnlyList<double> values)
    {
        double sum = 0.0;
        foreach (double v in values)
            sum += v;
        return sum / values.Count;
    }

    /// <summary>Стандартное отклонение генеральной совокупности (делитель N).</summary>
    public static double PopulationStdDev(IReadOnlyList<double> values)
    {
        double mean = Mean(values);
        double acc = 0.0;
        foreach (double v in values)
            acc += (v - mean) * (v - mean);
        return Math.Sqrt(acc / values.Count);
    }
}
