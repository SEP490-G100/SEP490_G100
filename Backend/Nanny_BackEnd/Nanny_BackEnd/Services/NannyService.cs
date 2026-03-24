using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class NannyService
{
    private readonly NannyProfileRepository _nannyProfileRepository;

    public NannyService(NannyProfileRepository nannyProfileRepository)
    {
        _nannyProfileRepository = nannyProfileRepository;
    }

    public async Task<NannyPagedResultResponse> GetListAsync(NannyListRequest request)
    {
        var parsedSkillIds = ParseSkillIds(request.SkillIds);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 12 : Math.Min(request.PageSize, 50);
        var (items, totalCount) = await _nannyProfileRepository.SearchAsync(request, parsedSkillIds);

        return new NannyPagedResultResponse
        {
            Items = items.Select(MapToListResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<NannyDetailResponse> GetDetailAsync(Guid nannyProfileId)
    {
        var nanny = await _nannyProfileRepository.GetDetailAsync(nannyProfileId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so nanny.");

        return MapToDetailResponse(nanny);
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

    private static NannyListItemResponse MapToListResponse(NannyProfile nanny)
    {
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

    private static NannyDetailResponse MapToDetailResponse(NannyProfile nanny)
    {
        var listItem = MapToListResponse(nanny);
        return new NannyDetailResponse
        {
            Id = listItem.Id,
            UserId = listItem.UserId,
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
            ProfileCompleteness = nanny.ProfileCompleteness,
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
