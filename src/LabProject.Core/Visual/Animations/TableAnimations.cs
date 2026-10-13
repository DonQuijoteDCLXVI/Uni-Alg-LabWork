namespace LabProject.Visual.Animations;

/// <summary>Анимации на таблицах/графе: умножение матриц, LCS, Флойд-Уоршелл.</summary>
internal static class TableAnimations
{
    // ------------------------------- умножение матриц ------------------------------- //

    public static Animation MatrixMultiply(Random rng)
    {
        const int n = 3, m = 4;
        var A = new int[n, m]; var B = new int[m, n];
        for (int i = 0; i < n; i++) for (int k = 0; k < m; k++) A[i, k] = rng.Next(1, 10);
        for (int k = 0; k < m; k++) for (int j = 0; j < n; j++) B[k, j] = rng.Next(1, 10);

        var b = new AnimationBuilder("Умножение матриц тремя циклами — O(n·m·p)");
        b.Legend = new[] { (Palette.AmberSoft, "текущие элементы"), (Palette.BlueSoft, "строка A / столбец B"), (Palette.GreenSoft, "готово") };
        var C = new long?[n, n];

        void Snap(string cap, int ci, int cj, int ck, long partial, string formula)
        {
            var cc = (long?[,])C.Clone();
            b.Add(cap, s =>
            {
                const double cs = 54;
                const double ay = 218, by = 190, cy0 = 218;
                double ax = 60, bx = ax + m * cs + 90, cx = bx + n * cs + 90;
                s.Text(ax + m * cs / 2, by - 24, "A", 18, Palette.Ink, TextAlign.Center, true);
                s.Text(bx + n * cs / 2, by - 24, "B", 18, Palette.Ink, TextAlign.Center, true);
                s.Text(cx + n * cs / 2, by - 24, "C = A·B", 18, Palette.Ink, TextAlign.Center, true);
                s.Text(ax + m * cs + 45, by + 2 * cs, "×", 30, Palette.Muted);
                s.Text(bx + n * cs + 45, by + 2 * cs, "=", 30, Palette.Muted);

                for (int i = 0; i < n; i++)
                    for (int k = 0; k < m; k++)
                    {
                        bool cur = i == ci && k == ck, row = i == ci && ci >= 0;
                        Draw.Cell(s, ax + k * cs, ay + i * cs, cs - 4, cs - 4, A[i, k].ToString(),
                            cur ? Palette.AmberSoft : row ? Palette.BlueSoft : Palette.White, cur ? Palette.Amber : null);
                    }
                for (int k = 0; k < m; k++)
                    for (int j = 0; j < n; j++)
                    {
                        bool cur = k == ck && j == cj, col = j == cj && cj >= 0;
                        Draw.Cell(s, bx + j * cs, by + k * cs, cs - 4, cs - 4, B[k, j].ToString(),
                            cur ? Palette.AmberSoft : col ? Palette.BlueSoft : Palette.White, cur ? Palette.Amber : null);
                    }
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        bool cur = i == ci && j == cj;
                        string text = cur && cc[i, j] is null ? partial.ToString() : cc[i, j]?.ToString() ?? "";
                        Rgba fill = cur ? Palette.AmberSoft : cc[i, j] is not null ? Palette.GreenSoft : Palette.Light;
                        Draw.Cell(s, cx + j * cs, cy0 + i * cs, cs - 4, cs - 4, text, fill, cur ? Palette.Amber : null);
                    }
                if (formula.Length > 0)
                    s.Text(60, 470, formula, 20, Palette.Ink, TextAlign.Left, true);
                s.Text(60, 520, "C[i][j] = Σ A[i][k]·B[k][j], k = 0..m−1  — три вложенных цикла i, j, k.", 14, Palette.Muted, TextAlign.Left);
            });
        }

        Snap("Каждый элемент C[i][j] — сумма попарных произведений строки i матрицы A и столбца j матрицы B.", -1, -1, -1, 0, "");
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                long total = 0; var terms = new List<string>();
                for (int k = 0; k < m; k++)
                {
                    long prod = A[i, k] * B[k, j];
                    total += prod;
                    terms.Add($"{A[i, k]}·{B[k, j]}");
                    Snap($"C[{i}][{j}]: k={k}, total += A[{i}][{k}]·B[{k}][{j}] = {A[i, k]}·{B[k, j]} = {prod}",
                        i, j, k, total, $"C[{i}][{j}] = {string.Join(" + ", terms)} = {total}");
                }
                C[i, j] = total;
                Snap($"Элемент C[{i}][{j}] = {total} готов.", i, j, -1, total, $"C[{i}][{j}] = {total}");
            }
        Snap($"Результат готов: {n}·{n}·{m} = {n * n * m} умножений.", -1, -1, -1, 0, "");
        return b.Build();
    }

    // ------------------------------- LCS ------------------------------- //

    public static Animation Lcs(Random rng)
    {
        const string alphabet = "ABCD";
        string a = new(Enumerable.Range(0, 7).Select(_ => alphabet[rng.Next(4)]).ToArray());
        string bb = new(Enumerable.Range(0, 6).Select(_ => alphabet[rng.Next(4)]).ToArray());
        int n = a.Length, m = bb.Length;
        var dp = new int?[n + 1, m + 1];
        for (int i = 0; i <= n; i++) dp[i, 0] = 0;
        for (int j = 0; j <= m; j++) dp[0, j] = 0;

        var b = new AnimationBuilder("Наибольшая общая подпоследовательность (LCS) — O(n·m)");
        b.Legend = new[] { (Palette.AmberSoft, "текущая клетка"), (Palette.GreenSoft, "совпадение"), (Palette.BlueSoft, "сверху / слева"), (Palette.PurpleSoft, "путь LCS") };

        void Snap(string cap, int ci, int cj, (int, int)[] related, Rgba relatedColor, HashSet<(int, int)> path, string lcs)
        {
            var d = (int?[,])dp.Clone();
            var rel = related.ToHashSet(); var pth = new HashSet<(int, int)>(path);
            b.Add(cap, s =>
            {
                const double cw = 62, ch = 46, x0 = 200, y0 = 120;
                s.Text(x0 - 40, y0 + ch / 2, "b →", 13, Palette.Muted, TextAlign.Right);
                s.Text(x0 + cw / 2, y0 - 12, "", 1, Palette.Muted);
                // шапки
                for (int j = 0; j <= m; j++)
                    Draw.Cell(s, x0 + (j + 1) * cw, y0, cw - 4, ch - 4, j == 0 ? "∅" : bb[j - 1].ToString(), Palette.Light, null, Palette.Ink, 16, true);
                for (int i = 0; i <= n; i++)
                    Draw.Cell(s, x0, y0 + (i + 1) * ch, cw - 4, ch - 4, i == 0 ? "∅" : a[i - 1].ToString(), Palette.Light, null, Palette.Ink, 16, true);
                s.Text(x0 + cw / 2, y0 - 14, "a ↓", 13, Palette.Muted);
                for (int i = 0; i <= n; i++)
                    for (int j = 0; j <= m; j++)
                    {
                        bool cur = i == ci && j == cj;
                        Rgba fill = pth.Contains((i, j)) ? Palette.PurpleSoft : cur ? Palette.AmberSoft : rel.Contains((i, j)) ? relatedColor
                            : d[i, j] is not null ? Palette.White : Palette.Light;
                        Rgba? st = pth.Contains((i, j)) ? Palette.Purple : cur ? Palette.Amber : null;
                        Draw.Cell(s, x0 + (j + 1) * cw, y0 + (i + 1) * ch, cw - 4, ch - 4, d[i, j]?.ToString() ?? "", fill, st);
                    }
                if (lcs.Length > 0)
                    s.Text(x0, 120 + (n + 2) * ch + 24, $"LCS = {lcs}   (длина {lcs.Length})", 22, Palette.Purple, TextAlign.Left, true);
            });
        }

        var none = Array.Empty<(int, int)>(); var noPath = new HashSet<(int, int)>();
        Snap($"Строки a = «{a}», b = «{bb}». Первая строка и первый столбец таблицы — нули (пустой префикс).", -1, -1, none, Palette.White, noPath, "");
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                if (a[i - 1] == bb[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                    Snap($"a[{i}]=b[{j}]=«{a[i - 1]}» совпали → dp = диагональ + 1 = {dp[i, j]}", i, j, new[] { (i - 1, j - 1) }, Palette.GreenSoft, noPath, "");
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j]!.Value, dp[i, j - 1]!.Value);
                    Snap($"«{a[i - 1]}» ≠ «{bb[j - 1]}» → dp = max(сверху {dp[i - 1, j]}, слева {dp[i, j - 1]}) = {dp[i, j]}", i, j, new[] { (i - 1, j), (i, j - 1) }, Palette.BlueSoft, noPath, "");
                }
            }

        // восстановление ответа
        var path = new HashSet<(int, int)> { (n, m) };
        string lcs = "";
        int pi = n, pj = m;
        Snap($"Таблица заполнена: длина LCS = {dp[n, m]}. Восстанавливаем саму подпоследовательность с конца.", pi, pj, none, Palette.White, new HashSet<(int, int)>(path), lcs);
        while (pi > 0 && pj > 0)
        {
            if (a[pi - 1] == bb[pj - 1])
            {
                lcs = a[pi - 1] + lcs; pi--; pj--;
                path.Add((pi, pj));
                Snap($"Символы совпали — «{a[pi]}» входит в LCS, идём по диагонали", pi, pj, none, Palette.White, new HashSet<(int, int)>(path), lcs);
            }
            else if (dp[pi - 1, pj] >= dp[pi, pj - 1])
            {
                pi--; path.Add((pi, pj));
                Snap("Значение пришло сверху — идём вверх", pi, pj, none, Palette.White, new HashSet<(int, int)>(path), lcs);
            }
            else
            {
                pj--; path.Add((pi, pj));
                Snap("Значение пришло слева — идём влево", pi, pj, none, Palette.White, new HashSet<(int, int)>(path), lcs);
            }
        }
        Snap(lcs.Length > 0 ? $"Готово: LCS = «{lcs}»." : "Готово: общих символов нет, LCS пуста.", -1, -1, none, Palette.White, new HashSet<(int, int)>(path), lcs);
        return b.Build();
    }

    // ------------------------------- Флойд–Уоршелл ------------------------------- //

    public static Animation FloydWarshall(Random rng)
    {
        const int n = 5;
        const double inf = double.PositiveInfinity;
        var d = new double[n, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) d[i, j] = i == j ? 0 : inf;
        // связный путь 1-2-3-4-5 + случайные хорды (граф неориентированный - так проще читать)
        for (int i = 0; i < n - 1; i++) { int w = rng.Next(1, 10); d[i, i + 1] = d[i + 1, i] = w; }
        for (int i = 0; i < n; i++)
            for (int j = i + 2; j < n; j++)
                if (rng.NextDouble() < 0.45) { int w = rng.Next(1, 10); d[i, j] = d[j, i] = w; }
        var original = (double[,])d.Clone();
        var changed = new bool[n, n];

        var b = new AnimationBuilder("Алгоритм Флойда–Уоршелла — O(n³)");

        Pt Node(int v)
        {
            double ang = -Math.PI / 2 + 2 * Math.PI * v / n;
            return new Pt(250 + 150 * Math.Cos(ang), 330 + 150 * Math.Sin(ang));
        }

        void Snap(string cap, int k, int ci, int cj, bool updated)
        {
            var dd = (double[,])d.Clone(); var ch = (bool[,])changed.Clone();
            b.Add(cap, s =>
            {
                // граф
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        if (!double.IsPositiveInfinity(original[i, j]))
                        {
                            var p = Node(i); var q = Node(j);
                            s.Line(p.X, p.Y, q.X, q.Y, Palette.Border, 2.5);
                            double mx = (p.X + q.X) / 2, my = (p.Y + q.Y) / 2;
                            s.Rect(mx - 11, my - 10, 22, 20, Palette.White, null, 0, 4);
                            s.Text(mx, my, Draw.Num(original[i, j]), 12, Palette.Muted);
                        }
                if (k >= 0 && ci >= 0)
                {
                    var pi = Node(ci); var pk = Node(k); var pj = Node(cj);
                    s.Line(pi.X, pi.Y, pk.X, pk.Y, Palette.Amber, 3, dashed: true);
                    s.Line(pk.X, pk.Y, pj.X, pj.Y, Palette.Amber, 3, dashed: true);
                }
                for (int v = 0; v < n; v++)
                {
                    var p = Node(v);
                    Rgba fill = v == k ? Palette.Purple : v == ci ? Palette.Blue : v == cj ? Palette.Green : Palette.BlueSoft;
                    bool strong = v == k || v == ci || v == cj;
                    s.Circle(p.X, p.Y, 24, fill, strong ? Palette.Ink : Palette.Blue, 2);
                    s.Text(p.X, p.Y, (v + 1).ToString(), 16, strong ? Palette.White : Palette.Ink, TextAlign.Center, true);
                }
                s.Text(250, 530, "● k — промежуточная   ● i — откуда   ● j — куда", 12, Palette.Muted);

                // матрица
                const double cw = 62, chh = 50, x0 = 560, y0 = 130;
                s.Text(x0 + cw + n * cw / 2, y0 - 22, "Матрица расстояний D", 15, Palette.Ink, TextAlign.Center, true);
                for (int j = 0; j < n; j++)
                    Draw.Cell(s, x0 + (j + 1) * cw, y0, cw - 4, chh - 4, (j + 1).ToString(), Palette.Light, null, Palette.Ink, 15, true);
                for (int i = 0; i < n; i++)
                {
                    Draw.Cell(s, x0, y0 + (i + 1) * chh, cw - 4, chh - 4, (i + 1).ToString(), Palette.Light, null, Palette.Ink, 15, true);
                    for (int j = 0; j < n; j++)
                    {
                        bool isIj = i == ci && j == cj;
                        bool isIk = k >= 0 && ci >= 0 && ((i == ci && j == k) || (i == k && j == cj));
                        Rgba fill = isIj ? (updated ? Palette.RedSoft : Palette.Light) : isIk ? Palette.AmberSoft : ch[i, j] ? Palette.GreenSoft : Palette.White;
                        Rgba? st = isIj ? (updated ? Palette.Red : Palette.Muted) : isIk ? Palette.Amber : null;
                        Draw.Cell(s, x0 + (j + 1) * cw, y0 + (i + 1) * chh, cw - 4, chh - 4, Draw.Inf(dd[i, j]), fill, st, null, 16, isIj);
                    }
                }
                s.Text(x0, y0 + (n + 1) * chh + 30, "зелёные — уже улучшенные расстояния", 12, Palette.Muted, TextAlign.Left);
            });
        }

        Snap("Начальная матрица D⁰: вес ребра, 0 на диагонали, ∞ — если ребра нет.", -1, -1, -1, false);
        for (int k = 0; k < n; k++)
        {
            Snap($"k = {k + 1}: проверяем, не короче ли путь i → {k + 1} → j, чем текущий i → j.", k, -1, -1, false);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (i == j || i == k || j == k) continue;
                    double via = d[i, k] + d[k, j];
                    string a1 = Draw.Inf(d[i, k]), a2 = Draw.Inf(d[k, j]), cur = Draw.Inf(d[i, j]);
                    if (via < d[i, j])
                    {
                        d[i, j] = via; changed[i, j] = true;
                        Snap($"D[{i + 1}][{j + 1}]: {a1} + {a2} = {Draw.Inf(via)} < {cur} → обновляем!", k, i, j, true);
                    }
                    else if (double.IsPositiveInfinity(via))
                        Snap($"D[{i + 1}][{j + 1}]: пути через {k + 1} нет ({a1} + {a2} = ∞) — без изменений", k, i, j, false);
                    else
                        Snap($"D[{i + 1}][{j + 1}]: {a1} + {a2} = {Draw.Inf(via)} ≥ {cur} — не лучше, оставляем", k, i, j, false);
                }
        }
        Snap("Готово: D[i][j] — длина кратчайшего пути между любой парой вершин.", -1, -1, -1, false);
        return b.Build();
    }
}
