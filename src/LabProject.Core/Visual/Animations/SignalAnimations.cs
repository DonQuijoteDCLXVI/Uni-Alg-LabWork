using System.Globalization;
using System.Numerics;

namespace LabProject.Visual.Animations;

/// <summary>Анимации БПФ (диаграмма «бабочек») и алгоритма Кнута-Морриса-Пратта.</summary>
internal static class SignalAnimations
{
    // ------------------------------- БПФ ------------------------------- //

    private static string Fmt(Complex c)
    {
        var inv = CultureInfo.InvariantCulture;
        double re = Math.Round(c.Real, 2), im = Math.Round(c.Imaginary, 2);
        if (Math.Abs(im) < 0.005) return re.ToString("0.##", inv);
        if (Math.Abs(re) < 0.005) return im.ToString("0.##", inv) + "i";
        return re.ToString("0.##", inv) + (im < 0 ? "−" : "+") + Math.Abs(im).ToString("0.##", inv) + "i";
    }

    public static Animation Fft()
    {
        const int n = 8, stages = 3;
        // входной сигнал: сумма двух гармоник (1-й и 3-й), чтобы спектр был наглядным
        var x = new Complex[n];
        for (int j = 0; j < n; j++)
            x[j] = Math.Round(Math.Sin(2 * Math.PI * j / n) + 0.5 * Math.Cos(2 * Math.PI * 3 * j / n), 2);

        int Rev(int v) { int r = 0; for (int b = 0; b < stages; b++) { r = (r << 1) | ((v >> b) & 1); } return r; }

        // columns[0] - вход, [1] - после битовой перестановки, [2..4] - стадии бабочек
        var cols = new Complex?[stages + 2][];
        for (int c = 0; c < cols.Length; c++) cols[c] = new Complex?[n];
        for (int j = 0; j < n; j++) cols[0][j] = x[j];

        var anim = new AnimationBuilder("Быстрое преобразование Фурье (Кули–Тьюки, n = 8) — O(n log n)");
        string[] colTitles = { "вход x[j]", "после перестановки", "стадия 1", "стадия 2", "стадия 3" };

        void Snap(string cap, int activeCol, int[] srcRows, int[] dstRows, string twiddle, bool revLines)
        {
            var snapshot = cols.Select(c => (Complex?[])c.Clone()).ToArray();
            anim.Add(cap, s =>
            {
                const double x0 = 60, cw = 112, colStep = 172, y0 = 120, rowStep = 38, rh = 30;
                double Cx(int c) => x0 + c * colStep;
                double Cy(int r) => y0 + r * rowStep;

                for (int c = 0; c < snapshot.Length; c++)
                    s.Text(Cx(c) + cw / 2, y0 - 22, colTitles[c], 13, Palette.Muted, TextAlign.Center, true);

                if (revLines)
                    for (int j = 0; j < n; j++)
                        s.Line(Cx(0) + cw, Cy(j) + rh / 2, Cx(1), Cy(Rev(j)) + rh / 2, Palette.Border, 1.5);

                if (srcRows.Length > 0 && activeCol >= 2)
                    foreach (int sr in srcRows)
                        foreach (int dr in dstRows)
                            s.Line(Cx(activeCol - 1) + cw, Cy(sr) + rh / 2, Cx(activeCol), Cy(dr) + rh / 2, Palette.Amber, 2.2);

                for (int c = 0; c < snapshot.Length; c++)
                    for (int r = 0; r < n; r++)
                    {
                        var v = snapshot[c][r];
                        bool isDst = c == activeCol && dstRows.Contains(r);
                        bool isSrc = c == activeCol - 1 && srcRows.Contains(r);
                        Rgba fill = isDst ? Palette.AmberSoft : isSrc ? Palette.BlueSoft : v is null ? Palette.Light : Palette.White;
                        Rgba? st = isDst ? Palette.Amber : isSrc ? Palette.Blue : null;
                        Draw.Cell(s, Cx(c), Cy(r), cw, rh, v is null ? "" : Fmt(v.Value), fill, st, null, 12);
                    }

                if (twiddle.Length > 0)
                    s.Text(40, 442, twiddle, 15, Palette.Ink, TextAlign.Left, true);

                // |значение| последней заполненной колонки
                int lastCol = 0;
                for (int c = 0; c < snapshot.Length; c++) if (snapshot[c].Any(v => v is not null)) lastCol = c;
                var mags = snapshot[lastCol].Select(v => v is null ? 0.0 : v.Value.Magnitude).ToArray();
                double maxMag = Math.Max(1e-9, Math.Max(mags.Max(), 1.0));
                s.Text(40, 478, $"Модуль значений ({colTitles[lastCol]}):", 13, Palette.Muted, TextAlign.Left);
                for (int r = 0; r < n; r++)
                {
                    double h = mags[r] / maxMag * 80;
                    double bx = 60 + r * 100;
                    s.Rect(bx, 580 - h, 60, Math.Max(2, h), lastCol == 4 ? Palette.Purple : Palette.Blue, null, 0, 3);
                    s.Text(bx + 30, 590, lastCol == 4 ? $"k={r}" : $"{r}", 11, Palette.Muted);
                }
            });
        }

        Snap("Входной сигнал x[0..7] — сумма двух гармоник. Ищем его спектр X[k].", 0, Array.Empty<int>(), Array.Empty<int>(), "", false);

        // битово-реверсная перестановка
        for (int j = 0; j < n; j++) cols[1][Rev(j)] = x[j];
        Snap("Шаг 1: битово-реверсная перестановка (индекс читаем справа налево: 001 → 100 и т.д.).", 1, Array.Empty<int>(), Array.Empty<int>(), "", true);

        var a = (Complex[])cols[1].Select(v => v!.Value).ToArray();
        int col = 2;
        for (int len = 2; len <= n; len <<= 1, col++)
        {
            int half = len / 2, step = n / len;
            for (int start = 0; start < n; start += len)
                for (int j = 0; j < half; j++)
                {
                    int p = start + j, q = start + j + half;
                    int k = j * step;
                    Complex w = Complex.FromPolarCoordinates(1, -2 * Math.PI * k / n);
                    Complex u = a[p], v = a[q] * w;
                    a[p] = u + v; a[q] = u - v;
                    cols[col][p] = a[p]; cols[col][q] = a[q];
                    Snap($"Стадия {col - 1}, «бабочка» ({p}, {q}):  новое[{p}] = u + w·v,  новое[{q}] = u − w·v",
                        col, new[] { p, q }, new[] { p, q },
                        $"w = e^(−2πi·{k}/{n}) = {Fmt(w)},   u = {Fmt(u)},   w·v = {Fmt(v)}", false);
                }
        }
        Snap("Готово: последняя колонка — спектр X[k]. Выделяются гармоники k = 1 и k = 3 (и зеркальные 7, 5).",
            4, Array.Empty<int>(), Array.Empty<int>(), "log₂8 = 3 стадии по 4 «бабочки» вместо 8·8 = 64 умножений у обычного ДПФ.", false);
        return anim.Build();
    }

    // ------------------------------- КМП ------------------------------- //

    public static Animation Kmp()
    {
        const string text = "ABABDABACDABABCABAB", pat = "ABABCABAB";
        int n = text.Length, m = pat.Length;
        var pi = new int[m];
        var anim = new AnimationBuilder("Алгоритм Кнута–Морриса–Пратта — O(n + m)");

        // состояние для отрисовки
        int piFilled = 0;                     // сколько значений pi уже посчитано
        var found = new List<int>();

        void Snap(string cap, int shift, int ti, int pj, Rgba? pairColor, int piI = -1, int piK = -1, bool showText = true)
        {
            var piSnap = (int[])pi.Clone(); int filled = piFilled; var foundSnap = found.ToList();
            anim.Add(cap, s =>
            {
                const double x0 = 70, cw = 42, ch = 42;
                if (showText)
                {
                    s.Text(40, 108, "Текст", 13, Palette.Muted, TextAlign.Left, true);
                    for (int i = 0; i < n; i++)
                    {
                        bool inMatch = foundSnap.Any(f => i >= f && i < f + m);
                        Rgba fill = i == ti && pairColor is not null ? pairColor.Value : inMatch ? Palette.GreenSoft : Palette.White;
                        Draw.Cell(s, x0 + i * (cw + 3), 124, cw, ch, text[i].ToString(), fill, i == ti ? Palette.Ink : null, null, 17, i == ti);
                        s.Text(x0 + i * (cw + 3) + cw / 2, 124 + ch + 12, i.ToString(), 10, Palette.Muted);
                    }
                    s.Text(40, 218, "Шаблон", 13, Palette.Muted, TextAlign.Left, true);
                    for (int j = 0; j < m; j++)
                    {
                        int pos = shift + j;
                        if (pos < 0 || pos >= n) continue;
                        Rgba fill = j == pj && pairColor is not null ? pairColor.Value : j < pj ? Palette.GreenSoft : Palette.BlueSoft;
                        Draw.Cell(s, x0 + pos * (cw + 3), 236, cw, ch, pat[j].ToString(), fill, j == pj ? Palette.Ink : null, null, 17, j == pj);
                    }
                    foreach (int f in foundSnap)
                        s.Rect(x0 + f * (cw + 3), 284, m * (cw + 3) - 3, 6, Palette.Green, null, 0, 3);
                }

                // таблица префикс-функции
                s.Text(40, 330, "Префикс-функция π шаблона", 13, Palette.Muted, TextAlign.Left, true);
                for (int j = 0; j < m; j++)
                {
                    bool isI = j == piI, isK = j == piK;
                    Draw.Cell(s, x0 + j * (cw + 3), 346, cw, ch, pat[j].ToString(), isI ? Palette.AmberSoft : isK ? Palette.BlueSoft : Palette.Light, isI ? Palette.Amber : isK ? Palette.Blue : null, null, 16, true);
                    Draw.Cell(s, x0 + j * (cw + 3), 346 + ch + 3, cw, ch, j < filled ? piSnap[j].ToString() : "", j < filled ? Palette.White : Palette.Light, isI ? Palette.Amber : null, null, 16);
                    s.Text(x0 + j * (cw + 3) + cw / 2, 346 + 2 * ch + 14, j.ToString(), 10, Palette.Muted);
                }
                s.Text(40, 470, "π[i] — длина самого длинного собственного префикса шаблона, который одновременно является суффиксом шаблона[0..i].", 13, Palette.Muted, TextAlign.Left);
                if (foundSnap.Count > 0)
                    s.Text(40, 510, $"Найдено вхождений: {foundSnap.Count}  (позиции: {string.Join(", ", foundSnap)})", 20, Palette.Green, TextAlign.Left, true);
            });
        }

        // фаза 1: префикс-функция
        Snap("Фаза 1. Строим префикс-функцию шаблона — она подскажет, на сколько сдвигать шаблон при несовпадении.", 0, -1, -1, null, showText: false);
        piFilled = 1;
        Snap("π[0] = 0 всегда: у строки из одного символа нет собственного префикса.", 0, -1, -1, null, 0, -1, false);
        int k = 0;
        for (int i = 1; i < m; i++)
        {
            while (k > 0 && pat[i] != pat[k])
            {
                Snap($"i={i}: «{pat[i]}» ≠ «{pat[k]}» (k={k}) → откатываем k = π[{k - 1}] = {pi[k - 1]}", 0, -1, -1, null, i, k, false);
                k = pi[k - 1];
            }
            if (pat[i] == pat[k])
            {
                Snap($"i={i}: «{pat[i]}» = «{pat[k]}» → k увеличиваем до {k + 1}", 0, -1, -1, null, i, k, false);
                k++;
            }
            pi[i] = k; piFilled = i + 1;
            Snap($"π[{i}] = {k}", 0, -1, -1, null, i, -1, false);
        }

        // фаза 2: поиск
        Snap("Фаза 2. Ищем шаблон в тексте. Указатель по тексту НИКОГДА не двигается назад.", 0, -1, -1, null);
        int j2 = 0;
        for (int i = 0; i < n; i++)
        {
            while (j2 > 0 && text[i] != pat[j2])
            {
                Snap($"text[{i}]=«{text[i]}» ≠ pat[{j2}]=«{pat[j2]}» → сдвигаем шаблон: j = π[{j2 - 1}] = {pi[j2 - 1]}", i - j2, i, j2, Palette.RedSoft);
                j2 = pi[j2 - 1];
            }
            if (text[i] == pat[j2])
            {
                Snap($"text[{i}]=«{text[i]}» = pat[{j2}] → совпадение, j = {j2 + 1}", i - j2, i, j2, Palette.GreenSoft);
                j2++;
            }
            else
            {
                Snap($"text[{i}]=«{text[i]}» ≠ pat[0]=«{pat[0]}» → шаблон сдвигается дальше", i, i, 0, Palette.RedSoft);
            }
            if (j2 == m)
            {
                found.Add(i - m + 1);
                Snap($"Шаблон найден целиком! Вхождение начинается с позиции {i - m + 1}.", i - m + 1, -1, -1, null);
                j2 = pi[j2 - 1];
            }
        }
        Snap($"Готово: текст пройден один раз за {n} шагов; найдено вхождений: {found.Count}.", 0, -1, -1, null);
        return anim.Build();
    }
}
