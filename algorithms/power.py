"""
algorithms/power.py

Часть IV лабораторной: три алгоритма возведения в степень (base^n).

ВАЖНО: по заданию здесь измеряется не время, а КОЛИЧЕСТВО ШАГОВ
(умножений и рекурсивных вызовов). Каждая функция возвращает кортеж
(результат, количество_шагов) - счётчик "вшит" через замыкание и
nonlocal, чтобы не заводить глобальные переменные и не зависеть от
порядка вызовов (каждый вызов функции получает свой независимый счётчик).

Единый принцип подсчёта для всех трёх алгоритмов: счётчик увеличивается
на 1 за каждую операцию умножения И на 1 за каждый рекурсивный вызов
(для итеративного алгоритма рекурсивных вызовов нет, поэтому считаются
только умножения). Это делает метрики трёх алгоритмов сопоставимыми
между собой.
"""

from __future__ import annotations

import sys

# power_recursive должен корректно отрабатывать n вплоть до 1000 (см. задание,
# Часть IV), а это означает глубину рекурсии тоже около 1000+. Штатный лимит
# Python (обычно 1000) в этом случае не хватает "впритык" из-за служебных
# фреймов вокруг вызова (main -> ... -> helper). Поднимаем лимит один раз при
# импорте модуля, если он меньше требуемого - не понижаем, если уже больше.
_REQUIRED_RECURSION_LIMIT = 3000
if sys.getrecursionlimit() < _REQUIRED_RECURSION_LIMIT:
    sys.setrecursionlimit(_REQUIRED_RECURSION_LIMIT)


def power_naive(base: float, n: int) -> tuple[float, int]:
    """
    Наивное итеративное возведение в степень: result = base * base * ... (n раз).

    Теоретическая сложность: O(n) умножений.

    Returns
    -------
    tuple[float, int]
        (base ** n, количество_умножений)
    """
    if n < 0:
        raise ValueError("Функция рассчитана на неотрицательные степени n")

    result = 1.0
    steps = 0
    for _ in range(n):
        result *= base
        steps += 1
    return result, steps


def power_recursive(base: float, n: int) -> tuple[float, int]:
    """
    Рекурсивное возведение в степень по определению:
        base^n = base * base^(n-1),  base^0 = 1

    Теоретическая сложность: O(n) - и по умножениям, и по глубине/числу
    рекурсивных вызовов.

    Returns
    -------
    tuple[float, int]
        (base ** n, количество_шагов), где шаг = 1 рекурсивный вызов
        ИЛИ 1 умножение (считаются оба типа операций).
    """
    if n < 0:
        raise ValueError("Функция рассчитана на неотрицательные степени n")

    steps = 0

    def helper(exp: int) -> float:
        nonlocal steps
        steps += 1  # учитываем сам факт рекурсивного вызова
        if exp == 0:
            return 1.0
        value = base * helper(exp - 1)
        steps += 1  # учитываем умножение
        return value

    result = helper(n)
    return result, steps


def power_binary(base: float, n: int) -> tuple[float, int]:
    """
    Быстрое (бинарное) возведение в степень:
        base^n = (base^(n//2))^2,        если n чётное
        base^n = base * base^(n-1),      если n нечётное
        base^0 = 1

    Теоретическая сложность: O(log n) - и по умножениям, и по глубине
    рекурсии/числу вызовов.

    Returns
    -------
    tuple[float, int]
        (base ** n, количество_шагов), считаются рекурсивные вызовы и
        умножения по тому же принципу, что и в power_recursive - это
        и позволяет корректно сравнивать O(n) с O(log n) на одном графике.
    """
    if n < 0:
        raise ValueError("Функция рассчитана на неотрицательные степени n")

    steps = 0

    def helper(exp: int) -> float:
        nonlocal steps
        steps += 1  # учитываем сам факт рекурсивного вызова
        if exp == 0:
            return 1.0
        if exp % 2 == 0:
            half = helper(exp // 2)
            steps += 1  # умножение half * half
            return half * half
        else:
            value = base * helper(exp - 1)
            steps += 1  # умножение base * helper(...)
            return value

    result = helper(n)
    return result, steps


if __name__ == "__main__":
    # Самопроверка корректности результатов и разумности числа шагов
    import math

    test_cases = [(2.0, 0), (2.0, 1), (3.0, 10), (2.0, 1000), (1.5, 17)]

    for base, n in test_cases:
        expected = base ** n

        r_naive, s_naive = power_naive(base, n)
        r_rec, s_rec = power_recursive(base, n)
        r_bin, s_bin = power_binary(base, n)

        assert math.isclose(r_naive, expected, rel_tol=1e-6), f"naive: {r_naive} != {expected}"
        assert math.isclose(r_rec, expected, rel_tol=1e-6), f"recursive: {r_rec} != {expected}"
        assert math.isclose(r_bin, expected, rel_tol=1e-6), f"binary: {r_bin} != {expected}"

        print(
            f"base={base}, n={n:5d} | "
            f"naive: {s_naive:5d} шагов | "
            f"recursive: {s_rec:5d} шагов | "
            f"binary: {s_bin:3d} шагов"
        )

    print("\nВсе результаты совпадают с base ** n. "
          "Видно, что naive/recursive растут линейно, а binary - логарифмически.")
