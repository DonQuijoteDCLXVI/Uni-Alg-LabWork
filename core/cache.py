"""
core/cache.py

BenchmarkCache - класс, который:
  1. Проверяет, есть ли в БД уже `num_runs` сырых запусков для связки
     (algorithm, n). Если есть - берёт их и просто усредняет (не считая
     заново, это и есть "кэш").
  2. Если данных нет (или их меньше num_runs, или указан
     force_recalculate=True) - запускает переданную функцию num_runs раз,
     измеряет время (и, опционально, кол-во шагов), сохраняет каждый
     запуск в БД и возвращает усреднённый результат.

Функция, которую тестируем, должна соответствовать одному из двух
контрактов:

    func(n) -> Any
        Возвращается любой результат вычисления. В этом случае измеряется
        только ВРЕМЯ выполнения через time.perf_counter().

    func(n) -> (Any, int)
        Возвращается кортеж (результат, кол-во_шагов). Актуально для
        Части IV, где по условию считаются шаги, а не время. Время всё
        равно замеряется попутно и сохраняется - лишним не будет.

Это определяется автоматически по типу возвращаемого значения
(см. _run_single).
"""

import time
import sqlite3
import statistics
from dataclasses import dataclass
from typing import Callable, Any


@dataclass
class BenchmarkResult:
    """Агрегированный результат серии запусков для одного (algorithm, n[, m])."""
    algorithm: str
    n: int
    num_runs: int
    mean_time: float | None
    std_time: float | None
    mean_steps: float | None
    std_steps: float | None
    from_cache: bool  # True, если данные были взяты из БД, а не вычислены заново
    m: int | None = None  # используется только для Части II (матрицы)

    def __repr__(self) -> str:
        parts = [f"BenchmarkResult(algorithm={self.algorithm!r}, n={self.n}"]
        if self.mean_time is not None:
            parts.append(f"mean_time={self.mean_time:.6g}s")
        if self.mean_steps is not None:
            parts.append(f"mean_steps={self.mean_steps:.1f}")
        parts.append(f"from_cache={self.from_cache})")
        return ", ".join(parts)


class BenchmarkCache:
    """
    Обёртка над соединением SQLite, реализующая логику кэша замеров.

    Parameters
    ----------
    conn : sqlite3.Connection
        Открытое соединение с БД (см. db.database.connect_db).
    """

    def __init__(self, conn: sqlite3.Connection):
        self.conn = conn

    # ------------------------------------------------------------------ #
    # Публичный API
    # ------------------------------------------------------------------ #

    def get_or_compute(
        self,
        algorithm: str,
        n: int,
        func: Callable[[int], Any],
        m: int | None = None,
        num_runs: int = 5,
        force_recalculate: bool = False,
    ) -> BenchmarkResult:
        """
        Главный метод. Возвращает усреднённый результат замеров для
        (algorithm, n[, m]), используя кэш БД, если это возможно.

        Parameters
        ----------
        algorithm : str
            Название алгоритма, как оно должно храниться в БД
            (например, 'bubble_sort').
        n : int
            Размер входа для данного запуска.
        func : Callable[[int], Any]
            Функция ОДНОГО аргумента n, выполняющая замеряемый алгоритм
            (если алгоритму нужен и m, например умножение матриц -
            зафиксируйте m заранее через functools.partial). Может
            вернуть либо просто результат, либо (результат, шаги).
        m : int | None
            Второй параметр для Части II (умножение матриц). Для всех
            остальных алгоритмов оставляйте None.
        num_runs : int
            Сколько запусков усреднять (по заданию - 5).
        force_recalculate : bool
            Если True - игнорировать имеющиеся данные в БД и
            пересчитать заново (старые сырые записи для этой связки
            удаляются перед пересчётом, чтобы не копить дубликаты).

        Returns
        -------
        BenchmarkResult
        """
        if not force_recalculate:
            cached = self._load_from_db(algorithm, n, m, num_runs)
            if cached is not None:
                return cached

        if force_recalculate:
            self._delete_existing_runs(algorithm, n, m)

        return self._compute_and_store(algorithm, n, func, m, num_runs)

    def load_history(self, algorithm: str) -> list[BenchmarkResult]:
        """
        Возвращает усреднённые результаты по ВСЕМ n (и m), которые уже
        есть в БД для данного алгоритма - вне зависимости от того,
        сколько именно запусков сохранено под каждым n. Используется для
        режима "Сравнить алгоритмы": там важно показать всё, что уже
        накоплено в истории, а не требовать ровно num_runs записей.
        """
        rows = self.conn.execute(
            """
            SELECT n, m, time_seconds, steps
            FROM results
            WHERE algorithm = ?
            ORDER BY n ASC
            """,
            (algorithm,),
        ).fetchall()

        grouped: dict[tuple[int, int | None], dict[str, list[float]]] = {}
        for row in rows:
            key = (row["n"], row["m"])
            bucket = grouped.setdefault(key, {"times": [], "steps": []})
            if row["time_seconds"] is not None:
                bucket["times"].append(row["time_seconds"])
            if row["steps"] is not None:
                bucket["steps"].append(row["steps"])

        results: list[BenchmarkResult] = []
        for (n, m), bucket in sorted(grouped.items(), key=lambda kv: (kv[0][0], kv[0][1] or 0)):
            times, steps = bucket["times"], bucket["steps"]
            results.append(
                BenchmarkResult(
                    algorithm=algorithm,
                    n=n,
                    m=m,
                    num_runs=len(times) or len(steps),
                    mean_time=statistics.fmean(times) if times else None,
                    std_time=statistics.pstdev(times) if len(times) > 1 else (0.0 if times else None),
                    mean_steps=statistics.fmean(steps) if steps else None,
                    std_steps=statistics.pstdev(steps) if len(steps) > 1 else (0.0 if steps else None),
                    from_cache=True,
                )
            )
        return results

    # ------------------------------------------------------------------ #
    # Внутренняя механика
    # ------------------------------------------------------------------ #

    def _load_from_db(
        self, algorithm: str, n: int, m: int | None, num_runs: int
    ) -> BenchmarkResult | None:
        """
        Пытается собрать результат из уже существующих в БД записей.
        Возвращает None, если подходящих записей меньше, чем num_runs
        (значит, кэш "неполный" и нужно досчитать/пересчитать).
        """
        rows = self.conn.execute(
            """
            SELECT time_seconds, steps
            FROM results
            WHERE algorithm = ? AND n = ? AND m IS ?
            ORDER BY run_number ASC
            LIMIT ?
            """,
            (algorithm, n, m, num_runs),
        ).fetchall()

        if len(rows) < num_runs:
            return None  # кэш неполный - надо считать

        times = [r["time_seconds"] for r in rows if r["time_seconds"] is not None]
        steps = [r["steps"] for r in rows if r["steps"] is not None]

        return BenchmarkResult(
            algorithm=algorithm,
            n=n,
            m=m,
            num_runs=num_runs,
            mean_time=statistics.fmean(times) if times else None,
            std_time=statistics.pstdev(times) if len(times) > 1 else 0.0 if times else None,
            mean_steps=statistics.fmean(steps) if steps else None,
            std_steps=statistics.pstdev(steps) if len(steps) > 1 else 0.0 if steps else None,
            from_cache=True,
        )

    def _delete_existing_runs(self, algorithm: str, n: int, m: int | None) -> None:
        with self.conn:
            self.conn.execute(
                "DELETE FROM results WHERE algorithm = ? AND n = ? AND m IS ?",
                (algorithm, n, m),
            )

    def _compute_and_store(
        self,
        algorithm: str,
        n: int,
        func: Callable[[int], Any],
        m: int | None,
        num_runs: int,
    ) -> BenchmarkResult:
        times: list[float] = []
        steps_list: list[int] = []

        for run_number in range(1, num_runs + 1):
            elapsed, steps = self._run_single(func, n)
            times.append(elapsed)
            if steps is not None:
                steps_list.append(steps)

            with self.conn:
                self.conn.execute(
                    """
                    INSERT INTO results (algorithm, n, m, run_number, time_seconds, steps)
                    VALUES (?, ?, ?, ?, ?, ?)
                    """,
                    (algorithm, n, m, run_number, elapsed, steps),
                )

        return BenchmarkResult(
            algorithm=algorithm,
            n=n,
            m=m,
            num_runs=num_runs,
            mean_time=statistics.fmean(times) if times else None,
            std_time=statistics.pstdev(times) if len(times) > 1 else 0.0,
            mean_steps=statistics.fmean(steps_list) if steps_list else None,
            std_steps=statistics.pstdev(steps_list) if len(steps_list) > 1 else (0.0 if steps_list else None),
            from_cache=False,
        )

    @staticmethod
    def list_algorithms_with_data(conn: sqlite3.Connection) -> list[str]:
        """Возвращает отсортированный список алгоритмов, для которых в БД уже есть хоть какие-то данные."""
        rows = conn.execute(
            "SELECT DISTINCT algorithm FROM results ORDER BY algorithm ASC;"
        ).fetchall()
        return [row["algorithm"] for row in rows]

    @staticmethod
    def _run_single(func: Callable[[int], Any], n: int) -> tuple[float, int | None]:
        """
        Выполняет func(n) один раз, измеряя время через perf_counter.
        Если func вернула кортеж (result, steps) длиной 2, второй элемент
        трактуется как количество шагов и извлекается отдельно.
        """
        start = time.perf_counter()
        result = func(n)
        elapsed = time.perf_counter() - start

        steps = None
        if isinstance(result, tuple) and len(result) == 2:
            _, steps = result

        return elapsed, steps


if __name__ == "__main__":
    # Демонстрация работы кэша на игрушечном примере
    from db.database import connect_db, clear_all_results

    def dummy_bubble_like(n: int):
        """Имитация алгоритма: делает n^2 'шагов' и немного спит для теста."""
        steps = 0
        total = 0
        for i in range(n):
            for j in range(n):
                total += i * j
                steps += 1
        return total, steps

    conn = connect_db()
    clear_all_results(conn)  # чистый старт для демонстрации
    cache = BenchmarkCache(conn)

    print("Первый вызов (считает заново):")
    res1 = cache.get_or_compute("dummy_bubble_like", n=200, func=dummy_bubble_like)
    print(" ", res1)

    print("Второй вызов с теми же параметрами (должен взяться из кэша):")
    res2 = cache.get_or_compute("dummy_bubble_like", n=200, func=dummy_bubble_like)
    print(" ", res2)

    print("Вызов с force_recalculate=True (пересчитает заново):")
    res3 = cache.get_or_compute(
        "dummy_bubble_like", n=200, func=dummy_bubble_like, force_recalculate=True
    )
    print(" ", res3)

    conn.close()
