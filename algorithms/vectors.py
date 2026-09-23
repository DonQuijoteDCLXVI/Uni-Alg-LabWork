"""
algorithms/vectors.py

Часть I лабораторной: операции над векторами (списками чисел).

Все сортирующие функции НЕ мутируют исходный список (возвращают копию) -
это важно, чтобы при многократных замерах (5 прогонов) каждый прогон
получал одинаковый неотсортированный вход, а не результат предыдущего.
"""

from __future__ import annotations

import random


Vector = list[float]


# --------------------------------------------------------------------- #
# Вспомогательная генерация данных (пригодится при запуске замеров)
# --------------------------------------------------------------------- #

def generate_random_vector(n: int, low: float = -1000.0, high: float = 1000.0) -> Vector:
    """Генерирует вектор из n случайных вещественных чисел в [low, high]."""
    return [random.uniform(low, high) for _ in range(n)]


# --------------------------------------------------------------------- #
# Сумма и произведение - O(n)
# --------------------------------------------------------------------- #

def vector_sum(vector: Vector) -> float:
    """Сумма элементов вектора. Теоретическая сложность: O(n)."""
    total = 0.0
    for value in vector:
        total += value
    return total


def vector_product(vector: Vector) -> float:
    """Произведение элементов вектора. Теоретическая сложность: O(n)."""
    product = 1.0
    for value in vector:
        product *= value
    return product


# --------------------------------------------------------------------- #
# Вычисление полинома: coefficients[i] - коэффициент при x^i
# (coefficients[0] - свободный член, coefficients[-1] - при старшей степени)
# --------------------------------------------------------------------- #

def polynomial_naive(coefficients: Vector, x: float) -> float:
    """
    Наивное вычисление полинома: для каждого слагаемого степень x^i
    считается отдельным циклом умножений (не используется быстрое
    возведение в степень). Теоретическая сложность: O(n^2).
    """
    result = 0.0
    for i, coeff in enumerate(coefficients):
        power = 1.0
        for _ in range(i):
            power *= x
        result += coeff * power
    return result


def polynomial_horner(coefficients: Vector, x: float) -> float:
    """
    Вычисление полинома схемой Горнера:
        P(x) = (...((a_n * x + a_{n-1}) * x + a_{n-2}) * x + ... ) * x + a_0
    Теоретическая сложность: O(n).
    """
    result = 0.0
    for coeff in reversed(coefficients):
        result = result * x + coeff
    return result


# --------------------------------------------------------------------- #
# Сортировки
# --------------------------------------------------------------------- #

def bubble_sort(vector: Vector) -> Vector:
    """
    Сортировка пузырьком с ранним выходом (если за проход не было ни
    одного обмена - массив уже отсортирован).
    Теоретическая сложность: O(n^2) в среднем/худшем случае, O(n) в лучшем.
    """
    arr = vector.copy()
    n = len(arr)
    for i in range(n):
        swapped = False
        for j in range(n - i - 1):
            if arr[j] > arr[j + 1]:
                arr[j], arr[j + 1] = arr[j + 1], arr[j]
                swapped = True
        if not swapped:
            break
    return arr


def quick_sort(vector: Vector) -> Vector:
    """
    Быстрая сортировка (схема Ломуто, пивот - последний элемент),
    реализована через рекурсию по вспомогательному стеку индексов, чтобы
    не упираться в лимит рекурсии Python на больших n... на деле для
    учебных размеров n рекурсии достаточно.
    Теоретическая сложность: O(n log n) в среднем, O(n^2) в худшем случае.
    """
    arr = vector.copy()
    _quick_sort_inplace(arr, 0, len(arr) - 1)
    return arr


def _quick_sort_inplace(arr: Vector, low: int, high: int) -> None:
    while low < high:
        pivot_index = _partition(arr, low, high)
        # Рекурсируем в меньшую часть, а большую обрабатываем в цикле
        # (хвостовая оптимизация вручную, снижает глубину рекурсии до O(log n)).
        if pivot_index - low < high - pivot_index:
            _quick_sort_inplace(arr, low, pivot_index - 1)
            low = pivot_index + 1
        else:
            _quick_sort_inplace(arr, pivot_index + 1, high)
            high = pivot_index - 1


def _partition(arr: Vector, low: int, high: int) -> int:
    pivot = arr[high]
    i = low - 1
    for j in range(low, high):
        if arr[j] <= pivot:
            i += 1
            arr[i], arr[j] = arr[j], arr[i]
    arr[i + 1], arr[high] = arr[high], arr[i + 1]
    return i + 1


def timsort_sort(vector: Vector) -> Vector:
    """
    Обёртка над встроенной sorted() (Timsort) - добавлена для единообразия
    интерфейса при замерах (все сортировки имеют сигнатуру Vector -> Vector).
    Теоретическая сложность: O(n log n), O(n) на почти отсортированных данных.
    """
    return sorted(vector)


if __name__ == "__main__":
    # Самопроверка корректности всех функций на небольшом примере
    test_vector = generate_random_vector(20, -50, 50)
    coeffs = [2.0, -3.0, 0.0, 1.5]  # P(x) = 2 - 3x + 1.5x^3
    x0 = 2.0

    assert abs(vector_sum(test_vector) - sum(test_vector)) < 1e-9
    print("vector_sum: OK")

    import math
    expected_product = math.prod(test_vector)
    assert abs(vector_product(test_vector) - expected_product) < 1e-6 * abs(expected_product)
    print("vector_product: OK")

    expected_poly = sum(c * x0 ** i for i, c in enumerate(coeffs))
    assert abs(polynomial_naive(coeffs, x0) - expected_poly) < 1e-9
    assert abs(polynomial_horner(coeffs, x0) - expected_poly) < 1e-9
    print(f"polynomial_naive / polynomial_horner: OK (P({x0}) = {expected_poly})")

    reference = sorted(test_vector)
    assert bubble_sort(test_vector) == reference
    assert quick_sort(test_vector) == reference
    assert timsort_sort(test_vector) == reference
    assert test_vector != reference or len(set(test_vector)) <= 1  # исходный вектор не тронут
    print("bubble_sort / quick_sort / timsort_sort: OK, исходный вектор не мутирован")
