using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class AdminSubcriptionPlanService
{
    private readonly AdminSubcriptionPlanRepository _adminSubcriptionPlanRepository;

    public AdminSubcriptionPlanService(AdminSubcriptionPlanRepository adminSubcriptionPlanRepository)
    {
        _adminSubcriptionPlanRepository = adminSubcriptionPlanRepository;
    }

    public async Task<AdminSubscriptionPlanListResponse> AdminViewSubscriptionPlanListAsync(
        string? search,
        string? targetRole,
        bool? isActive,
        int page,
        int pageSize)
    {
        var plans = await _adminSubcriptionPlanRepository.GetAdminPlansIncludingDeletedAsync();
        var normalizedTargetRole = string.IsNullOrWhiteSpace(targetRole)
            ? null
            : SubscriptionPlanMetadataHelper.NormalizeTargetRole(targetRole);
        var normalizedSearch = search?.Trim();

        var projected = plans
            .Select(plan => new
            {
                Plan = plan,
                PlanResponse = MapPlan(plan)
            })
            .Where(item =>
                string.IsNullOrWhiteSpace(normalizedSearch) ||
                item.Plan.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                item.PlanResponse.Code.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .Where(item =>
                string.IsNullOrWhiteSpace(normalizedTargetRole) ||
                string.Equals(item.PlanResponse.TargetRole, normalizedTargetRole, StringComparison.OrdinalIgnoreCase))
            .Where(item => !isActive.HasValue || item.Plan.IsActive == isActive.Value)
            .OrderBy(item => item.Plan.SortOrder)
            .ThenBy(item => item.Plan.Price)
            .ThenBy(item => item.Plan.Name)
            .ToList();

        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);
        var totalCount = projected.Count;
        var pagedItems = projected.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = new List<AdminSubscriptionPlanListItemResponse>(pagedItems.Count);
        foreach (var item in pagedItems)
        {
            items.Add(new AdminSubscriptionPlanListItemResponse
            {
                Id = item.Plan.Id,
                Code = item.PlanResponse.Code,
                TargetRole = item.PlanResponse.TargetRole,
                Name = item.Plan.Name,
                Price = item.Plan.Price,
                DurationDays = item.Plan.DurationDays,
                SortOrder = item.Plan.SortOrder,
                IsActive = item.Plan.IsActive,
                FeatureCount = item.PlanResponse.Features.Count,
                ActiveSubscriberCount = await _adminSubcriptionPlanRepository.CountActiveSubscriptionsByPlanAsync(
                    item.Plan.Id,
                    DateTime.UtcNow),
                CreatedAt = item.Plan.CreatedAt
            });
        }

        return new AdminSubscriptionPlanListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<AdminSubscriptionPlanDetailResponse?> AdminViewSubscriptionPlanDetailAsync(Guid id)
    {
        var plan = await _adminSubcriptionPlanRepository.FindAdminPlanByIdIncludingDeletedAsync(id);
        if (plan == null)
            return null;

        var planResponse = MapPlan(plan);
        return new AdminSubscriptionPlanDetailResponse
        {
            Id = plan.Id,
            Code = planResponse.Code,
            TargetRole = planResponse.TargetRole,
            Name = plan.Name,
            Description = plan.Description,
            Price = plan.Price,
            DurationDays = plan.DurationDays,
            Features = planResponse.Features,
            SortOrder = plan.SortOrder,
            Benefits = planResponse.Benefits,
            IsActive = plan.IsActive,
            ActiveSubscriberCount = await _adminSubcriptionPlanRepository.CountActiveSubscriptionsByPlanAsync(
                plan.Id,
                DateTime.UtcNow),
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt
        };
    }

    public async Task<AdminSubscriptionPlanDetailResponse> AdminCreateSubscriptionPlanAsync(
        Guid adminUserId,
        AdminSubscriptionPlanUpsertRequest request)
    {
        ValidateAdminPlanRequest(request);

        var normalizedName = request.Name.Trim();
        if (await _adminSubcriptionPlanRepository.ExistsPlanNameIncludingDeletedAsync(normalizedName))
            throw new InvalidOperationException("Ten goi subscription da ton tai.");

        var metadata = BuildAdminPlanMetadata(request, normalizedName);
        var nowUtc = DateTime.UtcNow;
        var nextSortOrder = await _adminSubcriptionPlanRepository.GetNextSubscriptionPlanSortOrderAsync();
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Price = request.Price,
            DurationDays = request.DurationDays,
            Features = SubscriptionPlanMetadataHelper.Serialize(metadata),
            IsActive = true,
            SortOrder = nextSortOrder,
            CreatedAt = nowUtc,
            CreatedBy = adminUserId,
            IsDeleted = false
        };

        _adminSubcriptionPlanRepository.AddPlan(plan);
        await _adminSubcriptionPlanRepository.SaveChangesAsync();

        return await AdminViewSubscriptionPlanDetailAsync(plan.Id)
            ?? throw new InvalidOperationException("Khong the tao goi subscription.");
    }

    public async Task<AdminSubscriptionPlanDetailResponse> AdminUpdateSubscriptionPlanAsync(
        Guid id,
        Guid adminUserId,
        AdminSubscriptionPlanUpsertRequest request)
    {
        ValidateAdminPlanRequest(request);

        var plan = await _adminSubcriptionPlanRepository.FindAdminPlanByIdIncludingDeletedAsync(id)
                   ?? throw new KeyNotFoundException("Khong tim thay goi subscription.");

        var normalizedName = request.Name.Trim();
        if (await _adminSubcriptionPlanRepository.ExistsPlanNameIncludingDeletedAsync(normalizedName, id))
            throw new InvalidOperationException("Ten goi subscription da ton tai.");

        var metadata = BuildAdminPlanMetadata(request, normalizedName);
        plan.Name = normalizedName;
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.Price = request.Price;
        plan.DurationDays = request.DurationDays;
        plan.SortOrder = request.SortOrder;
        plan.Features = SubscriptionPlanMetadataHelper.Serialize(metadata);
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = adminUserId;

        await _adminSubcriptionPlanRepository.SaveChangesAsync();

        return await AdminViewSubscriptionPlanDetailAsync(plan.Id)
            ?? throw new InvalidOperationException("Khong the cap nhat goi subscription.");
    }

    public async Task AdminUpdateSubscriptionPlanStatusAsync(Guid id, Guid adminUserId, bool isActive)
    {
        var plan = await _adminSubcriptionPlanRepository.FindAdminPlanByIdIncludingDeletedAsync(id)
                   ?? throw new KeyNotFoundException("Khong tim thay goi subscription.");

        var targetIsDeleted = !isActive;
        if (plan.IsActive == isActive && plan.IsDeleted == targetIsDeleted)
            return;

        plan.IsActive = isActive;
        plan.IsDeleted = targetIsDeleted;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = adminUserId;
        await _adminSubcriptionPlanRepository.SaveChangesAsync();
    }

    private static SubscriptionPlanMetadata BuildAdminPlanMetadata(AdminSubscriptionPlanUpsertRequest request, string normalizedName)
    {
        var targetRole = SubscriptionPlanMetadataHelper.NormalizeTargetRole(request.TargetRole);
        return new SubscriptionPlanMetadata
        {
            Code = SubscriptionPlanMetadataHelper.NormalizeCode(null, targetRole, normalizedName),
            TargetRole = targetRole,
            Features = SubscriptionPlanMetadataHelper.NormalizeFeatures(request.Features),
            Benefits = new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = request.Benefits.MonthlyJobPostLimit,
                MonthlyApplicationLimit = request.Benefits.MonthlyApplicationLimit,
                FeaturedBadge = request.Benefits.FeaturedBadge,
                SearchPriority = request.Benefits.SearchPriority,
                ListingDurationDays = request.Benefits.ListingDurationDays
            }
        };
    }

    private static void ValidateAdminPlanRequest(AdminSubscriptionPlanUpsertRequest request)
    {
        var context = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, validationResults, true);
        if (!isValid)
        {
            var message = validationResults.FirstOrDefault()?.ErrorMessage ?? "Du lieu khong hop le.";
            throw new InvalidOperationException(message);
        }

        request.Features = SubscriptionPlanMetadataHelper.NormalizeFeatures(request.Features);
        if (request.Features.Count == 0)
            throw new InvalidOperationException("Phai co it nhat 1 feature cho goi subscription.");
    }

    private static SubscriptionPlanResponse MapPlan(SubscriptionPlan plan)
    {
        var features = SplitFeatures(plan.Features);
        var targetRole = InferTargetRole(plan, features);
        return new SubscriptionPlanResponse
        {
            Id = plan.Id,
            Code = BuildPlanCode(plan),
            TargetRole = targetRole,
            Name = plan.Name,
            Description = plan.Description,
            Price = plan.Price,
            DurationDays = plan.DurationDays,
            Features = features,
            SortOrder = plan.SortOrder,
            Benefits = InferBenefits(plan, targetRole, features)
        };
    }

    private static List<string> SplitFeatures(string? features) =>
        string.IsNullOrWhiteSpace(features)
            ? []
            : TryParseJsonFeatures(features) ?? features
                .Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    private static List<string>? TryParseJsonFeatures(string features)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(features);
        }
        catch
        {
            return null;
        }
    }

    private static SubscriptionBenefitResponse InferBenefits(
        SubscriptionPlan plan,
        string targetRole,
        List<string> features)
    {
        var textSamples = new List<string> { plan.Name, plan.Description ?? string.Empty };
        textSamples.AddRange(features);

        var benefits = new SubscriptionBenefitResponse
        {
            FeaturedBadge = ContainsAny(textSamples, "badge", "featured", "noi bat"),
            SearchPriority = ContainsAny(textSamples, "uu tien", "priority", "tim kiem"),
            ListingDurationDays = InferListingDurationDays(textSamples, plan.DurationDays)
        };

        if (string.Equals(targetRole, "Parent", StringComparison.OrdinalIgnoreCase))
        {
            benefits.MonthlyJobPostLimit = InferNumericLimit(textSamples, "bai", "dang", "job");
            return benefits;
        }

        if (string.Equals(targetRole, "Nanny", StringComparison.OrdinalIgnoreCase))
        {
            benefits.MonthlyApplicationLimit = InferNumericLimit(textSamples, "ung tuyen", "apply", "cong viec");
            benefits.ListingDurationDays = 0;
            return benefits;
        }

        benefits.MonthlyJobPostLimit = InferNumericLimit(textSamples, "bai", "dang", "job");
        benefits.MonthlyApplicationLimit = InferNumericLimit(textSamples, "ung tuyen", "apply", "cong viec");
        return benefits;
    }

    private static string InferTargetRole(SubscriptionPlan plan, IEnumerable<string>? features = null)
    {
        var text = string.Join(' ', new[] { plan.Name, plan.Description ?? string.Empty, plan.Features ?? string.Empty }
            .Concat(features ?? []));
        var normalized = NormalizeText(text);

        if (ContainsAny(normalized, "nanny", "bao mau", "ung tuyen", "ho so", "candidate"))
            return "Nanny";

        if (ContainsAny(normalized, "parent", "phu huynh", "gia dinh", "bai dang", "dang tin", "job post"))
            return "Parent";

        return "Unknown";
    }

    private static string BuildPlanCode(SubscriptionPlan plan)
    {
        var source = string.IsNullOrWhiteSpace(plan.Name) ? plan.Id.ToString("N") : plan.Name;
        return NormalizeCode(source);
    }

    private static string NormalizeCode(string value)
    {
        var normalized = NormalizeText(value).ToUpperInvariant();
        var builder = new StringBuilder(normalized.Length);
        var lastUnderscore = false;

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastUnderscore = false;
                continue;
            }

            if (!lastUnderscore)
            {
                builder.Append('_');
                lastUnderscore = true;
            }
        }

        return builder.ToString().Trim('_');
    }

    private static int InferNumericLimit(IEnumerable<string> texts, params string[] keywords)
    {
        foreach (var text in texts.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var normalized = NormalizeText(text);
            if (!keywords.Any(keyword => normalized.Contains(NormalizeText(keyword), StringComparison.OrdinalIgnoreCase)))
                continue;

            var match = Regex.Match(normalized, @"\d+");
            if (match.Success && int.TryParse(match.Value, out var value))
                return value;
        }

        return 0;
    }

    private static int InferListingDurationDays(IEnumerable<string> texts, int fallbackDurationDays)
    {
        foreach (var text in texts.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var normalized = NormalizeText(text);
            if (!normalized.Contains("ngay", StringComparison.OrdinalIgnoreCase) &&
                !normalized.Contains("day", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = Regex.Match(normalized, @"\d+");
            if (match.Success && int.TryParse(match.Value, out var value))
                return value;
        }

        return Math.Max(0, fallbackDurationDays);
    }

    private static bool ContainsAny(IEnumerable<string> texts, params string[] keywords) =>
        texts.Any(text => ContainsAny(text, keywords));

    private static bool ContainsAny(string text, params string[] keywords)
    {
        var normalized = NormalizeText(text);
        return keywords.Any(keyword => normalized.Contains(NormalizeText(keyword), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
