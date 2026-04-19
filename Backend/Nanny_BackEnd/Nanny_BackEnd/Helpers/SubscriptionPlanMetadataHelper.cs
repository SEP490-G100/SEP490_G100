using System.Text;
using System.Text.Json;
using Nanny_BackEnd.DTOs.Subscription;

namespace Nanny_BackEnd.Helpers;

public class SubscriptionPlanMetadata
{
    public string Code { get; set; } = "";
    public string TargetRole { get; set; } = "";
    public List<string> Features { get; set; } = [];
    public SubscriptionBenefitResponse Benefits { get; set; } = new();
}

public static class SubscriptionPlanMetadataHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static SubscriptionPlanMetadata? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var metadata = JsonSerializer.Deserialize<SubscriptionPlanMetadata>(raw, JsonOptions);
            if (metadata != null &&
                (!string.IsNullOrWhiteSpace(metadata.TargetRole) ||
                 !string.IsNullOrWhiteSpace(metadata.Code) ||
                 metadata.Features.Count > 0))
            {
                metadata.Features = NormalizeFeatures(metadata.Features);
                return metadata;
            }
        }
        catch
        {
        }

        try
        {
            var features = JsonSerializer.Deserialize<List<string>>(raw, JsonOptions);
            if (features != null)
            {
                return new SubscriptionPlanMetadata
                {
                    Features = NormalizeFeatures(features)
                };
            }
        }
        catch
        {
        }

        var plainFeatures = raw
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return plainFeatures.Count == 0
            ? null
            : new SubscriptionPlanMetadata { Features = NormalizeFeatures(plainFeatures) };
    }

    public static string Serialize(SubscriptionPlanMetadata metadata)
    {
        metadata.Features = NormalizeFeatures(metadata.Features);
        metadata.Code = NormalizeCode(metadata.Code, metadata.TargetRole, null);
        metadata.TargetRole = NormalizeTargetRole(metadata.TargetRole);
        return JsonSerializer.Serialize(metadata, JsonOptions);
    }

    public static string NormalizeTargetRole(string? targetRole) =>
        string.Equals(targetRole, "Nanny", StringComparison.OrdinalIgnoreCase) ? "Nanny" :
        string.Equals(targetRole, "Parent", StringComparison.OrdinalIgnoreCase) ? "Parent" :
        "Unknown";

    public static string NormalizeCode(string? code, string? targetRole, string? fallbackName)
    {
        var seed = string.IsNullOrWhiteSpace(code) ? fallbackName ?? "" : code;
        var builder = new StringBuilder(seed.Length);
        var lastWasSeparator = false;

        foreach (var ch in seed.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSeparator = false;
                continue;
            }

            if (lastWasSeparator || builder.Length == 0)
                continue;

            builder.Append('_');
            lastWasSeparator = true;
        }

        var normalized = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "PLAN";

        var role = NormalizeTargetRole(targetRole);
        if (role == "Nanny" && !normalized.StartsWith("NANNY_", StringComparison.OrdinalIgnoreCase))
            normalized = $"NANNY_{normalized}";

        return normalized;
    }

    public static List<string> NormalizeFeatures(IEnumerable<string>? features) =>
        (features ?? [])
            .Select(feature => feature?.Trim())
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
}
