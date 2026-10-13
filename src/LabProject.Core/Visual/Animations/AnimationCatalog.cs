namespace LabProject.Visual.Animations;

/// <summary>
/// Каталог анимаций: у каждого алгоритма из реестра (ключ совпадает с
/// AlgorithmSpec.Key) есть своя пошаговая визуализация на небольших данных.
/// </summary>
public static class AnimationCatalog
{
    private static readonly Dictionary<string, Func<Random, Animation>> Factories = new()
    {
        ["vector_sum"] = r => BarAnimations.Reduce(r, product: false),
        ["vector_product"] = r => BarAnimations.Reduce(r, product: true),
        ["polynomial_naive"] = PolynomialPowerAnimations.PolynomialNaive,
        ["polynomial_horner"] = PolynomialPowerAnimations.PolynomialHorner,
        ["bubble_sort"] = BarAnimations.BubbleSort,
        ["quick_sort"] = BarAnimations.QuickSort,
        ["timsort"] = BarAnimations.MergeSort,
        ["matrix_multiply"] = TableAnimations.MatrixMultiply,
        ["custom_lcs"] = TableAnimations.Lcs,
        ["floyd_warshall"] = TableAnimations.FloydWarshall,
        ["fft"] = _ => SignalAnimations.Fft(),
        ["kmp_search"] = _ => SignalAnimations.Kmp(),
        ["power_naive"] = _ => PolynomialPowerAnimations.PowerNaive(),
        ["power_recursive"] = _ => PolynomialPowerAnimations.PowerRecursive(),
        ["power_binary"] = _ => PolynomialPowerAnimations.PowerBinary(),
    };

    public static IReadOnlyCollection<string> Keys => Factories.Keys;

    public static bool Has(string key) => Factories.ContainsKey(key);

    /// <summary>Создаёт анимацию; rng определяет случайные входные данные ("Новые данные").</summary>
    public static Animation Create(string key, Random? rng = null)
    {
        if (!Factories.TryGetValue(key, out var factory))
            throw new KeyNotFoundException($"Для алгоритма '{key}' визуализация не предусмотрена");
        return factory(rng ?? new Random());
    }
}
