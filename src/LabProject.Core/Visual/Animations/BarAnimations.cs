namespace LabProject.Visual.Animations;

/// <summary>Анимации на столбиковой диаграмме: сортировки, сумма и произведение вектора.</summary>
internal static class BarAnimations
{
    private static readonly Rgba Gray = Rgba.Hex("#E5E7EB");

    private static void DrawBars(Scene s, int[] values, Rgba[] colors, int max, string extra)
    {
        int n = values.Length;
        const double areaX = 50, areaW = 900, baseY = 540, areaH = 390;
        double slot = areaW / n, bw = slot * 0.72;
        for (int i = 0; i < n; i++)
        {
            double h = Math.Max(6, values[i] / (double)Math.Max(1, max) * areaH);
            double x = areaX + i * slot + (slot - bw) / 2;
            s.Rect(x, baseY - h, bw, h, colors[i], null, 0, 4);
            if (n <= 24)
                s.Text(x + bw / 2, baseY - h - 12, values[i].ToString(), 13, Palette.Ink);
            s.Text(x + bw / 2, baseY + 14, i.ToString(), 11, Palette.Muted);
        }
        s.Line(areaX - 5, baseY, areaX + areaW + 5, baseY, Palette.Border, 1.5);
        if (extra.Length > 0)
            s.Text(960, 112, extra, 24, Palette.Ink, TextAlign.Right, true);
    }

    private static Rgba[] Paint(int n, Func<int, Rgba> f)
    {
        var c = new Rgba[n];
        for (int i = 0; i < n; i++) c[i] = f(i);
        return c;
    }

    // ------------------------------- Bubble sort ------------------------------- //

    public static Animation BubbleSort(Random rng)
    {
        var a = Draw.RandomInts(rng, 14, 5, 99);
        int n = a.Length, max = 99;
        var b = new AnimationBuilder("Сортировка пузырьком — O(n²)");
        b.Legend = new[] { (Palette.Blue, "не обработан"), (Palette.Amber, "сравнение"), (Palette.Red, "обмен"), (Palette.Green, "на своём месте") };
        int sortedFrom = n;

        void Snap(string cap, int h1 = -1, int h2 = -1, Rgba? hc = null)
        {
            var vals = (int[])a.Clone();
            int sf = sortedFrom;
            var cols = Paint(n, i => i == h1 || i == h2 ? hc!.Value : i >= sf ? Palette.Green : Palette.Blue);
            b.Add(cap, s => DrawBars(s, vals, cols, max, ""));
        }

        Snap("Исходный массив. Сравниваем соседние элементы и «выталкиваем» больший вправо.");
        for (int i = 0; i < n; i++)
        {
            bool swapped = false;
            for (int j = 0; j < n - i - 1; j++)
            {
                Snap($"Сравниваем a[{j}]={a[j]} и a[{j + 1}]={a[j + 1]}", j, j + 1, Palette.Amber);
                if (a[j] > a[j + 1])
                {
                    (a[j], a[j + 1]) = (a[j + 1], a[j]);
                    swapped = true;
                    Snap($"Левый больше — меняем местами: a[{j}]={a[j]}, a[{j + 1}]={a[j + 1]}", j, j + 1, Palette.Red);
                }
            }
            sortedFrom = n - i - 1;
            if (!swapped)
            {
                sortedFrom = 0;
                Snap("За проход не было ни одного обмена — массив уже отсортирован (ранний выход).");
                break;
            }
            Snap($"Проход {i + 1} закончен: элемент на позиции {n - i - 1} занял своё место.");
        }
        sortedFrom = 0;
        Snap("Массив отсортирован.");
        return b.Build();
    }

    // ------------------------------- Quick sort ------------------------------- //

    public static Animation QuickSort(Random rng)
    {
        var a = Draw.RandomInts(rng, 14, 5, 99);
        int n = a.Length, max = 99;
        var sorted = new bool[n];
        var b = new AnimationBuilder("Быстрая сортировка (схема Ломуто) — O(n log n)");
        b.Legend = new[] { (Palette.Purple, "опорный"), (Palette.Amber, "сравнение"), (Palette.Red, "обмен"), (Palette.Green, "на своём месте"), (Gray, "вне подмассива") };

        void Snap(string cap, int lo, int hi, int pivot = -1, int cmp = -1, int sw1 = -1, int sw2 = -1)
        {
            var vals = (int[])a.Clone();
            var srt = (bool[])sorted.Clone();
            var cols = Paint(n, i =>
                i == sw1 || i == sw2 ? Palette.Red :
                i == pivot ? Palette.Purple :
                i == cmp ? Palette.Amber :
                srt[i] ? Palette.Green :
                (i < lo || i > hi) ? Gray : Palette.Blue);
            b.Add(cap, s => DrawBars(s, vals, cols, max, ""));
        }

        void Qs(int lo, int hi)
        {
            if (lo > hi) return;
            if (lo == hi) { sorted[lo] = true; return; }
            int pivot = a[hi];
            Snap($"Подмассив [{lo}..{hi}]: опорный элемент — последний, a[{hi}]={pivot}", lo, hi, pivot: hi);
            int i = lo - 1;
            for (int j = lo; j < hi; j++)
            {
                Snap($"Сравниваем a[{j}]={a[j]} с опорным {pivot}", lo, hi, hi, j);
                if (a[j] <= pivot)
                {
                    i++;
                    if (i != j)
                    {
                        (a[i], a[j]) = (a[j], a[i]);
                        Snap($"a[{j}] ≤ опорного — переносим в левую часть (обмен позиций {i} и {j})", lo, hi, hi, -1, i, j);
                    }
                }
            }
            (a[i + 1], a[hi]) = (a[hi], a[i + 1]);
            sorted[i + 1] = true;
            Snap($"Опорный элемент {pivot} ставим на позицию {i + 1} — она окончательная", lo, hi);
            Qs(lo, i);
            Qs(i + 2, hi);
        }

        Snap("Исходный массив. Выбираем опорный элемент и делим массив на «меньше» и «больше».", 0, n - 1);
        Qs(0, n - 1);
        Array.Fill(sorted, true);
        Snap("Массив отсортирован.", 0, n - 1);
        return b.Build();
    }

    // ------------------------------- Merge sort ------------------------------- //

    /// <summary>Визуализация встроенной сортировки: Timsort/introsort сложны для показа, поэтому
    /// показываем сортировку слиянием - она лежит в основе идеи Timsort.</summary>
    public static Animation MergeSort(Random rng)
    {
        var a = Draw.RandomInts(rng, 14, 5, 99);
        int n = a.Length, max = 99;
        var b = new AnimationBuilder("Сортировка слиянием (идея Timsort) — O(n log n)");
        b.Legend = new[] { (Palette.Blue, "сливаемый участок"), (Palette.Green, "записанный элемент"), (Gray, "вне участка") };

        void Snap(string cap, int lo, int hi, int written = -1)
        {
            var vals = (int[])a.Clone();
            var cols = Paint(n, i => i == written ? Palette.Green : (i >= lo && i <= hi) ? Palette.Blue : Gray);
            b.Add(cap, s => DrawBars(s, vals, cols, max, ""));
        }

        void Sort(int lo, int hi)
        {
            if (lo >= hi) return;
            int mid = (lo + hi) / 2;
            Sort(lo, mid);
            Sort(mid + 1, hi);
            var left = a[lo..(mid + 1)];
            var right = a[(mid + 1)..(hi + 1)];
            int i = 0, j = 0, k = lo;
            Snap($"Сливаем две отсортированные половины [{lo}..{mid}] и [{mid + 1}..{hi}]", lo, hi);
            while (i < left.Length || j < right.Length)
            {
                if (j >= right.Length || (i < left.Length && left[i] <= right[j])) a[k] = left[i++];
                else a[k] = right[j++];
                Snap($"Берём меньший из «голов» половин: {a[k]} → позиция {k}", lo, hi, k);
                k++;
            }
        }

        Snap("Исходный массив. Делим пополам, сортируем половины, затем сливаем.", 0, n - 1);
        Sort(0, n - 1);
        Snap("Массив отсортирован.", 0, n - 1);
        return b.Build();
    }

    // ------------------------------- Sum / Product ------------------------------- //

    public static Animation Reduce(Random rng, bool product)
    {
        int n = product ? 10 : 12;
        var a = product ? Draw.RandomInts(rng, n, 1, 3) : Draw.RandomInts(rng, n, 1, 9);
        int max = product ? 3 : 9;
        long acc = product ? 1 : 0;
        string sym = product ? "p" : "s", op = product ? "·" : "+";
        var b = new AnimationBuilder(product ? "Произведение элементов вектора — O(n)" : "Сумма элементов вектора — O(n)");
        b.Legend = new[] { (Palette.Green, "обработано"), (Palette.Amber, "текущий"), (Palette.Blue, "ожидает") };

        void Snap(string cap, int current, long accValue)
        {
            var vals = (int[])a.Clone();
            var cols = Paint(n, i => i < current ? Palette.Green : i == current ? Palette.Amber : Palette.Blue);
            string extra = $"{sym} = {accValue}";
            b.Add(cap, s => DrawBars(s, vals, cols, max, extra));
        }

        Snap(product ? "Начинаем с p = 1 и перемножаем элементы слева направо." : "Начинаем с s = 0 и прибавляем элементы слева направо.", -1, acc);
        for (int i = 0; i < n; i++)
        {
            long next = product ? acc * a[i] : acc + a[i];
            Snap($"{sym} = {sym} {op} a[{i}] = {acc} {op} {a[i]} = {next}", i, next);
            acc = next;
        }
        Snap($"Готово: один проход, n = {n} операций — линейная сложность.", n, acc);
        return b.Build();
    }
}
