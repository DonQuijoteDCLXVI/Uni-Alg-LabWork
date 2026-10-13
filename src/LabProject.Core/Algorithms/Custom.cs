namespace LabProject.Algorithms;

/// <summary>
/// Часть III лабораторной: нетривиальные алгоритмы.
///
/// Здесь остаётся исходный вариант (наибольшая общая подпоследовательность,
/// LCS, ДП, O(n^2)) - см. также Graphs.cs (Флойд-Уоршелл), Fft.cs (БПФ) и
/// StringSearch.cs (Кнут-Моррис-Пратт).
/// </summary>
public static class Custom
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

    private static string RandomString(int length)
    {
        var rng = Random.Shared;
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = Alphabet[rng.Next(Alphabet.Length)];
        return new string(chars);
    }

    /// <summary>
    /// Длина наибольшей общей подпоследовательности (LCS) двух строк,
    /// классическое ДП. Теоретическая сложность: O(len(a) * len(b)).
    /// </summary>
    public static int LcsLength(string a, string b)
    {
        int n = a.Length, m = b.Length;
        // dp[i][j] - длина LCS для a[..i] и b[..j]
        var dp = new int[n + 1][];
        for (int i = 0; i <= n; i++)
            dp[i] = new int[m + 1];

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                if (a[i - 1] == b[j - 1])
                    dp[i][j] = dp[i - 1][j - 1] + 1;
                else
                    dp[i][j] = Math.Max(dp[i - 1][j], dp[i][j - 1]);
            }
        }
        return dp[n][m];
    }

    /// <summary>
    /// Обёртка под интерфейс замеров: генерирует две случайные строки длины n
    /// и возвращает длину их LCS.
    /// </summary>
    public static int LcsRandom(int n)
    {
        string a = RandomString(n);
        string b = RandomString(n);
        return LcsLength(a, b);
    }
}
