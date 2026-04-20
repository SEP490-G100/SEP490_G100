using System.ComponentModel.DataAnnotations;

namespace WebSite.Validation;

public static class SalaryValidationRules
{
    public const decimal MinimumSalaryVnd = 8_000_000m;
    public const decimal MaximumSalaryVnd = 50_000_000m;
    public const string SalaryRangeText = "8.000.000 - 50.000.000 VND";

    public static IEnumerable<ValidationResult> Validate(
        decimal? minSalary,
        decimal? maxSalary,
        string minMemberName,
        string maxMemberName,
        string minLabel = "Mức lương tối thiểu",
        string maxLabel = "Mức lương tối đa")
    {
        if (minSalary.HasValue && !IsWithinAllowedRange(minSalary.Value))
        {
            yield return new ValidationResult(
                $"{minLabel} phải trong khoảng {SalaryRangeText}.",
                new[] { minMemberName });
        }

        if (maxSalary.HasValue && !IsWithinAllowedRange(maxSalary.Value))
        {
            yield return new ValidationResult(
                $"{maxLabel} phải trong khoảng {SalaryRangeText}.",
                new[] { maxMemberName });
        }

        if (minSalary.HasValue && maxSalary.HasValue && minSalary.Value > maxSalary.Value)
        {
            yield return new ValidationResult(
                $"{minLabel} không được lớn hơn {maxLabel}.",
                new[] { minMemberName, maxMemberName });
        }
    }

    public static bool IsWithinAllowedRange(decimal salary) =>
        salary >= MinimumSalaryVnd && salary <= MaximumSalaryVnd;
}
