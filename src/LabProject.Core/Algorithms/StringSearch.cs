namespace LabProject.Algorithms;

/// <summary>
/// Алгоритм Кнута-Морриса-Пратта (КМП): поиск всех вхождений подстроки
/// (pattern) в строке (text) без возвратов по тексту.
///
/// Сначала строится префикс-функция pi: pi[i] - длина наибольшего собственного
/// префикса pattern[..i], который одновременно является его суффиксом. При
/// несовпадении символа мы не откатываемся по тексту, а сдвигаем шаблон
/// на основе pi. Теоретическая сложность: O(n + m) по времени, O(m) по памяти.
/// </summary>
public static class StringSearch
{
    /// <summary>Префикс-функция строки. O(m).</summary>
    public static int[] PrefixFunction(string pattern)
    {
        int m = pattern.Length;
        var pi = new int[m];
        int k = 0;
        for (int i = 1; i < m; i++)
        {
            while (k > 0 && pattern[i] != pattern[k])
                k = pi[k - 1];
            if (pattern[i] == pattern[k])
                k++;
            pi[i] = k;
        }
        return pi;
    }

    /// <summary>
    /// Возвращает начальные позиции ВСЕХ (в том числе перекрывающихся)
    /// вхождений pattern в text. Для пустого шаблона возвращает пустой список.
    /// </summary>
    public static List<int> KmpSearch(string text, string pattern)
    {
        var occurrences = new List<int>();
        int n = text.Length, m = pattern.Length;
        if (m == 0 || m > n)
            return occurrences;

        int[] pi = PrefixFunction(pattern);
        int j = 0; // сколько символов шаблона сейчас совпало
        for (int i = 0; i < n; i++)
        {
            while (j > 0 && text[i] != pattern[j])
                j = pi[j - 1];
            if (text[i] == pattern[j])
                j++;
            if (j == m)
            {
                occurrences.Add(i - m + 1);
                j = pi[j - 1];
            }
        }
        return occurrences;
    }

    // ------------------------------------------------------------------ //
    // Данные и обёртка для замеров
    // ------------------------------------------------------------------ //

    /// <summary>Длина шаблона в замерах: min(n, 32).</summary>
    public const int BenchmarkPatternLength = 32;

    private static string RandomString(int length, string alphabet)
    {
        var rng = Random.Shared;
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = alphabet[rng.Next(alphabet.Length)];
        return new string(chars);
    }

    /// <summary>
    /// Обёртка под интерфейс замеров: случайный текст длины n над алфавитом
    /// из двух букв (много частичных совпадений - КМП работает "по-честному")
    /// и случайный шаблон длины min(n, 32). Возвращает число вхождений.
    /// </summary>
    public static int KmpRandom(int n)
    {
        const string alphabet = "ab";
        string text = RandomString(n, alphabet);
        string pattern = RandomString(Math.Min(n, BenchmarkPatternLength), alphabet);
        return KmpSearch(text, pattern).Count;
    }
}
