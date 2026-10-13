namespace LabProject.Db;

/// <summary>Одна сырая запись о запуске (строка таблицы results).</summary>
public sealed record RawRun(
    string Algorithm,
    int N,
    int? M,
    int RunNumber,
    double? TimeSeconds,
    long? Steps);

/// <summary>
/// Хранилище сырых замеров. Кэш (BenchmarkCache) работает поверх этого
/// интерфейса, поэтому его можно тестировать без реальной БД
/// (см. InMemoryResultStore в LabProject.Check).
/// </summary>
public interface IResultStore : IDisposable
{
    /// <summary>До <paramref name="limit"/> запусков для (algorithm, n, m), по возрастанию run_number.</summary>
    IReadOnlyList<RawRun> GetRuns(string algorithm, int n, int? m, int limit);

    /// <summary>Все запуски алгоритма, по возрастанию n.</summary>
    IReadOnlyList<RawRun> GetAllRuns(string algorithm);

    /// <summary>Вставляет пачку записей одной транзакцией.</summary>
    void InsertRuns(IReadOnlyList<RawRun> runs);

    /// <summary>Удаляет все запуски для (algorithm, n, m).</summary>
    void DeleteRuns(string algorithm, int n, int? m);

    /// <summary>Отсортированный список алгоритмов, по которым есть данные.</summary>
    IReadOnlyList<string> ListAlgorithms();

    /// <summary>Полная очистка таблицы results.</summary>
    void ClearAll();
}
