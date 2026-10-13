namespace LabProject.Core;

/// <summary>Результат подбора константы C для одной модели f(n).</summary>
public sealed record ApproximationResult(string ModelName, double C, double Mse, double[] Predicted)
{
    public override string ToString() => $"ApproximationResult(model='{ModelName}', C={C:G6}, MSE={Mse:G6})";
}

/// <summary>
/// Аппроксимация эмпирических замеров теоретической моделью T(n) = C * f(n)
/// методом наименьших квадратов + расчёт MSE.
///
/// Модель линейна по единственному параметру C, поэтому МНК-решение
/// замкнуто: C = sum(f*t) / sum(f*f) (ровно то, к чему сходится
/// scipy.optimize.curve_fit в исходной версии).
/// </summary>
public static class Approximation
{
    private const double Eps = 1e-12;

    /// <summary>
    /// Библиотека типовых теоретических функций f(n). Ключи те же, что и в
    /// исходном проекте. Небольшой eps под логарифмом - чтобы не улетать
    /// в -inf при n = 0.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Func<double, double>> ModelFunctions =
        new Dictionary<string, Func<double, double>>
        {
            ["const"] = _ => 1.0,
            ["log_n"] = n => Math.Log2(n + Eps),
            ["n"] = n => n,
            ["n_log_n"] = n => n * Math.Log2(n + Eps),
            ["n^2"] = n => n * n,
            ["n^3"] = n => n * n * n,
            ["2^n"] = n => Math.Pow(2.0, n),
        };

    /// <summary>
    /// Подбирает константу C для модели T(n) = C * f(n) методом наименьших
    /// квадратов и считает MSE между эмпирическими значениями и предсказанными.
    /// </summary>
    public static ApproximationResult FitModel(
        IReadOnlyList<double> nValues, IReadOnlyList<double> tValues, string modelName)
    {
        if (!ModelFunctions.TryGetValue(modelName, out var f))
        {
            throw new ArgumentException(
                $"Неизвестная модель '{modelName}'. Доступные: {string.Join(", ", ModelFunctions.Keys)}");
        }
        if (nValues.Count != tValues.Count)
            throw new ArgumentException("n_values и t_values должны быть одинаковой длины");

        int count = nValues.Count;
        var fVals = new double[count];
        double num = 0.0, den = 0.0;
        for (int i = 0; i < count; i++)
        {
            fVals[i] = f(nValues[i]);
            num += fVals[i] * tValues[i];
            den += fVals[i] * fVals[i];
        }

        double c = den != 0.0 ? num / den : 0.0;

        var predicted = new double[count];
        double sqErr = 0.0;
        for (int i = 0; i < count; i++)
        {
            predicted[i] = c * fVals[i];
            double d = tValues[i] - predicted[i];
            sqErr += d * d;
        }
        double mse = count > 0 ? sqErr / count : 0.0;

        return new ApproximationResult(modelName, c, mse, predicted);
    }

    /// <summary>
    /// Прогоняет FitModel для набора моделей-кандидатов и возвращает результаты,
    /// отсортированные по возрастанию MSE (лучшая модель первая).
    /// </summary>
    public static List<ApproximationResult> FindBestModel(
        IReadOnlyList<double> nValues,
        IReadOnlyList<double> tValues,
        IEnumerable<string>? candidateModels = null)
    {
        var names = candidateModels ?? ModelFunctions.Keys;
        return names
            .Select(name => FitModel(nValues, tValues, name))
            .OrderBy(r => double.IsNaN(r.Mse) ? double.PositiveInfinity : r.Mse)
            .ToList();
    }
}
