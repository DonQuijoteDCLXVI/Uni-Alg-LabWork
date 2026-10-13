namespace LabProject.Algorithms;

/// <summary>
/// Часть II лабораторной: умножение матриц классическим методом (3 цикла).
///
/// Для 3D-графика время должно зависеть от ДВУХ параметров: n и m.
/// Берём A размера (n x m) и B размера (m x n), их произведение C = A * B
/// имеет размер (n x n), а сложность умножения - O(n^2 * m):
/// n^2 элементов результата, каждый требует m умножений-сложений.
/// </summary>
public static class Matrices
{
    /// <summary>Генерирует матрицу rows x cols со случайными вещественными элементами.</summary>
    public static double[][] GenerateRandomMatrix(int rows, int cols, double low = -10.0, double high = 10.0)
    {
        var rng = Random.Shared;
        var result = new double[rows][];
        for (int i = 0; i < rows; i++)
        {
            var row = new double[cols];
            for (int j = 0; j < cols; j++)
                row[j] = low + (high - low) * rng.NextDouble();
            result[i] = row;
        }
        return result;
    }

    /// <summary>
    /// Классическое умножение матриц тремя вложенными циклами.
    /// A: (n x m), B: (m x p) -> результат (n x p).
    /// Теоретическая сложность: O(n * m * p), без оптимизаций (не Штрассен и т.п.).
    /// </summary>
    /// <exception cref="ArgumentException">Если размеры матриц несовместимы.</exception>
    public static double[][] Multiply(double[][] a, double[][] b)
    {
        int n = a.Length;
        int m = n > 0 ? a[0].Length : 0;
        int mCheck = b.Length;
        int p = mCheck > 0 ? b[0].Length : 0;

        if (m != mCheck)
        {
            throw new ArgumentException(
                $"Несовместимые размеры: A имеет {n}x{m}, B имеет {mCheck}x{p} " +
                "(число столбцов A должно равняться числу строк B)");
        }

        var result = new double[n][];
        for (int i = 0; i < n; i++)
        {
            result[i] = new double[p];
            for (int j = 0; j < p; j++)
            {
                double total = 0.0;
                for (int k = 0; k < m; k++)
                    total += a[i][k] * b[k][j];
                result[i][j] = total;
            }
        }
        return result;
    }

    /// <summary>
    /// Целевая функция бенчмарка с двумя параметрами: A (n x m), B (m x n),
    /// возвращает A*B (n x n). Время зависит и от n, и от m независимо.
    /// </summary>
    public static double[][] GenerateAndMultiply(int n, int m)
    {
        var a = GenerateRandomMatrix(n, m);
        var b = GenerateRandomMatrix(m, n);
        return Multiply(a, b);
    }
}
