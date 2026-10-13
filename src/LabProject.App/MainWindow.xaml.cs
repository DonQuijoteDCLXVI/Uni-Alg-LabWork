using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LabProject.Core;
using LabProject.Db;
using LabProject.Visual;
using LabProject.Visual.Animations;
using Microsoft.Win32;

namespace LabProject.App;

/// <summary>
/// Главное окно. Слева - панель параметров эксперимента (как в исходном
/// приложении), справа - две вкладки:
///   * "График"                 - Части I/III/IV: точки + аппроксимация с MSE,
///                                Часть II: вращаемая 3D-поверхность время(n, m),
///                                режим "Сравнить алгоритмы";
///   * "Визуализация алгоритма" - пошаговая анимация выбранного алгоритма.
/// </summary>
public partial class MainWindow : Window
{
    // Если сетка (n, m) для умножения матриц больше этого числа точек -
    // переспрашиваем пользователя (каждая точка - минимум numRuns запусков O(n^3)).
    private const int LargeGridWarningThreshold = 400;

    private readonly string _dbPath = SqliteResultStore.DefaultDbPath;
    private IResultStore OpenStore() => new SqliteResultStore(_dbPath);

    private bool _ready;
    private bool _busy;

    // параметры последнего запуска (для сопоставления результатов с n/m)
    private List<int> _lastN = new();
    private List<int>? _lastM;

    // состояние 3D-поверхности (чтобы её можно было вращать мышью)
    private sealed record SurfaceState(List<int> N, List<int> M, List<IReadOnlyList<double>> Grid, string Title);
    private SurfaceState? _surface;
    private double _yaw = SurfaceBuilder.DefaultYaw, _pitch = SurfaceBuilder.DefaultPitch;

    // состояние визуализации
    private Animation? _anim;
    private int _frame;
    private bool _updatingSlider;
    private readonly DispatcherTimer _timer = new();

    // плавное "рисование" графика / "вырастание" поверхности
    private readonly DispatcherTimer _revealTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Func<double, Scene>? _revealBuild;
    private long _revealStart;
    private const double RevealMs = 950;

    public MainWindow()
    {
        InitializeComponent();

        AlgoCombo.ItemsSource = AlgorithmRegistry.All;
        VizCombo.ItemsSource = AlgorithmRegistry.All.Where(s => AnimationCatalog.Has(s.Key)).ToList();

        PlotView.Dragged += PlotView_Dragged;
        _timer.Tick += Timer_Tick;
        _revealTimer.Tick += RevealTick;
        Closing += (_, _) => { _timer.Stop(); _revealTimer.Stop(); };

        _ready = true;
        ShowPlot(PlotBuilder.Placeholder());
        AlgoCombo.SelectedIndex = 0;   // вызовет AlgoCombo_SelectionChanged
        UpdateTimerInterval();
    }

    private AlgorithmSpec CurrentSpec() => (AlgorithmSpec)AlgoCombo.SelectedItem;

    // ------------------------------------------------------------------ //
    // Ввод чисел
    // ------------------------------------------------------------------ //

    private static readonly Dictionary<string, (int Min, int Max)> Limits = new()
    {
        ["NMaxBox"] = (2, 1_000_000),
        ["NStepBox"] = (1, 100_000),
        ["MMaxBox"] = (2, 100_000),
        ["MStepBox"] = (1, 10_000),
        ["RunsBox"] = (1, 50),
    };

    private static int ReadInt(TextBox box, int fallback)
    {
        var (min, max) = Limits[box.Name];
        if (!int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            v = fallback;
        v = Math.Clamp(v, min, max);
        box.Text = v.ToString(CultureInfo.InvariantCulture);
        return v;
    }

    private void IntBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var box = (TextBox)sender;
        ReadInt(box, Limits[box.Name].Min);
    }

    // ------------------------------------------------------------------ //
    // Смена алгоритма
    // ------------------------------------------------------------------ //

    private void AlgoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || AlgoCombo.SelectedItem is not AlgorithmSpec spec)
            return;

        // M_max / шаг M - только для умножения матриц
        MPanel.Visibility = spec.NeedsM ? Visibility.Visible : Visibility.Collapsed;

        // если на вкладке "График" лежит 3D-поверхность, а алгоритм сменили - вернуть заглушку
        if (spec.NeedsM && _surface is null)
            ShowPlot(SurfaceBuilder.Placeholder());
        else if (!spec.NeedsM && _surface is not null)
        {
            _surface = null;
            ShowPlot(PlotBuilder.Placeholder());
        }

        // синхронизируем выбор на вкладке визуализации
        if (VizCombo.Items.Cast<AlgorithmSpec>().Any(s => s.Key == spec.Key) && (string?)VizCombo.SelectedValue != spec.Key)
            VizCombo.SelectedValue = spec.Key;
    }

    // ------------------------------------------------------------------ //
    // Показ графика: мгновенно или с плавной анимацией появления
    // ------------------------------------------------------------------ //

    private void ShowPlot(Scene scene)
    {
        StopReveal();
        PlotView.Scene = scene;
    }

    /// <summary>build(progress) строит сцену для прогресса 0..1; кадры идут с плавным замедлением к концу.</summary>
    private void PlayReveal(Func<double, Scene> build)
    {
        StopReveal();
        _revealBuild = build;
        _revealStart = Stopwatch.GetTimestamp();
        PlotView.Scene = build(0);
        _revealTimer.Start();
    }

    private void StopReveal()
    {
        _revealTimer.Stop();
        _revealBuild = null;
    }

    private void RevealTick(object? sender, EventArgs e)
    {
        if (_revealBuild is null)
        {
            _revealTimer.Stop();
            return;
        }
        double t = Math.Min(1.0, Stopwatch.GetElapsedTime(_revealStart).TotalMilliseconds / RevealMs);
        double eased = 1 - Math.Pow(1 - t, 3);   // ease-out cubic
        PlotView.Scene = _revealBuild(eased);
        if (t >= 1.0)
            StopReveal();
    }

    private void SetStats(string model, string c, string mse, string points, string time, string cache)
    {
        StatModel.Text = model;
        StatC.Text = c;
        StatMse.Text = mse;
        StatPoints.Text = points;
        StatTime.Text = time;
        StatCache.Text = cache;
    }

    private static string CachePercent(IEnumerable<BenchmarkResult> results)
    {
        var list = results.ToList();
        if (list.Count == 0) return "—";
        return $"{100.0 * list.Count(r => r.FromCache) / list.Count:0}%";
    }

    // ------------------------------------------------------------------ //
    // Запуск эксперимента
    // ------------------------------------------------------------------ //

    private void SetBusy(bool busy)
    {
        _busy = busy;
        RunButton.IsEnabled = !busy;
        CompareButton.IsEnabled = !busy;
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        var spec = CurrentSpec();
        var nValues = RangeUtil.BuildRange(ReadInt(NMaxBox, 500), ReadInt(NStepBox, 20));
        int numRuns = ReadInt(RunsBox, 5);
        bool force = ForceCheck.IsChecked == true;

        List<int>? mValues = null;
        if (spec.NeedsM)
        {
            mValues = RangeUtil.BuildRange(ReadInt(MMaxBox, 100), ReadInt(MStepBox, 10));
            int gridSize = nValues.Count * mValues.Count;
            if (gridSize > LargeGridWarningThreshold)
            {
                var answer = MessageBox.Show(this,
                    $"Сетка (n, m) содержит {gridSize} точек, для каждой - {numRuns} запусков умножения " +
                    "матриц O(n³). Это может занять продолжительное время. Продолжить?",
                    "Большая сетка вычислений", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                    return;
            }
        }

        _lastN = nValues;
        _lastM = mValues;

        SetBusy(true);
        RunProgress.Value = 0;
        StatusText.Text = $"Выполняется: {spec.DisplayName}…";

        var progress = new Progress<(int Done, int Total)>(p =>
        {
            RunProgress.Maximum = Math.Max(p.Total, 1);
            RunProgress.Value = p.Done;
            StatusText.Text = $"Выполнено {p.Done} из {p.Total}…";
        });

        try
        {
            var outcome = await ExperimentService.RunAsync(OpenStore, spec, nValues, numRuns, force, mValues, progress);
            OnExperimentFinished(outcome);
        }
        catch (Exception ex)
        {
            SetBusy(false);
            StatusText.Text = "Ошибка при выполнении эксперимента.";
            MessageBox.Show(this, $"{ex.GetType().Name}: {ex.Message}", "Ошибка эксперимента",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnExperimentFinished(ExperimentOutcome outcome)
    {
        SetBusy(false);
        RunProgress.Value = RunProgress.Maximum;
        Tabs.SelectedIndex = 0;

        if (outcome.Spec.NeedsM)
            Render3D(outcome);
        else
            Render2D(outcome);

        StatusText.Text = $"Готово: {outcome.Spec.DisplayName}. " +
                          $"Общее время расчёта: {RangeUtil.FormatDuration(outcome.ElapsedSeconds)}.";
    }

    // ------------------------------------------------------------------ //
    // Отрисовка результатов
    // ------------------------------------------------------------------ //

    private void Render2D(ExperimentOutcome outcome)
    {
        var spec = outcome.Spec;
        _surface = null;

        var pairs = outcome.Series!
            .Select(r => (N: (double)r.N, V: spec.Metric == Metric.Time ? r.MeanTime : r.MeanSteps))
            .Where(p => p.V.HasValue)
            .ToList();
        var n = pairs.Select(p => p.N).ToList();
        var y = pairs.Select(p => p.V!.Value).ToList();

        if (n.Count == 0)
        {
            ShowPlot(PlotBuilder.Placeholder("Нет данных для построения графика"));
            SetStats("—", "—", "—", "0", RangeUtil.FormatDuration(outcome.ElapsedSeconds), CachePercent(outcome.Series!));
            return;
        }

        ApproximationResult? fit = null;
        if (spec.DefaultModel is not null && n.Count >= 2 && n.Count == outcome.Series!.Count)
            fit = Approximation.FitModel(n, y, spec.DefaultModel);

        string metricLabel = spec.MetricLabel, title = spec.DisplayName;
        PlayReveal(r => PlotBuilder.Single(n, y, metricLabel, title, fit, r));

        SetStats(
            fit?.ModelName ?? "—",
            fit is null ? "—" : PlotBuilder.G4(fit.C),
            fit is null ? "—" : PlotBuilder.G4(fit.Mse),
            n.Count.ToString(CultureInfo.InvariantCulture),
            RangeUtil.FormatDuration(outcome.ElapsedSeconds),
            CachePercent(outcome.Series!));
    }

    private void Render3D(ExperimentOutcome outcome)
    {
        var grid = outcome.Grid!
            .Select(row => (IReadOnlyList<double>)row.Select(c => c.MeanTime ?? 0.0).ToList())
            .ToList();

        _surface = new SurfaceState(_lastN, _lastM ?? new List<int>(), grid, outcome.Spec.DisplayName);

        var state = _surface;
        PlayReveal(r => SurfaceBuilder.Build(state.N, state.M, state.Grid, state.Title, _yaw, _pitch, grow: r));

        SetStats("поверхность", "—", "—",
            (state.N.Count * state.M.Count).ToString(CultureInfo.InvariantCulture),
            RangeUtil.FormatDuration(outcome.ElapsedSeconds),
            CachePercent(outcome.Grid!.SelectMany(row => row)));
    }

    private void RefreshSurface()
    {
        if (_surface is null)
            return;
        ShowPlot(SurfaceBuilder.Build(_surface.N, _surface.M, _surface.Grid, _surface.Title, _yaw, _pitch));
    }

    /// <summary>Вращение 3D-поверхности мышью.</summary>
    private void PlotView_Dragged(object? sender, Vector delta)
    {
        if (_surface is null)
            return;
        _yaw += delta.X * 0.01;
        _pitch = Math.Clamp(_pitch + delta.Y * 0.01, 0.08, 1.45);
        RefreshSurface();
    }

    // ------------------------------------------------------------------ //
    // Сравнение алгоритмов
    // ------------------------------------------------------------------ //

    private async void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        var current = CurrentSpec();
        try
        {
            // Сравниваем только алгоритмы с одинаковой метрикой и без параметра m
            var comparable = await ExperimentService.ListComparableAsync(OpenStore, current);

            var dialog = new CompareDialog(comparable, this);
            if (dialog.ShowDialog() != true || dialog.SelectedKeys.Count < 1)
                return;

            SetBusy(true);
            StatusText.Text = "Загрузка истории для сравнения…";

            var history = await ExperimentService.LoadHistoryAsync(OpenStore, dialog.SelectedKeys);
            SetBusy(false);

            var series = new List<PlotSeries>();
            foreach (var (key, results) in history)
            {
                var spec = AlgorithmRegistry.Get(key);
                var pairs = results
                    .Select(r => (N: (double)r.N, V: spec.Metric == Metric.Time ? r.MeanTime : r.MeanSteps))
                    .Where(p => p.V.HasValue).ToList();
                series.Add(new PlotSeries(spec.DisplayName, pairs.Select(p => p.N).ToList(), pairs.Select(p => p.V!.Value).ToList()));
            }

            _surface = null;
            Tabs.SelectedIndex = 0;
            ShowPlot(PlotBuilder.Multiple(series, current.MetricLabel, "Сравнение алгоритмов"));
            PlotView.FadeIn();
            SetStats("сравнение", "—", "—", series.Sum(s => s.N.Count).ToString(CultureInfo.InvariantCulture), "—", "100%");
            StatusText.Text = $"Сравнение построено: {series.Count} алгоритм(ов).";
        }
        catch (Exception ex)
        {
            SetBusy(false);
            StatusText.Text = "Ошибка при выполнении эксперимента.";
            MessageBox.Show(this, $"{ex.GetType().Name}: {ex.Message}", "Ошибка эксперимента",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ------------------------------------------------------------------ //
    // Сохранение изображения
    // ------------------------------------------------------------------ //

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var view = Tabs.SelectedIndex == 0 ? PlotView : VizView;
        if (view.Scene is null)
        {
            MessageBox.Show(this, "Пока нечего сохранять.", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Сохранить изображение",
            Filter = "Изображение PNG (*.png)|*.png|Векторное изображение SVG (*.svg)|*.svg",
            FileName = Tabs.SelectedIndex == 0 ? "graph" : "visualization",
        };
        if (dlg.ShowDialog(this) != true)
            return;

        try
        {
            if (Path.GetExtension(dlg.FileName).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(dlg.FileName, SceneSvg.ToSvg(view.Scene));
            else
                view.SavePng(dlg.FileName);
            StatusText.Text = $"Сохранено: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось сохранить", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ------------------------------------------------------------------ //
    // Вкладка "Визуализация алгоритма"
    // ------------------------------------------------------------------ //

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != Tabs || !_ready)
            return;
        (Tabs.SelectedIndex == 0 ? PlotView : VizView).FadeIn();
        if (Tabs.SelectedIndex == 1)
        {
            if (_anim is null)
                LoadAnimation();
        }
        else
        {
            StopPlayback();
        }
    }

    private void VizCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || VizCombo.SelectedValue is null)
            return;
        LoadAnimation();
    }

    private void NewDataButton_Click(object sender, RoutedEventArgs e) => LoadAnimation();

    private void LoadAnimation()
    {
        if (VizCombo.SelectedValue is not string key)
        {
            if (VizCombo.Items.Count == 0) return;
            VizCombo.SelectedIndex = 0;
            return; // SelectionChanged перезапустит LoadAnimation
        }

        StopPlayback();
        _anim = AnimationCatalog.Create(key, new Random());
        _updatingSlider = true;
        FrameSlider.Maximum = Math.Max(0, _anim.Count - 1);
        FrameSlider.Value = 0;
        _updatingSlider = false;
        ShowFrame(0);
    }

    private void ShowFrame(int index)
    {
        if (_anim is null)
            return;
        _frame = Math.Clamp(index, 0, _anim.Count - 1);
        VizView.Scene = _anim.Render(_frame);
        FrameLabel.Text = $"{_frame + 1} / {_anim.Count}";

        _updatingSlider = true;
        FrameSlider.Value = _frame;
        _updatingSlider = false;
    }

    private void FrameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _updatingSlider || _anim is null)
            return;
        StopPlayback();
        ShowFrame((int)Math.Round(FrameSlider.Value));
    }

    private void FirstButton_Click(object sender, RoutedEventArgs e) { StopPlayback(); ShowFrame(0); }
    private void PrevButton_Click(object sender, RoutedEventArgs e) { StopPlayback(); ShowFrame(_frame - 1); }
    private void NextButton_Click(object sender, RoutedEventArgs e) { StopPlayback(); ShowFrame(_frame + 1); }
    private void LastButton_Click(object sender, RoutedEventArgs e) { StopPlayback(); if (_anim is not null) ShowFrame(_anim.Count - 1); }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_anim is null)
            return;
        if (_timer.IsEnabled)
        {
            StopPlayback();
            return;
        }
        if (_frame >= _anim.Count - 1)
            ShowFrame(0);
        _timer.Start();
        PlayButton.Content = "⏸  Пауза";
    }

    private void StopPlayback()
    {
        _timer.Stop();
        PlayButton.Content = "▶  Пуск";
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_anim is null || _frame >= _anim.Count - 1)
        {
            StopPlayback();
            return;
        }
        ShowFrame(_frame + 1);
        if (_frame >= _anim.Count - 1)
            StopPlayback();
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_ready)
            UpdateTimerInterval();
    }

    private void UpdateTimerInterval()
    {
        // скорость 1..10 -> 1000..100 мс на кадр
        _timer.Interval = TimeSpan.FromMilliseconds(1100 - SpeedSlider.Value * 100);
    }
}
