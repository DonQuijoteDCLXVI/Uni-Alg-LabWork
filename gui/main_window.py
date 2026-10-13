"""
gui/main_window.py

Главное окно приложения.

Layout:
    +---------------------------+-------------------------------------+
    | Левая панель управления:  |  Правая область:                    |
    |  - выбор алгоритма        |  QStackedWidget с двумя страницами:  |
    |  - N_max, шаг N           |    - Plot2DCanvas (Части I, III, IV) |
    |  - M_max, шаг M (только   |    - Plot3DCanvas (Часть II)         |
    |    для умножения матриц)  |                                       |
    |  - кол-во запусков        |                                       |
    |  - force recalculate      |                                       |
    |  - кнопка "Запустить"     |                                       |
    |  - прогресс-бар           |                                       |
    |  - кнопка "Сравнить"      |                                       |
    +---------------------------+-------------------------------------+
"""

from __future__ import annotations

from PyQt6.QtWidgets import (
    QMainWindow, QWidget, QHBoxLayout, QVBoxLayout, QFormLayout,
    QComboBox, QSpinBox, QCheckBox, QPushButton, QProgressBar,
    QLabel, QStackedWidget, QMessageBox, QGroupBox,
)
from PyQt6.QtCore import Qt

from db.database import DEFAULT_DB_PATH, connect_db
from core.cache import BenchmarkCache, BenchmarkResult
from core.benchmark import ALGORITHM_REGISTRY, AlgorithmSpec, metric_values
from core.approximation import fit_model

from gui.workers import ExperimentWorker, HistoryLoaderWorker
from gui.plot_widgets import Plot2DCanvas, Plot3DCanvas
from gui.compare_dialog import CompareAlgorithmsDialog

# Если сетка (n, m) для умножения матриц получается больше этого числа
# точек - переспрашиваем пользователя, т.к. каждая точка - минимум
# 5 запусков O(n^3), и это может занять очень много времени.
_LARGE_GRID_WARNING_THRESHOLD = 400


def _build_range(max_value: int, step: int, start: int = 1) -> list[int]:
    """
    Строит список n от start до max_value с шагом step, гарантированно
    включая сам max_value последней точкой (даже если он не попадает
    ровно на шаг) - иначе график будет "обрезан" раньше, чем ожидает
    пользователь, указавший N_max.
    """
    step = max(1, step)
    start = max(1, start)
    if start > max_value:
        return [max_value]
    values = list(range(start, max_value + 1, step))
    if values[-1] != max_value:
        values.append(max_value)
    return values


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Эмпирический анализ временной сложности алгоритмов")
        self.resize(1200, 750)

        self.db_path = str(DEFAULT_DB_PATH)
        self.worker: ExperimentWorker | None = None
        self.history_worker: HistoryLoaderWorker | None = None

        # Запоминаем параметры последнего запуска - нужны в обработчике
        # finished_ok, чтобы правильно сопоставить результаты с n/m.
        self._last_n_values: list[int] = []
        self._last_m_values: list[int] | None = None

        self._build_ui()
        self._on_algorithm_changed()  # выставить видимость полей под первый алгоритм

    # ------------------------------------------------------------------ #
    # Построение интерфейса
    # ------------------------------------------------------------------ #

    def _build_ui(self) -> None:
        central = QWidget()
        self.setCentralWidget(central)
        root_layout = QHBoxLayout(central)

        root_layout.addWidget(self._build_controls_panel(), stretch=0)
        root_layout.addWidget(self._build_plot_panel(), stretch=1)

    def _build_controls_panel(self) -> QWidget:
        panel = QGroupBox("Параметры эксперимента")
        panel.setFixedWidth(320)
        form = QFormLayout(panel)

        # --- Выбор алгоритма --- #
        self.algo_combo = QComboBox()
        for spec in ALGORITHM_REGISTRY.values():
            self.algo_combo.addItem(spec.display_name, userData=spec.key)
        self.algo_combo.currentIndexChanged.connect(self._on_algorithm_changed)
        form.addRow("Алгоритм:", self.algo_combo)

        # --- N_max / шаг N --- #
        self.n_max_spin = QSpinBox()
        self.n_max_spin.setRange(2, 1_000_000)
        self.n_max_spin.setValue(500)
        form.addRow("N_max:", self.n_max_spin)

        self.n_step_spin = QSpinBox()
        self.n_step_spin.setRange(1, 100_000)
        self.n_step_spin.setValue(20)
        form.addRow("Шаг N:", self.n_step_spin)

        # --- M_max / шаг M (видны только для умножения матриц) --- #
        self.m_max_spin = QSpinBox()
        self.m_max_spin.setRange(2, 100_000)
        self.m_max_spin.setValue(100)
        self.m_max_label = QLabel("M_max:")
        form.addRow(self.m_max_label, self.m_max_spin)

        self.m_step_spin = QSpinBox()
        self.m_step_spin.setRange(1, 10_000)
        self.m_step_spin.setValue(10)
        self.m_step_label = QLabel("Шаг M:")
        form.addRow(self.m_step_label, self.m_step_spin)

        # --- Количество запусков --- #
        self.num_runs_spin = QSpinBox()
        self.num_runs_spin.setRange(1, 50)
        self.num_runs_spin.setValue(5)
        form.addRow("Кол-во запусков:", self.num_runs_spin)

        # --- Принудительный пересчёт --- #
        self.force_checkbox = QCheckBox("Принудительный пересчёт (игнорировать кэш)")
        form.addRow(self.force_checkbox)

        # --- Кнопка запуска + прогресс --- #
        self.run_button = QPushButton("Запустить эксперимент")
        self.run_button.clicked.connect(self._on_run_clicked)
        form.addRow(self.run_button)

        self.progress_bar = QProgressBar()
        self.progress_bar.setRange(0, 100)
        self.progress_bar.setValue(0)
        form.addRow(self.progress_bar)

        self.status_label = QLabel("Готов к запуску.")
        self.status_label.setWordWrap(True)
        form.addRow(self.status_label)

        # --- Сравнение алгоритмов --- #
        self.compare_button = QPushButton("Сравнить алгоритмы")
        self.compare_button.clicked.connect(self._on_compare_clicked)
        form.addRow(self.compare_button)

        return panel

    def _build_plot_panel(self) -> QWidget:
        self.stack = QStackedWidget()
        self.plot2d = Plot2DCanvas()
        self.plot3d = Plot3DCanvas()
        self.stack.addWidget(self.plot2d)
        self.stack.addWidget(self.plot3d)
        return self.stack

    # ------------------------------------------------------------------ #
    # Реакция на смену алгоритма
    # ------------------------------------------------------------------ #

    def _current_spec(self) -> AlgorithmSpec:
        key = self.algo_combo.currentData()
        return ALGORITHM_REGISTRY[key]

    def _on_algorithm_changed(self) -> None:
        spec = self._current_spec()
        needs_m = spec.needs_m

        self.m_max_spin.setVisible(needs_m)
        self.m_max_label.setVisible(needs_m)
        self.m_step_spin.setVisible(needs_m)
        self.m_step_label.setVisible(needs_m)

        self.stack.setCurrentWidget(self.plot3d if needs_m else self.plot2d)

    # ------------------------------------------------------------------ #
    # Запуск эксперимента
    # ------------------------------------------------------------------ #

    def _on_run_clicked(self) -> None:
        spec = self._current_spec()
        n_values = _build_range(self.n_max_spin.value(), self.n_step_spin.value())
        num_runs = self.num_runs_spin.value()
        force = self.force_checkbox.isChecked()

        m_values: list[int] | None = None
        if spec.needs_m:
            m_values = _build_range(self.m_max_spin.value(), self.m_step_spin.value())
            grid_size = len(n_values) * len(m_values)
            if grid_size > _LARGE_GRID_WARNING_THRESHOLD:
                answer = QMessageBox.question(
                    self,
                    "Большая сетка вычислений",
                    f"Сетка (n, m) содержит {grid_size} точек, для каждой - "
                    f"{num_runs} запусков умножения матриц O(n³). Это может "
                    f"занять продолжительное время. Продолжить?",
                )
                if answer != QMessageBox.StandardButton.Yes:
                    return

        self._last_n_values = n_values
        self._last_m_values = m_values

        self.run_button.setEnabled(False)
        self.compare_button.setEnabled(False)
        self.progress_bar.setValue(0)
        self.status_label.setText(f"Выполняется: {spec.display_name}…")

        self.worker = ExperimentWorker(
            db_path=self.db_path,
            spec=spec,
            n_values=n_values,
            num_runs=num_runs,
            force_recalculate=force,
            m_values=m_values,
        )
        self.worker.progress.connect(self._on_progress)
        self.worker.finished_ok.connect(self._on_experiment_finished)
        self.worker.failed.connect(self._on_experiment_failed)
        self.worker.start()

    def _on_progress(self, done: int, total: int) -> None:
        self.progress_bar.setMaximum(max(total, 1))
        self.progress_bar.setValue(done)
        self.status_label.setText(f"Выполнено {done} из {total}…")

    def _on_experiment_finished(self, results, spec: AlgorithmSpec) -> None:
        self.run_button.setEnabled(True)
        self.compare_button.setEnabled(True)
        self.progress_bar.setValue(self.progress_bar.maximum())

        if spec.needs_m:
            self._render_3d_results(results, spec)
        else:
            self._render_2d_results(results, spec)

        self.status_label.setText(f"Готово: {spec.display_name}.")

    def _on_experiment_failed(self, message: str) -> None:
        self.run_button.setEnabled(True)
        self.compare_button.setEnabled(True)
        self.status_label.setText("Ошибка при выполнении эксперимента.")
        QMessageBox.critical(self, "Ошибка эксперимента", message)

    # ------------------------------------------------------------------ #
    # Отрисовка результатов
    # ------------------------------------------------------------------ #

    def _render_2d_results(self, results: list[BenchmarkResult], spec: AlgorithmSpec) -> None:
        n_values = [r.n for r in results]
        values = metric_values(results, spec.metric)
        metric_label = "время, с" if spec.metric == "time" else "количество шагов"

        fit = None
        if spec.default_model is not None and len(n_values) >= 2 and all(v is not None for v in values):
            fit = fit_model(n_values, values, spec.default_model)

        self.stack.setCurrentWidget(self.plot2d)
        self.plot2d.plot_single(
            n_values=n_values,
            metric_values=values,
            metric_label=metric_label,
            title=spec.display_name,
            fit=fit,
        )

    def _render_3d_results(
        self, results: list[list[BenchmarkResult]], spec: AlgorithmSpec
    ) -> None:
        time_grid = [[cell.mean_time for cell in row] for row in results]
        self.stack.setCurrentWidget(self.plot3d)
        self.plot3d.plot_surface(
            n_values=self._last_n_values,
            m_values=self._last_m_values,
            time_grid=time_grid,
            title=spec.display_name,
        )

    # ------------------------------------------------------------------ #
    # Сравнение алгоритмов
    # ------------------------------------------------------------------ #

    def _on_compare_clicked(self) -> None:
        current_spec = self._current_spec()

        # Сравнивать имеет смысл только алгоритмы с одинаковой метрикой
        # (время со временем, шаги с шагами) и без второго параметра m
        # (умножение матриц сравнивается отдельно, через 3D-график).
        conn = connect_db(self.db_path)
        all_keys_with_data = BenchmarkCache.list_algorithms_with_data(conn)
        conn.close()

        comparable_keys = [
            key for key in all_keys_with_data
            if key in ALGORITHM_REGISTRY
            and not ALGORITHM_REGISTRY[key].needs_m
            and ALGORITHM_REGISTRY[key].metric == current_spec.metric
        ]

        dialog = CompareAlgorithmsDialog(comparable_keys, parent=self)
        if dialog.exec() != dialog.DialogCode.Accepted:
            return

        selected_keys = dialog.selected_keys()
        if len(selected_keys) < 1:
            return

        self.run_button.setEnabled(False)
        self.compare_button.setEnabled(False)
        self.status_label.setText("Загрузка истории для сравнения…")

        self._compare_metric = current_spec.metric  # запоминаем для обработчика

        self.history_worker = HistoryLoaderWorker(self.db_path, selected_keys)
        self.history_worker.finished_ok.connect(self._on_history_loaded)
        self.history_worker.failed.connect(self._on_experiment_failed)
        self.history_worker.start()

    def _on_history_loaded(self, history: dict[str, list[BenchmarkResult]]) -> None:
        self.run_button.setEnabled(True)
        self.compare_button.setEnabled(True)

        series: dict[str, tuple[list[float], list[float]]] = {}
        for key, results in history.items():
            spec = ALGORITHM_REGISTRY[key]
            n_values = [r.n for r in results]
            values = metric_values(results, spec.metric)
            series[spec.display_name] = (n_values, values)

        metric_label = "время, с" if self._compare_metric == "time" else "количество шагов"

        self.stack.setCurrentWidget(self.plot2d)
        self.plot2d.plot_multiple(series, metric_label, title="Сравнение алгоритмов")
        self.status_label.setText(f"Сравнение построено: {len(series)} алгоритм(ов).")
