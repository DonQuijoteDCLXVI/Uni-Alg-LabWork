"""
algorithms/matrices.py

Часть II лабораторной: умножение матриц классическим методом (3 цикла).

Для 3D-графика время должно зависеть от ДВУХ параметров: n и m.
Берём A размера (n x m) и B размера (m x n), их произведение C = A @ B
имеет размер (n x n), а сложность умножения - O(n^2 * m):
n^2 элементов результата, каждый требует m умножений-сложений.
Так время замера действительно варьируется по обеим осям (n, m) отдельно.
"""

from __future__ import annotations

import random

Matrix = list[list[float]]


def generate_random_matrix(rows: int, cols: int, low: float = -10.0, high: float = 10.0) -> Matrix:
    """Генерирует матрицу rows x cols со случайными вещественными элементами."""
    return [[random.uniform(low, high) for _ in range(cols)] for _ in range(rows)]


def multiply_matrices(a: Matrix, b: Matrix) -> Matrix:
    """
    Классическое умножение матриц тремя вложенными циклами.

    Parameters
    ----------
    a : Matrix
        Матрица размера (n x m).
    b : Matrix
        Матрица размера (m x p). Число столбцов a должно равняться
        числу строк b.

    Returns
    -------
    Matrix
        Результирующая матрица размера (n x p).

    Raises
    ------
    ValueError
        Если размеры матриц несовместимы для умножения.

    Notes
    -----
    Теоретическая сложность: O(n * m * p) - три вложенных цикла,
    без каких-либо оптимизаций (не Штрассен и т.п.), как и требуется
    по заданию.
    """
    n = len(a)
    m = len(a[0]) if n > 0 else 0
    m_check = len(b)
    p = len(b[0]) if m_check > 0 else 0

    if m != m_check:
        raise ValueError(
            f"Несовместимые размеры: A имеет {n}x{m}, B имеет {m_check}x{p} "
            f"(число столбцов A должно равняться числу строк B)"
        )

    result: Matrix = [[0.0] * p for _ in range(n)]

    for i in range(n):
        for j in range(p):
            total = 0.0
            for k in range(m):
                total += a[i][k] * b[k][j]
            result[i][j] = total

    return result


def generate_and_multiply(n: int, m: int) -> Matrix:
    """
    Вспомогательная функция для замеров с двумя параметрами.

    Генерирует случайную матрицу A размера (n x m) и B размера (m x n),
    перемножает их и возвращает результат (n x n). Используется как
    целевая функция бенчмарка: время зависит и от n, и от m независимо,
    что и нужно для 3D-графика "время от n и m".

    Parameters
    ----------
    n : int
        Число строк A / число столбцов B (и размер результата n x n).
    m : int
        Число столбцов A / число строк B ("внутренняя" размерность,
        по которой идёт суммирование).
    """
    a = generate_random_matrix(n, m)
    b = generate_random_matrix(m, n)
    return multiply_matrices(a, b)


if __name__ == "__main__":
    # Самопроверка: сверяем результат с ручным вычислением на маленьком примере
    a_test: Matrix = [[1, 2, 3], [4, 5, 6]]        # 2x3
    b_test: Matrix = [[7, 8], [9, 10], [11, 12]]   # 3x2

    result = multiply_matrices(a_test, b_test)
    expected: Matrix = [
        [1 * 7 + 2 * 9 + 3 * 11, 1 * 8 + 2 * 10 + 3 * 12],
        [4 * 7 + 5 * 9 + 6 * 11, 4 * 8 + 5 * 10 + 6 * 12],
    ]
    assert result == expected, f"Ошибка умножения: {result} != {expected}"
    print(f"multiply_matrices: OK, результат = {result}")

    # Проверка несовместимых размеров
    try:
        multiply_matrices([[1, 2]], [[1, 2]])
        raise AssertionError("Должно было выброситься ValueError")
    except ValueError as e:
        print(f"Проверка несовместимых размеров: OK ({e})")

    # Проверка функции для 3D-бенчмарка
    c = generate_and_multiply(n=5, m=8)
    assert len(c) == 5 and len(c[0]) == 5
    print(f"generate_and_multiply(n=5, m=8): OK, размер результата {len(c)}x{len(c[0])}")
