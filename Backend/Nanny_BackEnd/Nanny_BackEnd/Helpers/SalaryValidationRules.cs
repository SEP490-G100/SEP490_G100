namespace Nanny_BackEnd.Helpers;

public static class SalaryValidationRules
{
    public const decimal MinimumSalaryVnd = 8_000_000m;
    public const decimal MaximumSalaryVnd = 50_000_000m;
    public const string SalaryRangeText = "8.000.000 - 50.000.000 VND";

    public static bool IsWithinAllowedRange(decimal salary) =>
        salary >= MinimumSalaryVnd && salary <= MaximumSalaryVnd;

    public static string? GetFirstError(
        decimal? minSalary,
        decimal? maxSalary,
        string minLabel = "Lương tối thiểu",
        string maxLabel = "Lương tối đa")
    {
        if (minSalary.HasValue && !IsWithinAllowedRange(minSalary.Value))
            return $"{minLabel} phải trong khoảng {SalaryRangeText}.";

        if (maxSalary.HasValue && !IsWithinAllowedRange(maxSalary.Value))
            return $"{maxLabel} phải trong khoảng {SalaryRangeText}.";

        if (minSalary.HasValue && maxSalary.HasValue && minSalary.Value > maxSalary.Value)
            return $"{minLabel} không được lớn hơn {maxLabel}.";

        return null;
    }
}
