namespace LabProject.Algorithms;

/// <summary>
/// Часть I лабораторной: операции над векторами (массивами чисел).
///
/// Все сортирующие функции НЕ мутируют исходный массив (возвращают копию) -
/// это важно, чтобы при многократных замерах каждый прогон получал одинаковый
/// неотсортированный вход, а не результат предыдущего.
/// </summary>
public static class Vectors
{
    // ------------------------------------------------------------------ //
    // Вспомогательная генерация данных
    // ------------------------------------------------------------------ //

    /// <summary>Генерирует вектор из n случайных вещественных чисел в [low, high].</summary>
    public static double[] GenerateRandomVector(int n, double low = -1000.0, double high = 1000.0)
    {
        var rng = Random.Shared;
        var v = new double[n];
        for (int i = 0; i < n; i++)
            v[i] = low + (high - low) * rng.NextDouble();
        return v;
    }

    // ------------------------------------------------------------------ //
    // Сумма и произведение - O(n)
    // ------------------------------------------------------------------ //

    /// <summary>Сумма элементов вектора. Теоретическая сложность: O(n).</summary>
    public static double VectorSum(double[] vector)
    {
        double total = 0.0;
        foreach (double value in vector)
            total += value;
        return total;
    }

    /// <summary>Произведение элементов вектора. Теоретическая сложность: O(n).</summary>
    public static double VectorProduct(double[] vector)
    {
        double product = 1.0;
        foreach (double value in vector)
            product *= value;
        return product;
    }

    // ------------------------------------------------------------------ //
    // Вычисление полинома: coefficients[i] - коэффициент при x^i
    // (coefficients[0] - свободный член, coefficients[^1] - при старшей степени)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Наивное вычисление полинома: для каждого слагаемого степень x^i
    /// считается отдельным циклом умножений (быстрое возведение в степень
    /// не используется). Теоретическая сложность: O(n^2).
    /// </summary>
    public static double PolynomialNaive(double[] coefficients, double x)
    {
        double result = 0.0;
        for (int i = 0; i < coefficients.Length; i++)
        {
            double power = 1.0;
            for (int k = 0; k < i; k++)
                power *= x;
            result += coefficients[i] * power;
        }
        return result;
    }

    /// <summary>
    /// Вычисление полинома схемой Горнера:
    ///     P(x) = (...((a_n * x + a_{n-1}) * x + a_{n-2}) * x + ... ) * x + a_0
    /// Теоретическая сложность: O(n).
    /// </summary>
    public static double PolynomialHorner(double[] coefficients, double x)
    {
        double result = 0.0;
        for (int i = coefficients.Length - 1; i >= 0; i--)
            result = result * x + coefficients[i];
        return result;
    }

    // ------------------------------------------------------------------ //
    // Сортировки
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Сортировка пузырьком с ранним выходом (если за проход не было ни одного
    /// обмена - массив уже отсортирован).
    /// Теоретическая сложность: O(n^2) в среднем/худшем случае, O(n) в лучшем.
    /// </summary>
    public static double[] BubbleSort(double[] vector)
    {
        var arr = (double[])vector.Clone();
        int n = arr.Length;
        for (int i = 0; i < n; i++)
        {
            bool swapped = false;
            for (int j = 0; j < n - i - 1; j++)
            {
                if (arr[j] > arr[j + 1])
                {
                    (arr[j], arr[j + 1]) = (arr[j + 1], arr[j]);
                    swapped = true;
                }
            }
            if (!swapped)
                break;
        }
        return arr;
    }

    /// <summary>
    /// Быстрая сортировка (схема Ломуто, пивот - последний элемент).
    /// Теоретическая сложность: O(n log n) в среднем, O(n^2) в худшем случае.
    /// </summary>
    public static double[] QuickSort(double[] vector)
    {
        var arr = (double[])vector.Clone();
        QuickSortInPlace(arr, 0, arr.Length - 1);
        return arr;
    }

    private static void QuickSortInPlace(double[] arr, int low, int high)
    {
        while (low < high)
        {
            int pivotIndex = Partition(arr, low, high);
            // Рекурсируем в меньшую часть, а большую обрабатываем в цикле
            // (хвостовая оптимизация вручную, глубина рекурсии O(log n)).
            if (pivotIndex - low < high - pivotIndex)
            {
                QuickSortInPlace(arr, low, pivotIndex - 1);
                low = pivotIndex + 1;
            }
            else
            {
                QuickSortInPlace(arr, pivotIndex + 1, high);
                high = pivotIndex - 1;
            }
        }
    }

    private static int Partition(double[] arr, int low, int high)
    {
        double pivot = arr[high];
        int i = low - 1;
        for (int j = low; j < high; j++)
        {
            if (arr[j] <= pivot)
            {
                i++;
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }
        (arr[i + 1], arr[high]) = (arr[high], arr[i + 1]);
        return i + 1;
    }

    /// <summary>
    /// Обёртка над встроенной сортировкой (в Python - sorted()/Timsort, в .NET -
    /// Array.Sort, introsort). Оставлена для единообразия интерфейса при замерах
    /// (все сортировки имеют сигнатуру double[] -> double[]).
    /// Теоретическая сложность: O(n log n).
    /// </summary>
    public static double[] TimsortSort(double[] vector)
    {
        var arr = (double[])vector.Clone();
        Array.Sort(arr);
        return arr;
    }
}
