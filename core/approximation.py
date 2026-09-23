"""
core/approximation.py

Аппроксимация эмпирических замеров теоретической моделью T(n) = C * f(n)
методом наименьших квадратов (scipy.optimize.curve_fit) + расчёт MSE.

Также даёт удобную функцию find_best_model, которая прогоняет несколько
кандидатов f(n) (n, n*log(n), n^2, n^3, log(n), const...) и находит тот,
что даёт наименьший MSE - это то, что реально нужно для раздела отчёта
"эмпирическая проверка теоретической сложности".
"""

from dataclasses import dataclass
from typing import Callable

import numpy as np
from scipy.optimize import curve_fit

# --------------------------------------------------------------------- #
# Библиотека типовых теоретических функций f(n) для сравнения сложности.
# Каждая принимает numpy-массив n и возвращает numpy-массив того же размера.
# Добавляем небольшой eps под логарифмом, чтобы не улетать в -inf при n=0/1.
# --------------------------------------------------------------------- #

_EPS = 1e-12

MODEL_FUNCTIONS: dict[str, Callable[[np.ndarray], np.ndarray]] = {
    "const":     lambda n: np.ones_like(n, dtype=float),
    "log_n":     lambda n: np.log2(n + _EPS),
    "n":         lambda n: n.astype(float),
    "n_log_n":   lambda n: n * np.log2(n + _EPS),
    "n^2":       lambda n: n.astype(float) ** 2,
    "n^3":       lambda n: n.astype(float) ** 3,
    "2^n":       lambda n: np.power(2.0, n.astype(float)),
}


@dataclass
class ApproximationResult:
    """Результат подбора константы C для одной модели f(n)."""
    model_name: str
    C: float
    mse: float
    predicted: np.ndarray  # T(n) = C * f(n) на тех же n, что и вход

    def __repr__(self) -> str:
        return (
            f"ApproximationResult(model={self.model_name!r}, "
            f"C={self.C:.6g}, MSE={self.mse:.6g})"
        )


def fit_model(
    n_values: list[float] | np.ndarray,
    t_values: list[float] | np.ndarray,
    model_name: str,
) -> ApproximationResult:
    """
    Подбирает константу C для модели T(n) = C * f(n) методом наименьших
    квадратов и считает MSE между эмпирическими t_values и предсказанными
    значениями.

    Parameters
    ----------
    n_values : array-like
        Массив размеров входа (x).
    t_values : array-like
        Массив эмпирических средних времён/шагов, соответствующих n_values (y).
    model_name : str
        Ключ из MODEL_FUNCTIONS ('n', 'n_log_n', 'n^2', 'n^3', 'log_n', 'const', '2^n').

    Returns
    -------
    ApproximationResult
    """
    if model_name not in MODEL_FUNCTIONS:
        raise ValueError(
            f"Неизвестная модель '{model_name}'. "
            f"Доступные: {list(MODEL_FUNCTIONS.keys())}"
        )

    n_arr = np.asarray(n_values, dtype=float)
    t_arr = np.asarray(t_values, dtype=float)

    if n_arr.shape != t_arr.shape:
        raise ValueError("n_values и t_values должны быть одинаковой длины")

    f = MODEL_FUNCTIONS[model_name]

    # Модель для curve_fit: T(n) = C * f(n), один свободный параметр C.
    def _model(n, C):
        return C * f(n)

    # Начальное приближение для C: грубая оценка через отношение средних.
    f_vals = f(n_arr)
    nonzero_mask = f_vals != 0
    if nonzero_mask.any():
        p0 = float(np.mean(t_arr[nonzero_mask] / f_vals[nonzero_mask]))
    else:
        p0 = 1.0

    try:
        popt, _ = curve_fit(_model, n_arr, t_arr, p0=[p0], maxfev=10000)
        C = float(popt[0])
    except RuntimeError:
        # curve_fit не сошёлся - fallback на аналитическое МНК-решение
        # для линейной по C модели: C = sum(f*t) / sum(f*f)
        denom = np.sum(f_vals ** 2)
        C = float(np.sum(f_vals * t_arr) / denom) if denom != 0 else 0.0

    predicted = C * f_vals
    mse = float(np.mean((t_arr - predicted) ** 2))

    return ApproximationResult(model_name=model_name, C=C, mse=mse, predicted=predicted)


def find_best_model(
    n_values: list[float] | np.ndarray,
    t_values: list[float] | np.ndarray,
    candidate_models: list[str] | None = None,
) -> list[ApproximationResult]:
    """
    Прогоняет fit_model для набора моделей-кандидатов и возвращает список
    результатов, отсортированный по возрастанию MSE (лучшая модель первая).

    Parameters
    ----------
    n_values, t_values : array-like
        Эмпирические данные (см. fit_model).
    candidate_models : list[str] | None
        Список моделей для проверки. По умолчанию - все из MODEL_FUNCTIONS.

    Returns
    -------
    list[ApproximationResult]
        Отсортировано по MSE по возрастанию (results[0] - лучшее приближение).
    """
    if candidate_models is None:
        candidate_models = list(MODEL_FUNCTIONS.keys())

    results = [fit_model(n_values, t_values, name) for name in candidate_models]
    results.sort(key=lambda r: r.mse)
    return results


if __name__ == "__main__":
    # Демонстрация: сгенерируем "эмпирические" данные под известным законом
    # T(n) = 3.0 * n^2 + небольшой шум, и проверим, что find_best_model
    # правильно опознает n^2 как лучшую модель.
    rng = np.random.default_rng(42)

    n_demo = np.array([10, 50, 100, 200, 400, 800, 1200])
    true_C = 3.0
    noise = rng.normal(0, 500, size=n_demo.shape)  # шум для реалистичности
    t_demo = true_C * n_demo.astype(float) ** 2 + noise

    print("Эмпирические данные (n, T(n)):")
    for n, t in zip(n_demo, t_demo):
        print(f"  n={n:5d}  T={t:12.2f}")

    print("\nПодбор моделей (отсортировано по MSE, лучшая первая):")
    ranked = find_best_model(n_demo, t_demo)
    for r in ranked:
        print(f"  {r}")

    print(f"\n=> Лучшая модель: {ranked[0].model_name} "
          f"(ожидалось: n^2, C~{true_C})")
