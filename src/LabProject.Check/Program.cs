using System.Numerics;
using LabProject.Algorithms;
using LabProject.Core;
using LabProject.Db;
using LabProject.Visual;
using LabProject.Visual.Animations;

// Диагностика проекта (аналог check_setup.py). Запуск:
//     dotnet run --project src/LabProject.Check
// БД для проверки - ВРЕМЕННЫЙ файл, ваш data/results.db не затрагивается.

static void Fail(string message)
{
    Console.WriteLine($"[ОШИБКА] {message}");
    Environment.Exit(1);
}

static void Expect(bool condition, string what)
{
    if (!condition) Fail($"Не прошла проверка: {what}");
}

Console.WriteLine("=== Диагностика проекта ===\n");

// ---------------------------------------------------------------- 1/5
Console.WriteLine("1/5  Проверка SQLite и миграции схемы…");
string tempDb = Path.Combine(Path.GetTempPath(), $"labproject_check_{Guid.NewGuid():N}.db");
try
{
    using (var store = new SqliteResultStore(tempDb))
    {
        store.ClearAll();
        var columns = store.GetColumns();
        string[] required = { "algorithm", "n", "m", "run_number", "time_seconds", "steps", "run_date" };
        var missing = required.Where(c => !columns.Contains(c)).ToList();
        if (missing.Count > 0)
            Fail($"В таблице results не хватает колонок: {string.Join(", ", missing)}");
    }
    Console.WriteLine($"      OK: схема совпадает с исходным проектом (рабочая БД: {SqliteResultStore.DefaultDbPath})");

    // ------------------------------------------------------------ 2/5
    Console.WriteLine("2/5  Проверка кэша замеров поверх SQLite…");
    using (var store = new SqliteResultStore(tempDb))
    {
        var cache = new BenchmarkCache(store);
        var spec = AlgorithmRegistry.Get("vector_sum");
        var first = BenchmarkRunner.RunExperiment1D(cache, spec, new[] { 10, 20 }, numRuns: 2);
        Expect(first.Count == 2 && first.All(r => r.MeanTime.HasValue && !r.FromCache), "первый запуск считает заново");
        var second = BenchmarkRunner.RunExperiment1D(cache, spec, new[] { 10, 20 }, numRuns: 2);
        Expect(second.All(r => r.FromCache), "повторный запуск берётся из кэша");
        var forced = BenchmarkRunner.RunExperiment1D(cache, spec, new[] { 10, 20 }, numRuns: 2, forceRecalculate: true);
        Expect(forced.All(r => !r.FromCache), "принудительный пересчёт");

        var grid = BenchmarkRunner.RunExperiment2DGrid(cache, AlgorithmRegistry.Get("matrix_multiply"),
            new[] { 3, 6 }, new[] { 2, 4, 8 }, numRuns: 2);
        Expect(grid.Count == 2 && grid[0].Count == 3 && grid[1][2].M == 8, "сетка (n, m) для матриц (колонка m в БД)");

        var steps = BenchmarkRunner.RunExperiment1D(cache, AlgorithmRegistry.Get("power_binary"), new[] { 1000 }, numRuns: 2);
        Expect(steps[0].MeanSteps is > 0 and < 100, "степень: сохраняются шаги");
        Expect(cache.LoadHistory("vector_sum").Count == 2, "история для режима сравнения");
        Console.WriteLine($"      OK: зарегистрировано алгоритмов - {AlgorithmRegistry.All.Count}");
    }
}
finally
{
    foreach (var f in new[] { tempDb, tempDb + "-wal", tempDb + "-shm" })
        try { if (File.Exists(f)) File.Delete(f); } catch { /* не критично */ }
}

// ---------------------------------------------------------------- 3/5
Console.WriteLine("3/5  Проверка корректности алгоритмов…");
{
    var v = Vectors.GenerateRandomVector(300, -50, 50);
    var sorted = v.OrderBy(x => x).ToArray();
    Expect(Math.Abs(Vectors.VectorSum(v) - v.Sum()) < 1e-9, "сумма вектора");
    Expect(Vectors.BubbleSort(v).SequenceEqual(sorted) && Vectors.QuickSort(v).SequenceEqual(sorted)
           && Vectors.TimsortSort(v).SequenceEqual(sorted), "сортировки");
    var coeffs = new[] { 2.0, -3.0, 0.0, 1.5 };
    Expect(Math.Abs(Vectors.PolynomialNaive(coeffs, 2) - Vectors.PolynomialHorner(coeffs, 2)) < 1e-9, "полином: наивный = Горнер");

    var c = Matrices.Multiply(new[] { new[] { 1.0, 2, 3 }, new[] { 4.0, 5, 6 } },
                              new[] { new[] { 7.0, 8 }, new[] { 9.0, 10 }, new[] { 11.0, 12 } });
    Expect(c[0][0] == 58 && c[1][1] == 154, "умножение матриц");
    Expect(Custom.LcsLength("ABCBDAB", "BDCABA") == 4, "LCS");

    foreach (var (b, n) in new[] { (2.0, 0), (2.0, 1), (3.0, 10), (2.0, 1000), (1.5, 17) })
    {
        double e = Math.Pow(b, n);
        Expect(Math.Abs(Power.PowerNaive(b, n).Result - e) <= 1e-6 * e
               && Math.Abs(Power.PowerRecursive(b, n).Result - e) <= 1e-6 * e
               && Math.Abs(Power.PowerBinary(b, n).Result - e) <= 1e-6 * e, $"степень {b}^{n}");
    }
    Expect(BigStack.Run(() => Power.PowerRecursive(2.0, 1_000_000)).Steps == 2_000_001, "глубокая рекурсия (n = 10^6) в потоке с большим стеком");

    // --- новые алгоритмы ---
    {
        int n = 30; var w = Graphs.GenerateRandomGraph(n, 0.25, 20); var d = Graphs.FloydWarshall(w);
        bool ok = true;
        for (int s = 0; s < n && ok; s++)
        {
            var dist = Enumerable.Repeat(double.PositiveInfinity, n).ToArray(); dist[s] = 0;
            for (int it = 0; it < n; it++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        if (dist[i] + w[i][j] < dist[j]) dist[j] = dist[i] + w[i][j];
            for (int j = 0; j < n; j++) if (dist[j] != d[s][j]) ok = false;
        }
        Expect(ok, "Флойд-Уоршелл совпадает с Беллманом-Фордом");
    }
    foreach (int n in new[] { 1, 2, 3, 7, 8, 12, 31, 64, 100 })
    {
        var sig = Fft.GenerateRandomSignal(n);
        var f = Fft.Transform(sig); var ref_ = Fft.Dft(sig);
        double err = 0; for (int i = 0; i < n; i++) err = Math.Max(err, (f[i] - ref_[i]).Magnitude);
        Expect(err < 1e-8, $"БПФ длины {n} совпадает с ДПФ по определению (ошибка {err:E1})");
    }
    {
        var rng = new Random(1); bool ok = true;
        for (int t = 0; t < 1000 && ok; t++)
        {
            string text = new(Enumerable.Range(0, rng.Next(0, 50)).Select(_ => "ab"[rng.Next(2)]).ToArray());
            string pat = new(Enumerable.Range(0, rng.Next(1, 5)).Select(_ => "ab"[rng.Next(2)]).ToArray());
            var naive = new List<int>();
            for (int i = 0; i + pat.Length <= text.Length; i++)
                if (string.CompareOrdinal(text, i, pat, 0, pat.Length) == 0) naive.Add(i);
            ok = naive.SequenceEqual(StringSearch.KmpSearch(text, pat));
        }
        Expect(ok, "КМП совпадает с наивным поиском подстроки");
        Expect(StringSearch.KmpSearch("ABABDABACDABABCABAB", "ABABCABAB").SequenceEqual(new[] { 10 }), "КМП: классический пример");
    }
    Console.WriteLine("      OK: сортировки, матрицы, LCS, степень, Флойд-Уоршелл, БПФ, КМП");
}

// ---------------------------------------------------------------- 4/5
Console.WriteLine("4/5  Проверка аппроксимации…");
{
    var rng = new Random(42);
    var nn = new double[] { 10, 50, 100, 200, 400, 800, 1200 };
    var tt = nn.Select(n => 3.0 * n * n + (rng.NextDouble() - 0.5) * 1000).ToArray();
    var best = Approximation.FindBestModel(nn, tt)[0];
    Expect(best.ModelName == "n^2" && Math.Abs(best.C - 3) < 0.05, "подбор модели T(n) = C·f(n)");
    Console.WriteLine($"      OK: лучшая модель - {best.ModelName}, C ≈ {best.C:F3}");
}

// ---------------------------------------------------------------- 5/5
Console.WriteLine("5/5  Проверка графиков и визуализации алгоритмов…");
{
    int frames = 0;
    foreach (var spec in AlgorithmRegistry.All)
    {
        Expect(AnimationCatalog.Has(spec.Key), $"есть визуализация для '{spec.Key}'");
        var anim = AnimationCatalog.Create(spec.Key, new Random(1));
        for (int i = 0; i < anim.Count; i++)
        {
            Expect(anim.Render(i).Shapes.Count > 0, $"кадр {i} анимации '{spec.Key}' не пуст");
            frames++;
        }
    }
    var n2 = new double[] { 1, 21, 41, 61 };
    var y2 = n2.Select(x => 1e-9 * x * x).ToArray();
    Expect(PlotBuilder.Single(n2, y2, "время, с", "тест", Approximation.FitModel(n2, y2, "n^2")).Shapes.Count > 20, "2D-график");
    var nv = new[] { 1, 11, 21 }; var mv = new[] { 1, 6, 11 };
    var gr = nv.Select(a => (IReadOnlyList<double>)mv.Select(b => 1e-9 * a * a * b).ToList()).ToList();
    Expect(SurfaceBuilder.Build(nv, mv, gr, "тест").Shapes.Count > 20, "3D-поверхность");
    Console.WriteLine($"      OK: {AlgorithmRegistry.All.Count} анимаций, {frames} кадров, 2D и 3D графики строятся");
}

Console.WriteLine("\nВсё готово. Можно запускать: dotnet run --project src/LabProject.App");
