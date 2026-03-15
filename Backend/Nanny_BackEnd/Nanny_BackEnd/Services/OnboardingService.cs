using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class OnboardingService
{
    private readonly UserRepository _userRepo;
    private readonly ParentRepository _parentRepo;
    private readonly ChildRepository _childRepo;
    private readonly NannyProfileRepository _nannyProfileRepo;
    private readonly NannySkillRepository _nannySkillRepo;
    private readonly NannyAvailabilityRepository _nannyAvailabilityRepo;
    private readonly Sep490NannyDbContext _db;

    public OnboardingService(
        UserRepository userRepo,
        ParentRepository parentRepo,
        ChildRepository childRepo,
        NannyProfileRepository nannyProfileRepo,
        NannySkillRepository nannySkillRepo,
        NannyAvailabilityRepository nannyAvailabilityRepo,
        Sep490NannyDbContext db)
    {
        _userRepo = userRepo;
        _parentRepo = parentRepo;
        _childRepo = childRepo;
        _nannyProfileRepo = nannyProfileRepo;
        _nannySkillRepo = nannySkillRepo;
        _nannyAvailabilityRepo = nannyAvailabilityRepo;
        _db = db;
    }

    public async Task<OnboardingStatusDto> GetStatusAsync(Guid userId)
    {
        var roles = await _userRepo.GetRolesAsync(userId);
        var role = roles.FirstOrDefault() ?? string.Empty;

        var status = new OnboardingStatusDto
        {
            Role = role,
            RequiresOnboarding = false,
            NextStep = "Completed"
        };

        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        // Kiểm tra thông tin cá nhân cơ bản
        // Không bắt buộc DateOfBirth và Ward để tránh bắt user onboarding lại sau khi đã điền đủ thông tin yêu cầu trên giao diện
        var hasBasicInfo =
            !string.IsNullOrWhiteSpace(user.FirstName) &&
            !string.IsNullOrWhiteSpace(user.LastName) &&
            !string.IsNullOrWhiteSpace(user.Address) &&
            !string.IsNullOrWhiteSpace(user.City) &&
            !string.IsNullOrWhiteSpace(user.District);

        if (!hasBasicInfo)
        {
            status.RequiresOnboarding = true;
            status.NextStep = role.Equals("Nanny", StringComparison.OrdinalIgnoreCase)
                ? "NannyBasicInfo"
                : "ParentBasicInfo";
            return status;
        }
        if (role.Equals("Parent", StringComparison.OrdinalIgnoreCase))
        {
            var parentProfile = await _parentRepo.FindByUserIdAsync(userId);
            if (parentProfile == null)
            {
                status.RequiresOnboarding = true;
                status.NextStep = "ParentFamily";
                return status;
            }

            if (string.IsNullOrWhiteSpace(parentProfile.FamilyDescription) || parentProfile.NumberOfChildren == null)
            {
                status.RequiresOnboarding = true;
                status.NextStep = "ParentFamily";
                return status;
            }

            var children = await _childRepo.GetByParentProfileIdAsync(parentProfile.Id);
            if (children.Count == 0)
            {
                status.RequiresOnboarding = true;
                status.NextStep = "ParentChildren";
                return status;
            }

            // Parent đã đủ thông tin
            status.RequiresOnboarding = false;
            status.NextStep = "Completed";
            return status;
        }

        if (role.Equals("Nanny", StringComparison.OrdinalIgnoreCase))
        {
            var nannyProfile = await _nannyProfileRepo.FindByUserIdAsync(userId);
            if (nannyProfile == null ||
                string.IsNullOrWhiteSpace(nannyProfile.Bio) ||
                nannyProfile.YearsOfExperience == null ||
                nannyProfile.EducationLevel == null ||
                nannyProfile.ExpectedSalaryMin == null ||
                nannyProfile.ExpectedSalaryMax == null ||
                nannyProfile.MaxTravelDistance == null)
            {
                status.RequiresOnboarding = true;
                status.NextStep = "NannyProfile";
                return status;
            }

            var skills = await _nannySkillRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
            if (skills.Count == 0)
            {
                status.RequiresOnboarding = true;
                status.NextStep = "NannySkills";
                return status;
            }

            var avails = await _nannyAvailabilityRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
            if (avails.Count == 0)
            {
                status.RequiresOnboarding = true;
                status.NextStep = "NannyAvailability";
                return status;
            }

            status.RequiresOnboarding = false;
            status.NextStep = "Completed";
        }

        return status;
    }

    public async Task<List<SkillSelectionDto>> GetAllSkillsAsync()
    {
        var skills = await _db.Skills
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        return skills.Select(s => new SkillSelectionDto
        {
            SkillId = s.Id,
            SkillName = s.Name,
            Category = s.Category
        }).ToList();
    }

    public async Task<NannyProfile> UpdateNannyProfileAsync(Guid userId, UpdateNannyProfileRequest request)
    {
        var profile = await _nannyProfileRepo.FindByUserIdAsync(userId);
        if (profile == null)
        {
            profile = new NannyProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            _nannyProfileRepo.Add(profile);
        }

        profile.Bio = request.Bio;
        profile.YearsOfExperience = request.YearsOfExperience;
        profile.EducationLevel = request.EducationLevel;
        profile.ExpectedSalaryMin = request.ExpectedSalaryMin;
        profile.ExpectedSalaryMax = request.ExpectedSalaryMax;
        profile.MaxTravelDistance = request.MaxTravelDistance;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = userId;

        await _nannyProfileRepo.SaveChangesAsync();
        return profile;
    }

    public async Task UpdateNannySkillsAsync(Guid userId, UpdateNannySkillsRequest request)
    {
        var profile = await _nannyProfileRepo.FindByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Chưa có hồ sơ nanny.");

        var existing = await _nannySkillRepo.GetByNannyProfileIdAsync(profile.Id);
        if (existing.Any())
        {
            _nannySkillRepo.RemoveRange(existing);
        }

        var now = DateTime.UtcNow;
        var newSkills = request.Skills.Select(s => new NannySkill
        {
            Id = Guid.NewGuid(),
            NannyProfileId = profile.Id,
            SkillId = s.SkillId,
            ProficiencyLevel = s.ProficiencyLevel,
            CreatedAt = now,
            CreatedBy = userId
        });

        _nannySkillRepo.AddRange(newSkills);
        await _nannySkillRepo.SaveChangesAsync();
    }

    public async Task UpdateNannyAvailabilityAsync(Guid userId, UpdateNannyAvailabilityRequest request)
    {
        var profile = await _nannyProfileRepo.FindByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Chưa có hồ sơ nanny.");

        var existing = await _nannyAvailabilityRepo.GetByNannyProfileIdAsync(profile.Id);
        if (existing.Any())
        {
            _nannyAvailabilityRepo.RemoveRange(existing);
        }

        var items = new List<NannyAvailability>();
        var now = DateTime.UtcNow;

        foreach (var day in request.Days)
        {
            // Morning: 6h - 12h
            if (day.Morning)
                items.Add(CreateAvailability(profile.Id, day.DayOfWeek, new TimeOnly(6, 0), new TimeOnly(12, 0), userId, now));
            // Afternoon: 13h - 19h
            if (day.Afternoon)
                items.Add(CreateAvailability(profile.Id, day.DayOfWeek, new TimeOnly(13, 0), new TimeOnly(19, 0), userId, now));
            // Evening: 20h - 24h (lưu 23:59:59 cho end of day)
            if (day.Evening)
                items.Add(CreateAvailability(profile.Id, day.DayOfWeek, new TimeOnly(20, 0), new TimeOnly(23, 59, 59), userId, now));
            // Night: 1h - 5h
            if (day.Night)
                items.Add(CreateAvailability(profile.Id, day.DayOfWeek, new TimeOnly(1, 0), new TimeOnly(5, 0), userId, now));
        }

        if (items.Any())
            _nannyAvailabilityRepo.AddRange(items);

        await _nannyAvailabilityRepo.SaveChangesAsync();
    }

    private static NannyAvailability CreateAvailability(Guid nannyProfileId, int dayOfWeek, TimeOnly start, TimeOnly end, Guid userId, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            NannyProfileId = nannyProfileId,
            DayOfWeek = dayOfWeek,
            StartTime = start,
            EndTime = end,
            IsAvailable = true,
            CreatedAt = now,
            CreatedBy = userId
        };
}

