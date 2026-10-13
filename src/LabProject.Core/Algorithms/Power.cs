namespace LabProject.Algorithms;

/// <summary>
/// Часть IV лабораторной: три алгоритма возведения в степень (base^n).
///
/// ВАЖНО: по заданию здесь измеряется не время, а КОЛИЧЕСТВО ШАГОВ
/// (умножений и рекурсивных вызовов). Каждая функция возвращает пару
/// (результат, количество_шагов); у каждого вызова свой независимый счётчик.
///
/// Единый принцип подсчёта: +1 за каждую операцию умножения И +1 за каждый
/// рекурсивный вызов (для итеративного алгоритма рекурсии нет, поэтому
/// считаются только умножения). Метрики трёх алгоритмов сопоставимы.
///
/// Рекурсивные варианты рассчитаны на глубину рекурсии порядка n (до 10^6) -
/// поэтому приложение выполняет замеры в потоке с увеличенным стеком
/// (см. BigStack).
/// </summary>
public static class Power
{
    /// <summary>Наивное итеративное возведение в степень. O(n) умножений.</summary>
    public static (double Result, long Steps) PowerNaive(double @base, int n)
    {
        if (n < 0)
            throw new ArgumentException("Функция рассчитана на неотрицательные степени n");

        double result = 1.0;
        long steps = 0;
        for (int i = 0; i < n; i++)
        {
            result *= @base;
            steps++;
        }
        return (result, steps);
    }

    /// <summary>
    /// Рекурсивное возведение в степень по определению:
    ///     base^n = base * base^(n-1),  base^0 = 1
    /// O(n) - и по умножениям, и по глубине/числу рекурсивных вызовов.
    /// </summary>
    public static (double Result, long Steps) PowerRecursive(double @base, int n)
    {
        if (n < 0)
            throw new ArgumentException("Функция рассчитана на неотрицательные степени n");

        long steps = 0;
        double result = RecursiveHelper(@base, n, ref steps);
        return (result, steps);
    }

    private static double RecursiveHelper(double @base, int exp, ref long steps)
    {
        steps++; // сам факт рекурсивного вызова
        if (exp == 0)
            return 1.0;
        double value = @base * RecursiveHelper(@base, exp - 1, ref steps);
        steps++; // умножение
        return value;
    }

    /// <summary>
    /// Быстрое (бинарное) возведение в степень:
    ///     base^n = (base^(n/2))^2,        если n чётное
    ///     base^n = base * base^(n-1),     если n нечётное
    ///     base^0 = 1
    /// O(log n) - и по умножениям, и по глубине рекурсии/числу вызовов.
    /// </summary>
    public static (double Result, long Steps) PowerBinary(double @base, int n)
    {
        if (n < 0)
            throw new ArgumentException("Функция рассчитана на неотрицательные степени n");

        long steps = 0;
        double result = BinaryHelper(@base, n, ref steps);
        return (result, steps);
    }

    private static double BinaryHelper(double @base, int exp, ref long steps)
    {
        steps++; // сам факт рекурсивного вызова
        if (exp == 0)
            return 1.0;
        if (exp % 2 == 0)
        {
            double half = BinaryHelper(@base, exp / 2, ref steps);
            steps++; // умножение half * half
            return half * half;
        }
        else
        {
            double value = @base * BinaryHelper(@base, exp - 1, ref steps);
            steps++; // умножение base * helper(...)
            return value;
        }
    }
}
