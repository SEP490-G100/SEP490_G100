using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class OnboardingService : IOnboardingService
{
    private readonly IUserRepository _userRepo;
    private readonly IParentRepository _parentRepo;
    private readonly IChildRepository _childRepo;
    private readonly INannyProfileRepository _nannyProfileRepo;
    private readonly INannySkillRepository _nannySkillRepo;
    private readonly INannyAvailabilityRepository _nannyAvailabilityRepo;
    private readonly IJobRepository _jobRepo;
    private readonly ILogger<OnboardingService> _logger;

    public OnboardingService(
        IUserRepository userRepo,
        IParentRepository parentRepo,
        IChildRepository childRepo,
        INannyProfileRepository nannyProfileRepo,
        INannySkillRepository nannySkillRepo,
        INannyAvailabilityRepository nannyAvailabilityRepo,
        IJobRepository jobRepo,
        ILogger<OnboardingService> logger)
    {
        _userRepo = userRepo;
        _parentRepo = parentRepo;
        _childRepo = childRepo;
        _nannyProfileRepo = nannyProfileRepo;
        _nannySkillRepo = nannySkillRepo;
        _nannyAvailabilityRepo = nannyAvailabilityRepo;
        _jobRepo = jobRepo;
        _logger = logger;
    }

    public async Task<OnboardingStatusDto> GetStatusAsync(Guid userId)
    {
        var roles = await _userRepo.GetRolesAsync(userId);
        var role = roles.FirstOrDefault() ?? string.Empty;

        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Nguoi dung khong ton tai.");

        var hasBasicInfo =
            !string.IsNullOrWhiteSpace(user.FirstName) &&
            !string.IsNullOrWhiteSpace(user.LastName) &&
            !string.IsNullOrWhiteSpace(user.Address) &&
            !string.IsNullOrWhiteSpace(user.City) &&
            !string.IsNullOrWhiteSpace(user.District);

        // Nếu thiếu thông tin cơ bản, PHẢI chọn role trước 
        // (vì không thể điền FirstName/LastName/City/District mà không biết role)
        if (!hasBasicInfo)
        {
            if (string.IsNullOrEmpty(role))
            {
                return new OnboardingStatusDto
                {
                    Role = string.Empty,
                    RequiresOnboarding = true,
                    NextStep = "SelectRole"
                };
            }

            // Nếu user có role nhưng thiếu basic info, hãy tiến hành điền basic info
            var status = new OnboardingStatusDto
            {
                Role = role,
                RequiresOnboarding = true,
                NextStep = role.Equals("Nanny", StringComparison.OrdinalIgnoreCase)
                    ? "NannyBasicInfo"
                    : "ParentBasicInfo"
            };
            return status;
        }

        // Nếu chưa chọn role mà đã có thông tin cơ bản, yêu cầu chọn role
        if (string.IsNullOrEmpty(role))
        {
            return new OnboardingStatusDto
            {
                Role = string.Empty,
                RequiresOnboarding = true,
                NextStep = "SelectRole"
            };
        }

        var finalStatus = new OnboardingStatusDto
        {
            Role = role,
            RequiresOnboarding = false,
            NextStep = "Completed"
        };

        if (role.Equals("Parent", StringComparison.OrdinalIgnoreCase))
        {
            var parentProfile = await _parentRepo.FindByUserIdAsync(userId);
            if (parentProfile == null)
            {
                finalStatus.RequiresOnboarding = true;
                finalStatus.NextStep = "ParentFamily";
                return finalStatus;
            }

            if (string.IsNullOrWhiteSpace(parentProfile.FamilyDescription) || parentProfile.NumberOfChildren == null)
            {
                finalStatus.RequiresOnboarding = true;
                finalStatus.NextStep = "ParentFamily";
                return finalStatus;
            }

            // Parent đã đủ thông tin (children là tùy chọn, không bắt buộc để hoàn thành onboarding)
            finalStatus.RequiresOnboarding = false;
            finalStatus.NextStep = "Completed";
            return finalStatus;
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
                finalStatus.RequiresOnboarding = true;
                finalStatus.NextStep = "NannyProfile";
                return finalStatus;
            }

            var skills = await _nannySkillRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
            if (skills.Count == 0)
            {
                finalStatus.RequiresOnboarding = true;
                finalStatus.NextStep = "NannySkills";
                return finalStatus;
            }

            var availabilities = await _nannyAvailabilityRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
            if (availabilities.Count == 0)
            {
                finalStatus.RequiresOnboarding = true;
                finalStatus.NextStep = "NannyAvailability";
                return finalStatus;
            }

            finalStatus.RequiresOnboarding = false;
            finalStatus.NextStep = "Completed";
        }

        return finalStatus;
    }

    public async Task<List<SkillSelectionDto>> GetAllSkillsAsync()
    {
        var skills = await _jobRepo.getActiveSkills();

        return skills.Select(s => new SkillSelectionDto
        {
            SkillId = s.Id,
            SkillName = s.Name,
            Category = s.Category
        }).ToList();
    }

    public async Task<NannyProfile> UpdateNannyProfileAsync(Guid userId, UpdateNannyProfileRequest request)
    {
        var normalizedBio = NormalizeOptionalText(request.Bio, 2000, "Giới thiệu bản thân");
        if (request.YearsOfExperience is < 0 or > 80)
            throw new InvalidOperationException("Số năm kinh nghiệm phải trong khoảng 0-80.");

        if (request.MaxTravelDistance is < 0 or > 1000)
            throw new InvalidOperationException("Khoảng cách di chuyển tối đa phải trong khoảng 0-1000 km.");

        if (request.EducationLevel.HasValue &&
            !Enum.IsDefined(typeof(EducationLevel), request.EducationLevel.Value))
        {
            throw new InvalidOperationException("Trình độ học vấn không hợp lệ.");
        }

        var salaryValidationError = SalaryValidationRules.GetFirstError(
            request.ExpectedSalaryMin,
            request.ExpectedSalaryMax,
            "Lương từ",
            "Đến");
        if (!string.IsNullOrWhiteSpace(salaryValidationError))
            throw new InvalidOperationException(salaryValidationError);

        var profile = await _nannyProfileRepo.FindByUserIdAsync(userId);
        if (profile == null)
        {
            profile = new NannyProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SalaryType = 0,
                ProfileCompleteness = 0,
                TotalReviews = 0,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                VerificationStatus = (int)VerificationStatus.NotSubmitted
            };
            _nannyProfileRepo.Add(profile);
        }

        profile.Bio = normalizedBio;
        profile.YearsOfExperience = request.YearsOfExperience;
        profile.EducationLevel = (int?)request.EducationLevel;
        profile.ExpectedSalaryMin = request.ExpectedSalaryMin;
        profile.ExpectedSalaryMax = request.ExpectedSalaryMax;
        profile.MaxTravelDistance = request.MaxTravelDistance;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = userId;

        try
        {
            await _nannyProfileRepo.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi lưu hồ sơ onboarding nanny cho UserId={UserId}", userId);
            throw new InvalidOperationException(BuildFriendlyDbUpdateMessage(ex));
        }

        return profile;
    }

    public async Task UpdateNannySkillsAsync(Guid userId, UpdateNannySkillsRequest request)
    {
        var profile = await _nannyProfileRepo.FindByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Chưa có hồ sơ nanny.");

        var existing = await _nannySkillRepo.GetByNannyProfileIdAsync(profile.Id);
        if (existing.Any())
            _nannySkillRepo.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var newSkills = request.Skills.Select(s => new NannySkill
        {
            Id = Guid.NewGuid(),
            NannyProfileId = profile.Id,
            SkillId = s.SkillId,
            ProficiencyLevel = (int?)s.ProficiencyLevel,
            CreatedAt = now,
            CreatedBy = userId
        });

        _nannySkillRepo.AddRange(newSkills);
        try
        {
            await _nannySkillRepo.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi lưu kỹ năng onboarding nanny cho UserId={UserId}", userId);
            throw new InvalidOperationException(BuildFriendlyDbUpdateMessage(ex));
        }
    }

    public async Task UpdateNannyAvailabilityAsync(Guid userId, UpdateNannyAvailabilityRequest request)
    {
        var profile = await _nannyProfileRepo.FindByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Chưa có hồ sơ nanny.");

        var existing = await _nannyAvailabilityRepo.GetByNannyProfileIdAsync(profile.Id);
        if (existing.Any())
            _nannyAvailabilityRepo.RemoveRange(existing);

        var items = new List<NannyAvailability>();
        var now = DateTime.UtcNow;

        foreach (var day in request.Days)
        {
            // TimeSlot: 0=Morning, 1=Afternoon, 2=Evening, 3=Night
            if (day.Morning)
                items.Add(CreateAvailability(profile.Id, day.DayOfWeek, 0, userId, now));
            if (day.Afternoon)
                items.Add(CreateAvailability(profile.Id, day.DayOfWeek, 1, userId, now));
            if (day.Evening)
                items.Add(CreateAvailability(profile.Id, day.DayOfWeek, 2, userId, now));
            if (day.Night)
                items.Add(CreateAvailability(profile.Id, day.DayOfWeek, 3, userId, now));
        }

        if (items.Any())
            _nannyAvailabilityRepo.AddRange(items);

        try
        {
            await _nannyAvailabilityRepo.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi lưu lịch làm việc onboarding nanny cho UserId={UserId}", userId);
            throw new InvalidOperationException(BuildFriendlyDbUpdateMessage(ex));
        }
    }

    private static NannyAvailability CreateAvailability(Guid nannyProfileId, int dayOfWeek, int timeSlot, Guid userId, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            NannyProfileId = nannyProfileId,
            DayOfWeek = dayOfWeek,
            TimeSlot = timeSlot,
            IsAvailable = true,
            CreatedAt = now,
            CreatedBy = userId
        };

    private static string? NormalizeOptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new InvalidOperationException($"{fieldName} không được vượt quá {maxLength} ký tự.");

        return normalized;
    }

    private static string BuildFriendlyDbUpdateMessage(DbUpdateException ex)
    {
        var sqlEx = ex.GetBaseException() as SqlException;
        if (sqlEx == null)
            return "Không thể lưu dữ liệu hồ sơ. Vui lòng kiểm tra lại thông tin.";

        if ((sqlEx.Number == 2601 || sqlEx.Number == 2627) &&
            sqlEx.Message.Contains("UQ_NannyProfiles_UserId", StringComparison.OrdinalIgnoreCase))
        {
            return "Hồ sơ bảo mẫu đã tồn tại. Vui lòng tải lại trang và thử lại.";
        }

        if ((sqlEx.Number == 2601 || sqlEx.Number == 2627) &&
            sqlEx.Message.Contains("UQ_NannySkills_NannyProfileId_SkillId", StringComparison.OrdinalIgnoreCase))
        {
            return "Danh sách kỹ năng có mục bị trùng. Vui lòng kiểm tra lại.";
        }

        if (sqlEx.Number == 515)
        {
            var (column, table) = TryExtractSqlColumnAndTable(sqlEx.Message);
            if (!string.IsNullOrWhiteSpace(column))
            {
                var target = string.IsNullOrWhiteSpace(table) ? column : $"{table}.{column}";
                return $"Thiếu dữ liệu bắt buộc: {target}.";
            }

            return "Thiếu dữ liệu bắt buộc để lưu hồ sơ.";
        }

        if (sqlEx.Number == 8152 || sqlEx.Number == 2628)
            return "Một số trường vượt quá độ dài cho phép. Vui lòng rút gọn nội dung và thử lại.";

        if (sqlEx.Number == 547)
            return "Dữ liệu cập nhật không hợp lệ theo ràng buộc hệ thống.";

        return $"Không thể lưu dữ liệu hồ sơ (SQL {sqlEx.Number}).";
    }

    private static (string? Column, string? Table) TryExtractSqlColumnAndTable(string? sqlMessage)
    {
        if (string.IsNullOrWhiteSpace(sqlMessage))
            return (null, null);

        var match = Regex.Match(sqlMessage, @"column '([^']+)'.*table '([^']+)'", RegexOptions.IgnoreCase);
        if (!match.Success)
            return (null, null);

        var column = match.Groups.Count > 1 ? match.Groups[1].Value : null;
        var table = match.Groups.Count > 2 ? match.Groups[2].Value : null;
        return (column, table);
    }
}
