"""
check_setup.py

Скрипт первичной диагностики. Запускать один раз после установки
зависимостей, ДО запуска main.py - проверяет, что:
  1. Все нужные библиотеки установлены и нужных версий.
  2. БД создаётся и миграция схемы отрабатывает.
  3. Кэш и раннер экспериментов работают (без GUI).
  4. PyQt6 действительно может создать QApplication в этом окружении.

Использование:
    python3 check_setup.py
"""

from __future__ import annotations

import sys


def _fail(message: str) -> None:
    print(f"[ОШИБКА] {message}")
    sys.exit(1)


def check_dependencies() -> None:
    print("1/4  Проверка зависимостей…")
    missing = []
    for module_name in ("PyQt6.QtWidgets", "matplotlib", "numpy", "scipy"):
        try:
            __import__(module_name)
        except ImportError:
            missing.append(module_name.split(".")[0])
    if missing:
        _fail(
            f"Не установлены пакеты: {', '.join(sorted(set(missing)))}. "
            f"Выполните: pip install -r requirements.txt"
        )
    print("      OK: PyQt6, matplotlib, numpy, scipy найдены")


def check_database() -> None:
    print("2/4  Проверка БД и миграции схемы…")
    from db.database import connect_db, clear_all_results, DEFAULT_DB_PATH

    conn = connect_db()
    clear_all_results(conn)
    columns = {row["name"] for row in conn.execute("PRAGMA table_info(results);")}
    conn.close()
    required = {"algorithm", "n", "m", "run_number", "time_seconds", "steps", "run_date"}
    if not required.issubset(columns):
        _fail(f"В таблице results не хватает колонок: {required - columns}")
    print(f"      OK: {DEFAULT_DB_PATH}")


def check_benchmark_pipeline() -> None:
    print("3/4  Проверка пайплайна кэш + алгоритмы (без GUI)…")
    from db.database import connect_db
    from core.cache import BenchmarkCache
    from core.benchmark import get_spec, run_experiment_1d, ALGORITHM_REGISTRY

    conn = connect_db()
    cache = BenchmarkCache(conn)

    spec = get_spec("vector_sum")
    results = run_experiment_1d(cache, spec, n_values=[10, 20], num_runs=2)
    if len(results) != 2 or any(r.mean_time is None for r in results):
        conn.close()
        _fail("run_experiment_1d вернул некорректные результаты")

    print(f"      OK: зарегистрировано алгоритмов - {len(ALGORITHM_REGISTRY)}")
    conn.close()


def check_qapplication() -> None:
    print("4/4  Проверка создания QApplication…")
    from PyQt6.QtWidgets import QApplication

    app = QApplication.instance() or QApplication(sys.argv)
    if app is None:
        _fail("Не удалось создать QApplication")
    print("      OK: QApplication создаётся без ошибок")


if __name__ == "__main__":
    print("=== Диагностика проекта перед запуском main.py ===\n")
    check_dependencies()
    check_database()
    check_benchmark_pipeline()
    check_qapplication()
    print("\nВсё готово. Можно запускать: python3 main.py")
