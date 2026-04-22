using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class NannyService : INannyService
{
    private readonly INannyProfileRepository _nannyProfileRepository;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly INotificationService _notificationService;

    public NannyService(
        INannyProfileRepository nannyProfileRepository,
        IFavoriteRepository favoriteRepository,
        INotificationService notificationService)
    {
        _nannyProfileRepository = nannyProfileRepository;
        _favoriteRepository = favoriteRepository;
        _notificationService = notificationService;
    }

    public async Task<NannyPagedResultResponse> GetListAsync(NannyListRequest request, Guid? currentParentProfileId = null)
    {
        var parsedSkillIds = ParseSkillIds(request.SkillIds);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 12 : Math.Min(request.PageSize, 50);
        var (items, totalCount) = await _nannyProfileRepository.SearchAsync(request, parsedSkillIds);
        var favoriteNannyIds = currentParentProfileId.HasValue
            ? await _favoriteRepository.getFavoriteNannyIds(currentParentProfileId.Value, items.Select(item => item.Id))
            : [];

        return new NannyPagedResultResponse
        {
            Items = items.Select(item => MapToListResponse(item, favoriteNannyIds)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<NannyDetailResponse> GetDetailAsync(Guid nannyProfileId, Guid? currentParentProfileId = null)
    {
        var nanny = await _nannyProfileRepository.GetDetailAsync(nannyProfileId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ bảo mẫu.");

        var isFavorite = currentParentProfileId.HasValue &&
                         await _favoriteRepository.isFavoriteNanny(currentParentProfileId.Value, nannyProfileId);

        return MapToDetailResponse(nanny, isFavorite);
    }

    public async Task<NannyPagedResultResponse> GetFavoritesAsync(Guid parentProfileId, int page = 1, int pageSize = 12)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 12 : Math.Min(pageSize, 50);

        var (items, totalCount) = await _favoriteRepository.getFavoriteNannies(parentProfileId, page, pageSize);
        var favoriteNannyIds = items.Select(item => item.Id).ToHashSet();

        return new NannyPagedResultResponse
        {
            Items = items.Select(item => MapToListResponse(item, favoriteNannyIds)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool IsFavorite, Guid NannyUserId)> ToggleFavoriteAsync(Guid parentProfileId, Guid nannyProfileId, Guid actorUserId)
    {
        var nanny = await _nannyProfileRepository.GetDetailAsync(nannyProfileId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ bảo mẫu.");

        if (nanny.IsDeleted || nanny.User.IsDeleted)
            throw new InvalidOperationException("Hồ sơ bảo mẫu không hợp lệ.");

        var isFavorite = await _favoriteRepository.toggleFavoriteNanny(parentProfileId, nannyProfileId, actorUserId);
        if (isFavorite)
        {
            await _notificationService.createNotification(
                nanny.UserId,
                "Hồ sơ của bạn vừa được yêu thích",
                "Có một phụ huynh vừa tìm hồ sơ của bạn.",
                NotificationTypes.NannyProfileFavorited,
                nannyProfileId,
                "NannyProfile",
                actorUserId);
        }

        return (isFavorite, nanny.UserId);
    }

    private static List<Guid> ParseSkillIds(string? skillIds)
    {
        if (string.IsNullOrWhiteSpace(skillIds))
            return [];

        return skillIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static NannyListItemResponse MapToListResponse(NannyProfile nanny, HashSet<Guid>? favoriteNannyIds = null)
    {
        var entitlement = getNannyEntitlement(nanny);
        var educationLevel = nanny.EducationLevel.HasValue && Enum.IsDefined(typeof(EducationLevel), nanny.EducationLevel.Value)
            ? (EducationLevel?)nanny.EducationLevel.Value
            : null;
        var verificationStatus = Enum.IsDefined(typeof(VerificationStatus), nanny.VerificationStatus)
            ? (VerificationStatus)nanny.VerificationStatus
            : VerificationStatus.NotSubmitted;

        return new NannyListItemResponse
        {
            Id = nanny.Id,
            UserId = nanny.UserId,
            IsFavorite = favoriteNannyIds?.Contains(nanny.Id) == true,
            FullName = $"{nanny.User.FirstName} {nanny.User.LastName}".Trim(),
            AvatarUrl = nanny.User.AvatarUrl,
            Bio = nanny.Bio,
            City = nanny.User.City,
            District = nanny.User.District,
            Age = CalculateAge(nanny.User.DateOfBirth),
            YearsOfExperience = nanny.YearsOfExperience,
            ExpectedSalaryMin = nanny.ExpectedSalaryMin,
            ExpectedSalaryMax = nanny.ExpectedSalaryMax,
            AverageRating = nanny.AverageRating,
            TotalReviews = nanny.TotalReviews,
            SubscriptionPlanCode = entitlement.PlanCode,
            FeaturedBadge = entitlement.Benefits.FeaturedBadge,
            SearchPriority = entitlement.Benefits.SearchPriority,
            VerificationStatus = nanny.VerificationStatus,
            VerificationStatusLabel = EnumDisplayHelper.GetDisplayName(verificationStatus),
            EducationLevel = nanny.EducationLevel,
            EducationLevelLabel = educationLevel.HasValue ? EnumDisplayHelper.GetDisplayName(educationLevel.Value) : null,
            Skills = nanny.NannySkills
                .Where(skill => !skill.IsDeleted && !skill.Skill.IsDeleted && skill.Skill.IsActive)
                .OrderBy(skill => skill.Skill.SortOrder)
                .ThenBy(skill => skill.Skill.Name)
                .Select(MapSkill)
                .ToList(),
            AvailabilitySlots = nanny.NannyAvailabilities
                .Where(slot => !slot.IsDeleted && slot.IsAvailable)
                .OrderBy(slot => slot.TimeSlot)
                .ThenBy(slot => slot.DayOfWeek)
                .Select(MapAvailabilitySlot)
                .ToList(),
            Latitude = (double?)nanny.User.Latitude,
            Longitude = (double?)nanny.User.Longitude
        };
    }

    private static NannyDetailResponse MapToDetailResponse(NannyProfile nanny, bool isFavorite)
    {
        var listItem = MapToListResponse(nanny, isFavorite ? [nanny.Id] : []);
        return new NannyDetailResponse
        {
            Id = listItem.Id,
            UserId = listItem.UserId,
            IsFavorite = listItem.IsFavorite,
            FullName = listItem.FullName,
            AvatarUrl = listItem.AvatarUrl,
            Bio = listItem.Bio,
            City = listItem.City,
            District = listItem.District,
            Age = listItem.Age,
            YearsOfExperience = listItem.YearsOfExperience,
            ExpectedSalaryMin = listItem.ExpectedSalaryMin,
            ExpectedSalaryMax = listItem.ExpectedSalaryMax,
            AverageRating = listItem.AverageRating,
            TotalReviews = listItem.TotalReviews,
            VerificationStatus = listItem.VerificationStatus,
            VerificationStatusLabel = listItem.VerificationStatusLabel,
            EducationLevel = listItem.EducationLevel,
            EducationLevelLabel = listItem.EducationLevelLabel,
            Skills = listItem.Skills,
            AvailabilitySlots = listItem.AvailabilitySlots,
            PhoneNumber = nanny.User.PhoneNumber,
            Address = nanny.User.Address,
            Ward = nanny.User.Ward,
            MaxTravelDistance = nanny.MaxTravelDistance,
            VerifiedAt = nanny.VerifiedAt,
            Latitude = listItem.Latitude,
            Longitude = listItem.Longitude
        };
    }

    private static NannySkillResponse MapSkill(NannySkill skill)
    {
        ProficiencyLevel? proficiency = null;
        if (skill.ProficiencyLevel.HasValue && Enum.IsDefined(typeof(ProficiencyLevel), skill.ProficiencyLevel.Value))
            proficiency = (ProficiencyLevel)skill.ProficiencyLevel.Value;

        return new NannySkillResponse
        {
            SkillId = skill.SkillId,
            SkillName = skill.Skill.Name,
            Category = skill.Skill.Category,
            ProficiencyLevel = skill.ProficiencyLevel,
            ProficiencyLevelLabel = proficiency.HasValue ? EnumDisplayHelper.GetDisplayName(proficiency.Value) : null
        };
    }

    private static int? CalculateAge(DateOnly? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dateOfBirth.Value.Year;
        if (dateOfBirth.Value > today.AddYears(-age))
            age--;

        return age < 0 ? null : age;
    }

    private static NannyAvailabilitySlotResponse MapAvailabilitySlot(NannyAvailability slot) =>
        new()
        {
            DayOfWeek = slot.DayOfWeek,
            DayLabel = GetDayLabel(slot.DayOfWeek),
            TimeSlot = slot.TimeSlot,
            TimeSlotLabel = GetTimeSlotLabel(slot.TimeSlot)
        };

    private static (string? PlanCode, SubscriptionBenefitResponse Benefits) getNannyEntitlement(NannyProfile nanny)
    {
        var nowUtc = DateTime.UtcNow;
        var activeSubscription = nanny.User?.UserSubscriptions?
            .Where(s => !s.IsDeleted && s.Status == 1 && s.EndDate >= nowUtc)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();

        var plan = activeSubscription?.SubscriptionPlan;
        var planName = plan?.Name;
        if (string.Equals(planName, "Pro", StringComparison.OrdinalIgnoreCase))
        {
            return ("NANNY_PRO", new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 0,
                MonthlyApplicationLimit = 5,
                FeaturedBadge = true,
                SearchPriority = true,
                ListingDurationDays = 0,
                CanUseRecommendation = plan!.CanUseRecommendation
            });
        }

        if (string.Equals(planName, "Plus", StringComparison.OrdinalIgnoreCase))
        {
            return ("NANNY_PLUS", new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 0,
                MonthlyApplicationLimit = 3,
                FeaturedBadge = true,
                SearchPriority = false,
                ListingDurationDays = 0,
                CanUseRecommendation = plan!.CanUseRecommendation
            });
        }

        return (null, SubscriptionBenefitResponse.FreeNanny);
    }

    private static string GetDayLabel(int dayOfWeek) => dayOfWeek switch
    {
        0 => "Mon",
        1 => "Tue",
        2 => "Wed",
        3 => "Thu",
        4 => "Fri",
        5 => "Sat",
        6 => "Sun",
        _ => $"Day {dayOfWeek}"
    };

    private static string GetTimeSlotLabel(int timeSlot) => timeSlot switch
    {
        0 => "Morning",
        1 => "Afternoon",
        2 => "Evening",
        3 => "Night",
        _ => $"Slot {timeSlot}"
    };
}
