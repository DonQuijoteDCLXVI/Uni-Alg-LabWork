"""
gui/workers.py

QThread-воркеры, выполняющие тяжёлые вычисления (замеры алгоритмов) в
фоне, чтобы главный поток GUI не подвисал.

ВАЖНО про SQLite и потоки: соединение sqlite3.Connection, созданное в
одном потоке, по умолчанию нельзя использовать в другом. Поэтому воркер
получает не готовое соединение, а ТОЛЬКО db_path, и открывает своё
собственное соединение внутри run() - то есть уже в фоновом потоке.
"""

from __future__ import annotations

from PyQt6.QtCore import QThread, pyqtSignal

from db.database import connect_db
from core.cache import BenchmarkCache, BenchmarkResult
from core.benchmark import (
    AlgorithmSpec,
    run_experiment_1d,
    run_experiment_2d_grid,
)


class ExperimentWorker(QThread):
    """
    Запускает серию замеров для одного алгоритма в фоновом потоке.

    Signals
    -------
    progress(int, int)
        (сделано, всего) - для обновления QProgressBar.
    finished_ok(list, object)
        (results, spec) при успешном завершении. Для Части II (needs_m)
        results - это list[list[BenchmarkResult]] (сетка n x m), для
        остальных частей - плоский list[BenchmarkResult].
    failed(str)
        Текст ошибки, если во время вычислений что-то упало (например,
        некорректные параметры).
    """

    progress = pyqtSignal(int, int)
    finished_ok = pyqtSignal(object, object)
    failed = pyqtSignal(str)

    def __init__(
        self,
        db_path: str,
        spec: AlgorithmSpec,
        n_values: list[int],
        num_runs: int,
        force_recalculate: bool,
        m_values: list[int] | None = None,
        parent=None,
    ):
        super().__init__(parent)
        self.db_path = db_path
        self.spec = spec
        self.n_values = n_values
        self.num_runs = num_runs
        self.force_recalculate = force_recalculate
        self.m_values = m_values  # используется только если spec.needs_m

    def run(self) -> None:
        try:
            conn = connect_db(self.db_path)
            cache = BenchmarkCache(conn)

            if self.spec.needs_m:
                if not self.m_values:
                    raise ValueError("Для этого алгоритма нужно указать значения m")
                results = run_experiment_2d_grid(
                    cache,
                    self.spec,
                    self.n_values,
                    self.m_values,
                    num_runs=self.num_runs,
                    force_recalculate=self.force_recalculate,
                    progress_callback=lambda done, total: self.progress.emit(done, total),
                )
            else:
                results = run_experiment_1d(
                    cache,
                    self.spec,
                    self.n_values,
                    num_runs=self.num_runs,
                    force_recalculate=self.force_recalculate,
                    progress_callback=lambda done, total: self.progress.emit(done, total),
                )

            conn.close()
            self.finished_ok.emit(results, self.spec)

        except Exception as exc:  # noqa: BLE001 - в GUI-потоке хотим показать любую ошибку
            self.failed.emit(f"{type(exc).__name__}: {exc}")


class HistoryLoaderWorker(QThread):
    """
    Загружает историю замеров для НЕСКОЛЬКИХ алгоритмов из БД (для режима
    "Сравнить алгоритмы"). Чтение обычно быстрое, но всё равно выносим в
    поток, чтобы не блокировать GUI при большой истории.
    """

    finished_ok = pyqtSignal(dict)  # {algorithm_key: list[BenchmarkResult]}
    failed = pyqtSignal(str)

    def __init__(self, db_path: str, algorithm_keys: list[str], parent=None):
        super().__init__(parent)
        self.db_path = db_path
        self.algorithm_keys = algorithm_keys

    def run(self) -> None:
        try:
            conn = connect_db(self.db_path)
            cache = BenchmarkCache(conn)
            history: dict[str, list[BenchmarkResult]] = {
                key: cache.load_history(key) for key in self.algorithm_keys
            }
            conn.close()
            self.finished_ok.emit(history)
        except Exception as exc:  # noqa: BLE001
            self.failed.emit(f"{type(exc).__name__}: {exc}")
