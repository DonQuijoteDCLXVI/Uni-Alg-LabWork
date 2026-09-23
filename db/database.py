"""
db/database.py

Модуль отвечает за подключение к SQLite и создание схемы БД.

Таблица `results` хранит КАЖДЫЙ отдельный запуск (сырые данные), а не
усреднённые значения — это важно для лабораторной: усреднение,
дисперсию и т.д. мы считаем поверх сырых данных, ничего не теряя.

Поля:
    id            - PK
    algorithm     - название алгоритма ('bubble_sort', 'binary_power', ...)
    n             - размер входа (кол-во элементов вектора, степень и т.п.)
    m             - второй параметр (нужен ТОЛЬКО для Части II - умножения
                    матриц, где время зависит от n и m независимо; для всех
                    остальных алгоритмов остаётся NULL)
    run_number    - номер запуска в серии (1..5)
    time_seconds  - время выполнения в секундах (может быть NULL, если для
                    данного алгоритма меряются только шаги, см. Часть IV)
    steps         - количество шагов/операций (может быть NULL, если для
                    данного алгоритма меряется только время)
    run_date      - дата и время замера (для воспроизводимости/аудита)
"""

import sqlite3
from pathlib import Path
from contextlib import contextmanager

DEFAULT_DB_PATH = Path(__file__).resolve().parent.parent / "data" / "results.db"

CREATE_TABLE_SQL = """
CREATE TABLE IF NOT EXISTS results (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    algorithm    TEXT NOT NULL,
    n            INTEGER NOT NULL,
    m            INTEGER,
    run_number   INTEGER NOT NULL,
    time_seconds REAL,
    steps        INTEGER,
    run_date     TEXT NOT NULL DEFAULT (datetime('now'))
);
"""

# Индекс ускоряет ровно тот запрос, который чаще всего будет делать кэш:
# "дай мне все запуски для этого алгоритма и этой пары (n, m)".
CREATE_INDEX_SQL = """
CREATE INDEX IF NOT EXISTS idx_results_algo_n_m
ON results (algorithm, n, m);
"""


def connect_db(db_path: str | Path = DEFAULT_DB_PATH) -> sqlite3.Connection:
    """
    Открывает соединение с БД (файл будет создан, если его нет) и
    гарантирует, что нужные таблицы/индексы существуют.

    Parameters
    ----------
    db_path : str | Path
        Путь к файлу .db. По умолчанию - data/results.db внутри проекта.

    Returns
    -------
    sqlite3.Connection
        Открытое соединение с row_factory = sqlite3.Row (доступ к
        колонкам по имени, что удобнее, чем по индексу).
    """
    db_path = Path(db_path)
    db_path.parent.mkdir(parents=True, exist_ok=True)

    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row

    # Включаем поддержку внешних ключей на будущее (если таблиц станет больше)
    conn.execute("PRAGMA foreign_keys = ON;")

    create_tables(conn)
    return conn


def create_tables(conn: sqlite3.Connection) -> None:
    """Создаёт таблицу results и индекс, если их ещё нет."""
    with conn:
        conn.execute(CREATE_TABLE_SQL)
        conn.execute(CREATE_INDEX_SQL)
    _migrate_add_m_column_if_missing(conn)


def _migrate_add_m_column_if_missing(conn: sqlite3.Connection) -> None:
    """
    Лёгкая миграция: если results была создана до появления колонки m
    (например, из первой версии проекта), добавляет её. CREATE TABLE IF
    NOT EXISTS этого не делает - он не трогает уже существующую таблицу.
    """
    columns = {row["name"] for row in conn.execute("PRAGMA table_info(results);")}
    if "m" not in columns:
        with conn:
            conn.execute("ALTER TABLE results ADD COLUMN m INTEGER;")


@contextmanager
def get_connection(db_path: str | Path = DEFAULT_DB_PATH):
    """
    Контекстный менеджер для короткоживущих соединений:

        with get_connection() as conn:
            conn.execute(...)

    Автоматически закрывает соединение по выходу из блока.
    """
    conn = connect_db(db_path)
    try:
        yield conn
    finally:
        conn.close()


def clear_all_results(conn: sqlite3.Connection) -> None:
    """Полная очистка таблицы results. Полезно для тестов/перезапуска эксперимента."""
    with conn:
        conn.execute("DELETE FROM results;")


if __name__ == "__main__":
    # Простая самопроверка модуля
    conn = connect_db()
    cur = conn.execute(
        "SELECT name FROM sqlite_master WHERE type='table' AND name='results';"
    )
    print("Таблица results создана:", cur.fetchone() is not None)
    conn.close()
