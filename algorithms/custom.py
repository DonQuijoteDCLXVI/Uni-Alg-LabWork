"""
algorithms/custom.py

Часть III лабораторной: нетривиальный алгоритм.

ВРЕМЕННАЯ ЗАГЛУШКА: реализован вариант A (наибольшая общая
подпоследовательность, LCS, через динамическое программирование,
O(n^2)) на двух случайных строках длины n - просто чтобы GUI и весь
пайплайн работали end-to-end уже сейчас. Когда определитесь с финальным
вариантом (A/B/C), эта функция меняется, а вся остальная система
(кэш, GUI, аппроксимация) трогать не придётся - интерфейс func(n) -> Any
остаётся тем же.
"""

from __future__ import annotations

import random
import string


def _random_string(length: int) -> str:
    return "".join(random.choice(string.ascii_lowercase) for _ in range(length))


def lcs_length(a: str, b: str) -> int:
    """
    Длина наибольшей общей подпоследовательности (LCS) двух строк,
    классическое ДП. Теоретическая сложность: O(len(a) * len(b)).
    """
    n, m = len(a), len(b)
    # dp[i][j] - длина LCS для a[:i] и b[:j]
    dp = [[0] * (m + 1) for _ in range(n + 1)]
    for i in range(1, n + 1):
        for j in range(1, m + 1):
            if a[i - 1] == b[j - 1]:
                dp[i][j] = dp[i - 1][j - 1] + 1
            else:
                dp[i][j] = max(dp[i - 1][j], dp[i][j - 1])
    return dp[n][m]


def lcs_random(n: int) -> int:
    """
    Обёртка под интерфейс замеров func(n) -> Any: генерирует две
    случайные строки длины n и возвращает длину их LCS.
    """
    a = _random_string(n)
    b = _random_string(n)
    return lcs_length(a, b)


if __name__ == "__main__":
    assert lcs_length("ABCBDAB", "BDCABA") == 4  # классический учебный пример
    print("lcs_length: OK")
    print("lcs_random(50) =", lcs_random(50))
