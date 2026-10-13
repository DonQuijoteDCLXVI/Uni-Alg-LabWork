namespace LabProject.Algorithms;

/// <summary>
/// Алгоритм Флойда-Уоршелла: кратчайшие пути между всеми парами вершин
/// взвешенного графа (динамическое программирование по "промежуточной
/// вершине" k). Теоретическая сложность: O(n^3) по времени, O(n^2) по памяти.
/// </summary>
public static class Graphs
{
    public const double Inf = double.PositiveInfinity;

    /// <summary>
    /// Случайный ориентированный граф в виде матрицы весов n x n:
    /// w[i][i] = 0, ребро i->j существует с вероятностью edgeProbability и имеет
    /// целый вес 1..maxWeight, отсутствие ребра - Inf.
    /// </summary>
    public static double[][] GenerateRandomGraph(int n, double edgeProbability = 0.5, int maxWeight = 100)
    {
        var rng = Random.Shared;
        var w = new double[n][];
        for (int i = 0; i < n; i++)
        {
            w[i] = new double[n];
            for (int j = 0; j < n; j++)
            {
                if (i == j)
                    w[i][j] = 0.0;
                else
                    w[i][j] = rng.NextDouble() < edgeProbability ? rng.Next(1, maxWeight + 1) : Inf;
            }
        }
        return w;
    }

    /// <summary>
    /// Алгоритм Флойда-Уоршелла. Принимает матрицу весов (Inf - нет ребра),
    /// возвращает НОВУЮ матрицу кратчайших расстояний (исходная не меняется).
    /// Отрицательные циклы не обрабатываются (в генераторе веса положительные).
    /// </summary>
    public static double[][] FloydWarshall(double[][] weights)
    {
        int n = weights.Length;
        var dist = new double[n][];
        for (int i = 0; i < n; i++)
            dist[i] = (double[])weights[i].Clone();

        for (int k = 0; k < n; k++)
        {
            double[] rowK = dist[k];
            for (int i = 0; i < n; i++)
            {
                double[] rowI = dist[i];
                double dik = rowI[k];
                for (int j = 0; j < n; j++)
                {
                    double candidate = dik + rowK[j];
                    if (candidate < rowI[j])
                        rowI[j] = candidate;
                }
            }
        }
        return dist;
    }

    /// <summary>Обёртка под интерфейс замеров: случайный граф из n вершин + Флойд-Уоршелл.</summary>
    public static double[][] FloydWarshallRandom(int n)
    {
        return FloydWarshall(GenerateRandomGraph(n));
    }
}
