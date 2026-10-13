namespace LabProject.Visual.Animations;

/// <summary>Анимации Горнера/наивного полинома и трёх способов возведения в степень.</summary>
internal static class PolynomialPowerAnimations
{
    private static string Poly(int[] c, int x)
    {
        var parts = new List<string>();
        for (int i = 0; i < c.Length; i++)
            parts.Add(i == 0 ? $"{c[i]}" : i == 1 ? $"{c[i]}·x" : $"{c[i]}·x^{i}");
        return $"P(x) = {string.Join(" + ", parts)},  x = {x}";
    }

    private static string Fmt(long v) => v.ToString();

    // ------------------------------- наивный полином ------------------------------- //

    public static Animation PolynomialNaive(Random rng)
    {
        int[] c = Draw.RandomInts(rng, 5, -4, 5);
        const int x = 2;
        int n = c.Length;
        var b = new AnimationBuilder("Полином: наивное вычисление — O(n²)");

        var pow = new string[n]; var term = new string[n];
        Array.Fill(pow, ""); Array.Fill(term, "");
        long result = 0; int mults = 0;

        void Snap(string cap, int cur, int mark)
        {
            var pw = (string[])pow.Clone(); var tm = (string[])term.Clone();
            long res = result; int mc = mults;
            b.Add(cap, s =>
            {
                const double x0 = 150, y0 = 130, cw = 150, ch = 52;
                s.Text(40, 100, Poly(c, x), 15, Palette.Ink, TextAlign.Left);
                string[] labels = { "i", "c[i]", "x^i", "c[i]·x^i" };
                for (int r = 0; r < 4; r++)
                    s.Text(x0 - 12, y0 + r * (ch + 8) + ch / 2, labels[r], 14, Palette.Muted, TextAlign.Right);
                for (int i = 0; i < n; i++)
                {
                    double cx = x0 + i * (cw + 6) / 1.0 * 0.82;
                    Rgba hl = i == cur ? Palette.AmberSoft : i < cur ? Palette.GreenSoft : Palette.White;
                    Rgba? st = i == cur ? Palette.Amber : null;
                    double w = cw * 0.78;
                    Draw.Cell(s, cx, y0, w, ch, i.ToString(), Palette.Light);
                    Draw.Cell(s, cx, y0 + (ch + 8), w, ch, c[i].ToString(), hl, st);
                    Draw.Cell(s, cx, y0 + 2 * (ch + 8), w, ch, pw[i], i == cur ? (mark == 1 ? Palette.AmberSoft : hl) : hl, i == cur ? Palette.Amber : null);
                    Draw.Cell(s, cx, y0 + 3 * (ch + 8), w, ch, tm[i], tm[i].Length > 0 ? Palette.GreenSoft : Palette.White);
                }
                s.Text(40, 420, $"P(x) = {res}", 30, Palette.Ink, TextAlign.Left, true);
                s.Text(40, 470, $"Умножений выполнено: {mc}", 20, mc > 0 ? Palette.Red : Palette.Muted, TextAlign.Left, true);
                s.Text(40, 510, "Для каждого слагаемого степень x^i считается заново отдельным циклом.", 14, Palette.Muted, TextAlign.Left);
            });
        }

        Snap("Для каждого слагаемого вычисляем x^i заново, затем прибавляем c[i]·x^i.", -1, 0);
        for (int i = 0; i < n; i++)
        {
            long power = 1;
            pow[i] = "1";
            Snap($"Слагаемое i={i}: x^{i} считаем циклом из {i} умножений, начиная с 1", i, 1);
            for (int k = 1; k <= i; k++)
            {
                power *= x; mults++;
                pow[i] = Fmt(power);
                Snap($"Умножение {k} из {i}: x^{i} ← {Fmt(power / x)}·{x} = {Fmt(power)}", i, 1);
            }
            long t = c[i] * power;
            term[i] = Fmt(t);
            result += t;
            Snap($"Прибавляем c[{i}]·x^{i} = {c[i]}·{power} = {t}; сумма = {result}", i, 2);
        }
        Snap($"Готово: P({x}) = {result}. Всего умножений: {mults} = n(n−1)/2 — квадратичный рост.", n, 2);
        return b.Build();
    }

    // ------------------------------- Горнер ------------------------------- //

    public static Animation PolynomialHorner(Random rng)
    {
        int[] c = Draw.RandomInts(rng, 5, -4, 5);
        const int x = 2;
        int n = c.Length;
        var b = new AnimationBuilder("Полином: схема Горнера — O(n)");

        var res = new string[n]; Array.Fill(res, "");
        long result = 0; int mults = 0;

        void Snap(string cap, int step)
        {
            var rs = (string[])res.Clone(); long r = result; int mc = mults;
            b.Add(cap, s =>
            {
                s.Text(40, 100, Poly(c, x), 15, Palette.Ink, TextAlign.Left);
                const double x0 = 150, y0 = 150, w = 110, ch = 56;
                s.Text(x0 - 12, y0 + ch / 2, "c[i]", 14, Palette.Muted, TextAlign.Right);
                s.Text(x0 - 12, y0 + ch + 12 + ch / 2, "result", 14, Palette.Muted, TextAlign.Right);
                for (int k = 0; k < n; k++)
                {
                    int idx = n - 1 - k;
                    double cx = x0 + k * (w + 12);
                    bool cur = k == step, done = k < step;
                    Draw.Cell(s, cx, y0 - 34, w, 28, $"i = {idx}", Palette.Light, null, Palette.Muted, 12);
                    Draw.Cell(s, cx, y0, w, ch, c[idx].ToString(), cur ? Palette.AmberSoft : done ? Palette.GreenSoft : Palette.White, cur ? Palette.Amber : null);
                    Draw.Cell(s, cx, y0 + ch + 12, w, ch, rs[k], rs[k].Length > 0 ? (cur ? Palette.AmberSoft : Palette.GreenSoft) : Palette.White, cur ? Palette.Amber : null);
                }
                s.Text(40, 400, $"result = {r}", 30, Palette.Ink, TextAlign.Left, true);
                s.Text(40, 450, $"Умножений выполнено: {mc}", 20, mc > 0 ? Palette.Green : Palette.Muted, TextAlign.Left, true);
                s.Text(40, 490, "Коэффициенты идут от старшей степени к младшей; на каждом шаге — одно умножение и одно сложение.", 14, Palette.Muted, TextAlign.Left);
            });
        }

        Snap("Идём от старшего коэффициента к младшему: result = result·x + c[i].", -1);
        for (int k = 0; k < n; k++)
        {
            int idx = n - 1 - k;
            long prev = result;
            result = result * x + c[idx];
            if (k > 0) mults++;
            res[k] = Fmt(result);
            Snap(k == 0 ? $"Начало: result = c[{idx}] = {result}" : $"result = {prev}·{x} + {c[idx]} = {result}", k);
        }
        Snap($"Готово: P({x}) = {result}. Умножений: {mults} = n−1 — линейный рост.", n);
        return b.Build();
    }

    // ------------------------------- степень ------------------------------- //

    private sealed record StackItem(string Label, string? Value, bool Active);

    private static IEnumerable<string> Wrap(string text, int maxChars)
    {
        var line = "";
        foreach (var word in text.Split(' '))
        {
            if (line.Length + word.Length + 1 > maxChars && line.Length > 0) { yield return line; line = word; }
            else line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) yield return line;
    }

    private static void DrawStack(Scene s, IReadOnlyList<StackItem> stack, long steps, string? result, string hint)
    {
        const double x = 60, w = 400, h = 26, pitch = 29, baseY = 570;
        s.Text(x, 112, "Стек вызовов", 14, Palette.Muted, TextAlign.Left, true);
        for (int i = 0; i < stack.Count; i++)
        {
            double y = baseY - (i + 1) * pitch;
            var it = stack[i];
            Rgba fill = it.Value is not null ? Palette.GreenSoft : it.Active ? Palette.AmberSoft : Palette.BlueSoft;
            Draw.Cell(s, x, y, w, h, it.Value is null ? it.Label : $"{it.Label}  =  {it.Value}", fill, it.Active ? Palette.Amber : null, null, 13);
        }
        s.Text(560, 200, $"Шагов: {steps}", 40, Palette.Ink, TextAlign.Left, true);
        s.Text(560, 245, "каждый вызов и каждое умножение = 1 шаг", 13, Palette.Muted, TextAlign.Left);
        if (result is not null)
            s.Text(560, 315, $"Результат: {result}", 26, Palette.Green, TextAlign.Left, true);
        double ty = 400;
        foreach (var line in Wrap(hint, 48))
        {
            s.Text(560, ty, line, 14, Palette.Muted, TextAlign.Left);
            ty += 22;
        }
    }

    private const int PowN = 13;

    public static Animation PowerNaive()
    {
        var b = new AnimationBuilder($"Степень: наивная — 2^{PowN}, O(n)");
        long value = 1, steps = 0;
        void Snap(string cap, int upto, string? result)
        {
            long st = steps; long v = value;
            b.Add(cap, s =>
            {
                const double x0 = 40, y0 = 150, cw = 64, ch = 52;
                for (int k = 1; k <= PowN; k++)
                {
                    int col = (k - 1) % 13; double cx = x0 + col * (cw + 6);
                    bool done = k <= upto, cur = k == upto;
                    Draw.Cell(s, cx, y0, cw, ch, done ? $"2^{k}" : "", done ? (cur ? Palette.AmberSoft : Palette.GreenSoft) : Palette.White, cur ? Palette.Amber : null, null, 13);
                    s.Text(cx + cw / 2, y0 - 14, k.ToString(), 11, Palette.Muted);
                    if (done)
                    {
                        double val = Math.Pow(2, k);
                        s.Text(cx + cw / 2, y0 + ch + 16, val.ToString("0"), 11, Palette.Ink);
                    }
                }
                s.Text(40, 300, $"result = {v}", 30, Palette.Ink, TextAlign.Left, true);
                s.Text(40, 350, $"Шагов (умножений): {st}", 26, Palette.Red, TextAlign.Left, true);
                if (result is not null) s.Text(40, 400, $"Итог: 2^{PowN} = {result}", 22, Palette.Green, TextAlign.Left, true);
                s.Text(40, 470, "Цикл делает ровно n умножений — число шагов растёт линейно.", 14, Palette.Muted, TextAlign.Left);
            });
        }
        Snap("result = 1; в цикле умножаем на основание (2) ровно n раз.", 0, null);
        for (int k = 1; k <= PowN; k++)
        {
            value *= 2; steps++;
            Snap($"Итерация {k}: result = result·2 = {value}", k, null);
        }
        Snap($"Готово: {steps} умножений для n = {PowN}.", PowN, value.ToString());
        return b.Build();
    }

    public static Animation PowerRecursive()
    {
        var b = new AnimationBuilder($"Степень: рекурсивная — 2^{PowN}, O(n)");
        var stack = new List<StackItem>();
        long steps = 0;

        void Snap(string cap, string? result = null)
        {
            var st = stack.ToList(); long sc = steps;
            b.Add(cap, s => DrawStack(s, st, sc, result, "Глубина рекурсии растёт до n: каждый уровень ждёт результат следующего."));
        }

        long Rec(int exp)
        {
            steps++;
            stack.Add(new StackItem($"power({exp})", null, true));
            Snap($"Вызов power({exp}): +1 шаг" + (exp == 0 ? " — база рекурсии" : $", вызываем power({exp - 1})"));
            long v;
            if (exp == 0) v = 1;
            else
            {
                long inner = Rec(exp - 1);
                steps++;
                v = 2 * inner;
                stack[^1] = new StackItem($"power({exp})", null, true);
                Snap($"Вернулись в power({exp}): 2·{inner} = {v}, умножение +1 шаг");
            }
            stack[^1] = new StackItem($"power({exp})", v.ToString(), false);
            Snap($"power({exp}) возвращает {v}");
            stack.RemoveAt(stack.Count - 1);
            return v;
        }

        long r = Rec(PowN);
        Snap($"Готово: {steps} шагов = 2n + 1 для n = {PowN}.", r.ToString());
        return b.Build();
    }

    public static Animation PowerBinary()
    {
        var b = new AnimationBuilder($"Степень: бинарная (быстрая) — 2^{PowN}, O(log n)");
        var stack = new List<StackItem>();
        long steps = 0;

        void Snap(string cap, string? result = null)
        {
            var st = stack.ToList(); long sc = steps;
            b.Add(cap, s => DrawStack(s, st, sc, result, "Чётная степень — делим пополам; нечётная — выделяем один множитель."));
        }

        long Rec(int exp)
        {
            steps++;
            stack.Add(new StackItem($"power({exp})", null, true));
            long v;
            if (exp == 0)
            {
                Snap("Вызов power(0): +1 шаг — база рекурсии");
                v = 1;
            }
            else if (exp % 2 == 0)
            {
                Snap($"Вызов power({exp}): +1 шаг, {exp} чётное → считаем power({exp / 2}) и возводим в квадрат");
                long half = Rec(exp / 2);
                steps++;
                v = half * half;
                Snap($"power({exp}) = {half}² = {v}, умножение +1 шаг");
            }
            else
            {
                Snap($"Вызов power({exp}): +1 шаг, {exp} нечётное → 2·power({exp - 1})");
                long inner = Rec(exp - 1);
                steps++;
                v = 2 * inner;
                Snap($"power({exp}) = 2·{inner} = {v}, умножение +1 шаг");
            }
            stack[^1] = new StackItem($"power({exp})", v.ToString(), false);
            Snap($"power({exp}) возвращает {v}");
            stack.RemoveAt(stack.Count - 1);
            return v;
        }

        long r = Rec(PowN);
        Snap($"Готово: всего {steps} шагов вместо {2 * PowN + 1} у рекурсивной версии — рост логарифмический.", r.ToString());
        return b.Build();
    }
}
