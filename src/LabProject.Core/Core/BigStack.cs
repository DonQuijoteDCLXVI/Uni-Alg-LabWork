namespace LabProject.Core;

/// <summary>
/// Запуск вычислений в потоке с большим стеком.
///
/// Рекурсивные алгоритмы Части IV (степень) имеют глубину рекурсии порядка n,
/// а N_max в GUI может быть до 1 000 000. Штатный стек .NET-потока (1 МБ)
/// переполнился бы и УБИЛ бы процесс (StackOverflow в .NET не перехватывается),
/// поэтому все эксперименты выполняются в потоке со стеком 512 МБ
/// (это лишь резерв адресного пространства - физическая память не тратится,
/// пока стек не используется).
/// </summary>
public static class BigStack
{
    public const int StackSizeBytes = 512 * 1024 * 1024;

    public static Task<T> RunAsync<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(work());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, StackSizeBytes)
        {
            IsBackground = true,
            Name = "LabProject.BigStack",
        };
        thread.Start();
        return tcs.Task;
    }

    /// <summary>Синхронный вариант (для консольных проверок).</summary>
    public static T Run<T>(Func<T> work) => RunAsync(work).GetAwaiter().GetResult();
}
