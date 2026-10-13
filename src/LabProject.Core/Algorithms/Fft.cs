using System.Numerics;

namespace LabProject.Algorithms;

/// <summary>
/// Быстрое преобразование Фурье (БПФ).
///
/// * Длина - степень двойки: итеративный алгоритм Кули-Тьюки (радикс-2,
///   прореживание по времени): битово-реверсная перестановка + "бабочки".
/// * Длина - любая другая: алгоритм Блюстейна (chirp-z), который сводит ДПФ
///   произвольной длины n к свёртке, вычисляемой тремя радикс-2 БПФ длины
///   m >= 2n-1. Благодаря этому БПФ корректно работает для ЛЮБОГО n, которое
///   задаёт пользователь в GUI (N_max, шаг).
///
/// Теоретическая сложность: O(n log n). У ДПФ по определению (Dft) - O(n^2).
/// На графике времени для произвольных n видны "ступеньки" - они вызваны
/// тем, что размер внутренних БПФ округляется вверх до степени двойки.
/// </summary>
public static class Fft
{
    public static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    private static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n)
            p <<= 1;
        return p;
    }

    /// <summary>Прямое ДПФ длины n. Возвращает НОВЫЙ массив, вход не меняется.</summary>
    public static Complex[] Transform(Complex[] input)
    {
        int n = input.Length;
        var a = (Complex[])input.Clone();
        if (n <= 1)
            return a;

        if (IsPowerOfTwo(n))
        {
            Radix2InPlace(a, inverse: false);
            return a;
        }
        return Bluestein(a);
    }

    /// <summary>Обратное ДПФ (с делением на n). Возвращает новый массив.</summary>
    public static Complex[] Inverse(Complex[] spectrum)
    {
        // IDFT(X) = conj(DFT(conj(X))) / n
        int n = spectrum.Length;
        var conj = new Complex[n];
        for (int i = 0; i < n; i++)
            conj[i] = Complex.Conjugate(spectrum[i]);
        var t = Transform(conj);
        for (int i = 0; i < n; i++)
            t[i] = Complex.Conjugate(t[i]) / n;
        return t;
    }

    /// <summary>ДПФ "по определению", O(n^2). Нужно для проверки корректности БПФ.</summary>
    public static Complex[] Dft(Complex[] x)
    {
        int n = x.Length;
        var result = new Complex[n];
        for (int k = 0; k < n; k++)
        {
            Complex sum = Complex.Zero;
            for (int t = 0; t < n; t++)
            {
                double angle = -2.0 * Math.PI * ((long)k * t % n) / n;
                sum += x[t] * new Complex(Math.Cos(angle), Math.Sin(angle));
            }
            result[k] = sum;
        }
        return result;
    }

    /// <summary>Итеративный радикс-2 БПФ на месте. Длина a - строго степень двойки.</summary>
    public static void Radix2InPlace(Complex[] a, bool inverse)
    {
        int n = a.Length;
        if (n <= 1)
            return;

        // 1. Битово-реверсная перестановка
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
                (a[i], a[j]) = (a[j], a[i]);
        }

        // 2. Таблица поворотных множителей W_n^k = exp(-+2*pi*i*k/n), k < n/2
        //    (точнее, чем накопление произведения w *= wlen).
        var twiddle = new Complex[n / 2];
        double sign = inverse ? 1.0 : -1.0;
        for (int k = 0; k < twiddle.Length; k++)
        {
            double angle = sign * 2.0 * Math.PI * k / n;
            twiddle[k] = new Complex(Math.Cos(angle), Math.Sin(angle));
        }

        // 3. "Бабочки"
        for (int len = 2; len <= n; len <<= 1)
        {
            int half = len >> 1;
            int step = n / len;
            for (int start = 0; start < n; start += len)
            {
                for (int j = 0; j < half; j++)
                {
                    Complex u = a[start + j];
                    Complex v = a[start + j + half] * twiddle[j * step];
                    a[start + j] = u + v;
                    a[start + j + half] = u - v;
                }
            }
        }

        if (inverse)
        {
            for (int i = 0; i < n; i++)
                a[i] /= n;
        }
    }

    /// <summary>БПФ произвольной длины по Блюстейну.</summary>
    private static Complex[] Bluestein(Complex[] x)
    {
        int n = x.Length;
        int m = NextPowerOfTwo(2 * n - 1);

        // chirp[k] = exp(-i*pi*k^2/n); k^2 берём по модулю 2n, чтобы не терять точность
        var chirp = new Complex[n];
        for (int k = 0; k < n; k++)
        {
            long k2 = ((long)k * k) % (2L * n);
            double angle = -Math.PI * k2 / n;
            chirp[k] = new Complex(Math.Cos(angle), Math.Sin(angle));
        }

        var a = new Complex[m];
        var b = new Complex[m];
        for (int k = 0; k < n; k++)
            a[k] = x[k] * chirp[k];

        b[0] = Complex.Conjugate(chirp[0]);
        for (int k = 1; k < n; k++)
        {
            Complex c = Complex.Conjugate(chirp[k]);
            b[k] = c;
            b[m - k] = c;
        }

        // Циклическая свёртка a * b через БПФ длины m
        Radix2InPlace(a, inverse: false);
        Radix2InPlace(b, inverse: false);
        for (int i = 0; i < m; i++)
            a[i] *= b[i];
        Radix2InPlace(a, inverse: true);

        var result = new Complex[n];
        for (int k = 0; k < n; k++)
            result[k] = a[k] * chirp[k];
        return result;
    }

    // ------------------------------------------------------------------ //
    // Данные и обёртка для замеров
    // ------------------------------------------------------------------ //

    /// <summary>Случайный комплексный сигнал длины n (вещественная и мнимая части в [-1, 1]).</summary>
    public static Complex[] GenerateRandomSignal(int n)
    {
        var rng = Random.Shared;
        var s = new Complex[n];
        for (int i = 0; i < n; i++)
            s[i] = new Complex(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
        return s;
    }

    /// <summary>Обёртка под интерфейс замеров: случайный сигнал длины n + БПФ.</summary>
    public static Complex[] FftRandom(int n)
    {
        return Transform(GenerateRandomSignal(n));
    }
}
