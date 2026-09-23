"""
core/benchmark.py

Связующий слой между "чистыми" реализациями алгоритмов (algorithms/*)
и кэшем замеров (core/cache.BenchmarkCache).

ALGORITHM_REGISTRY - единый источник правды о том, какие алгоритмы есть
в системе, к какой части задания относятся, какая у них метрика
(время / шаги) и какая теоретическая модель f(n) ожидается по умолчанию
для аппроксимации. GUI строит выпадающий список именно по этому реестру,
так что добавление нового алгоритма - это правка в одном месте.
"""

from __future__ import annotations

import functools
from dataclasses import dataclass
from typing import Callable, Any

from algorithms import vectors, matrices, power, custom
from core.cache import BenchmarkCache, BenchmarkResult


@dataclass(frozen=True)
class AlgorithmSpec:
    key: str                       # уникальный идентификатор, хранится в БД как algorithm
    display_name: str              # человекочитаемое имя для GUI
    part: str                      # 'I' | 'II' | 'III' | 'IV'
    metric: str                    # 'time' | 'steps'
    func: Callable[..., Any]       # func(n) -> Any, либо func(n, m) -> Any если needs_m
    needs_m: bool = False          # True только для умножения матриц (Часть II)
    default_model: str | None = None  # ключ из approximation.MODEL_FUNCTIONS для авто-подбора


# Фиксированное основание для степенных функций (Часть IV) - конкретное
# значение base не влияет на число шагов, важен только показатель степени n.
_POWER_BASE = 2.0

# x, в которой вычисляется полином (Часть I) - намеренно близко к 1, чтобы
# при naive-подсчёте x^i не было переполнения на больших степенях.
_POLYNOMIAL_X = 1.0001


ALGORITHM_REGISTRY: dict[str, AlgorithmSpec] = {
    spec.key: spec
    for spec in [
        # ---------------------------- Часть I: векторы ---------------------------- #
        AlgorithmSpec(
            key="vector_sum",
            display_name="I. Сумма вектора",
            part="I",
            metric="time",
            default_model="n",
            func=lambda n: vectors.vector_sum(vectors.generate_random_vector(n)),
        ),
        AlgorithmSpec(
            key="vector_product",
            display_name="I. Произведение вектора",
            part="I",
            metric="time",
            default_model="n",
            # значения близко к 1, чтобы произведение не переполнялось на больших n
            func=lambda n: vectors.vector_product(
                vectors.generate_random_vector(n, low=0.99, high=1.01)
            ),
        ),
        AlgorithmSpec(
            key="polynomial_naive",
            display_name="I. Полином (наивный)",
            part="I",
            metric="time",
            default_model="n^2",
            func=lambda n: vectors.polynomial_naive(
                vectors.generate_random_vector(n, low=-1, high=1), _POLYNOMIAL_X
            ),
        ),
        AlgorithmSpec(
            key="polynomial_horner",
            display_name="I. Полином (Горнер)",
            part="I",
            metric="time",
            default_model="n",
            func=lambda n: vectors.polynomial_horner(
                vectors.generate_random_vector(n, low=-1, high=1), _POLYNOMIAL_X
            ),
        ),
        AlgorithmSpec(
            key="bubble_sort",
            display_name="I. Bubble sort",
            part="I",
            metric="time",
            default_model="n^2",
            func=lambda n: vectors.bubble_sort(vectors.generate_random_vector(n)),
        ),
        AlgorithmSpec(
            key="quick_sort",
            display_name="I. Quick sort",
            part="I",
            metric="time",
            default_model="n_log_n",
            func=lambda n: vectors.quick_sort(vectors.generate_random_vector(n)),
        ),
        AlgorithmSpec(
            key="timsort",
            display_name="I. Timsort (sorted)",
            part="I",
            metric="time",
            default_model="n_log_n",
            func=lambda n: vectors.timsort_sort(vectors.generate_random_vector(n)),
        ),

        # ---------------------------- Часть II: матрицы ---------------------------- #
        AlgorithmSpec(
            key="matrix_multiply",
            display_name="II. Умножение матриц O(n^3)",
            part="II",
            metric="time",
            needs_m=True,
            default_model=None,  # для 3D-поверхности одномерная аппроксимация не строится
            func=lambda n, m: matrices.generate_and_multiply(n, m),
        ),

        # ------------------------- Часть III: свой алгоритм ------------------------ #
        AlgorithmSpec(
            key="custom_lcs",
            display_name="III. LCS (заглушка, заменить на выбранный вариант)",
            part="III",
            metric="time",
            default_model="n^2",
            func=lambda n: custom.lcs_random(n),
        ),

        # ---------------------------- Часть IV: степень ---------------------------- #
        AlgorithmSpec(
            key="power_naive",
            display_name="IV. Степень: наивная (итеративная)",
            part="IV",
            metric="steps",
            default_model="n",
            func=lambda n: power.power_naive(_POWER_BASE, n),
        ),
        AlgorithmSpec(
            key="power_recursive",
            display_name="IV. Степень: рекурсивная",
            part="IV",
            metric="steps",
            default_model="n",
            func=lambda n: power.power_recursive(_POWER_BASE, n),
        ),
        AlgorithmSpec(
            key="power_binary",
            display_name="IV. Степень: бинарная (быстрая)",
            part="IV",
            metric="steps",
            default_model="log_n",
            func=lambda n: power.power_binary(_POWER_BASE, n),
        ),
    ]
}


def get_spec(key: str) -> AlgorithmSpec:
    """Достаёт AlgorithmSpec по ключу, с понятной ошибкой если ключ не найден."""
    try:
        return ALGORITHM_REGISTRY[key]
    except KeyError:
        raise KeyError(
            f"Алгоритм '{key}' не зарегистрирован. "
            f"Доступные ключи: {list(ALGORITHM_REGISTRY.keys())}"
        ) from None


def run_experiment_1d(
    cache: BenchmarkCache,
    spec: AlgorithmSpec,
    n_values: list[int],
    num_runs: int = 5,
    force_recalculate: bool = False,
    m_value: int | None = None,
    progress_callback: Callable[[int, int], None] | None = None,
) -> list[BenchmarkResult]:
    """
    Прогоняет серию замеров для одного алгоритма по списку n (Части I, III, IV;
    и Часть II при фиксированном m_value - для случая "срез 3D-поверхности").

    Parameters
    ----------
    cache : BenchmarkCache
    spec : AlgorithmSpec
    n_values : list[int]
        Значения n, для которых нужно получить результат.
    num_runs, force_recalculate : см. BenchmarkCache.get_or_compute
    m_value : int | None
        Обязателен, если spec.needs_m; для остальных алгоритмов игнорируется.
    progress_callback : Callable[[int, int], None] | None
        Вызывается как progress_callback(сделано, всего) после каждого n -
        удобно прокинуть сюда pyqtSignal.emit из GUI-потока.

    Returns
    -------
    list[BenchmarkResult]
        В том же порядке, что и n_values.
    """
    if spec.needs_m and m_value is None:
        raise ValueError(f"Алгоритм '{spec.key}' требует указания m_value")

    func = functools.partial(spec.func, m=m_value) if spec.needs_m else spec.func

    results: list[BenchmarkResult] = []
    total = len(n_values)
    for i, n in enumerate(n_values, start=1):
        result = cache.get_or_compute(
            algorithm=spec.key,
            n=n,
            func=func,
            m=m_value if spec.needs_m else None,
            num_runs=num_runs,
            force_recalculate=force_recalculate,
        )
        results.append(result)
        if progress_callback is not None:
            progress_callback(i, total)

    return results


def run_experiment_2d_grid(
    cache: BenchmarkCache,
    spec: AlgorithmSpec,
    n_values: list[int],
    m_values: list[int],
    num_runs: int = 5,
    force_recalculate: bool = False,
    progress_callback: Callable[[int, int], None] | None = None,
) -> list[list[BenchmarkResult]]:
    """
    Специально для Части II: прогоняет замеры по ПОЛНОЙ сетке (n, m) -
    нужно для построения 3D-поверхности "время от n и m".

    Returns
    -------
    list[list[BenchmarkResult]]
        Внешний список - по n_values, внутренний - по m_values
        (results[i][j] соответствует n_values[i], m_values[j]).
    """
    if not spec.needs_m:
        raise ValueError(f"Алгоритм '{spec.key}' не поддерживает сетку (n, m)")

    grid: list[list[BenchmarkResult]] = []
    total = len(n_values) * len(m_values)
    done = 0

    for n in n_values:
        row: list[BenchmarkResult] = []
        for m in m_values:
            func = functools.partial(spec.func, m=m)
            result = cache.get_or_compute(
                algorithm=spec.key,
                n=n,
                func=func,
                m=m,
                num_runs=num_runs,
                force_recalculate=force_recalculate,
            )
            row.append(result)
            done += 1
            if progress_callback is not None:
                progress_callback(done, total)
        grid.append(row)

    return grid


def metric_values(results: list[BenchmarkResult], metric: str) -> list[float]:
    """Извлекает нужную метрику (mean_time или mean_steps) из списка результатов."""
    attr = "mean_time" if metric == "time" else "mean_steps"
    return [getattr(r, attr) for r in results]


if __name__ == "__main__":
    from db.database import connect_db, clear_all_results

    conn = connect_db()
    clear_all_results(conn)
    cache = BenchmarkCache(conn)

    spec = get_spec("quick_sort")
    n_values = [10, 50, 100, 200]

    print(f"Запуск эксперимента: {spec.display_name}")
    results = run_experiment_1d(
        cache, spec, n_values, num_runs=3,
        progress_callback=lambda done, total: print(f"  прогресс: {done}/{total}"),
    )
    for r in results:
        print(" ", r)

    print("\nПовторный запуск (должен взяться из кэша):")
    results_cached = run_experiment_1d(cache, spec, n_values, num_runs=3)
    assert all(r.from_cache for r in results_cached)
    print("  OK, все результаты из кэша")

    conn.close()
