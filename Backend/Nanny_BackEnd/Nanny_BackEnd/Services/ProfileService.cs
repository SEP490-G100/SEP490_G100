using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;
using System.Text.RegularExpressions;

namespace Nanny_BackEnd.Services;

public class ProfileService : IProfileService
{
    private readonly IUserRepository _userRepo;
    private readonly IParentRepository _parentRepo;
    private readonly IChildRepository _childRepo;
    private readonly INannyProfileRepository _nannyProfileRepo;
    private readonly INannySkillRepository _nannySkillRepo;
    private readonly INannyCertificateRepository _nannyCertificateRepo;
    private readonly INannyAvailabilityRepository _nannyAvailabilityRepo;
    private readonly IVerificationRequestRepository _verificationRequestRepo;
    private readonly IWebHostEnvironment _env;
    private readonly IGeocodingService _geo;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        IUserRepository userRepo,
        IParentRepository parentRepo,
        IChildRepository childRepo,
        INannyProfileRepository nannyProfileRepo,
        INannySkillRepository nannySkillRepo,
        INannyCertificateRepository nannyCertificateRepo,
        INannyAvailabilityRepository nannyAvailabilityRepo,
        IVerificationRequestRepository verificationRequestRepo,
        IWebHostEnvironment env,
        IGeocodingService geo,
        IServiceScopeFactory scopeFactory,
        ILogger<ProfileService> logger)
    {
        _userRepo = userRepo;
        _parentRepo = parentRepo;
        _childRepo = childRepo;
        _nannyProfileRepo = nannyProfileRepo;
        _nannySkillRepo = nannySkillRepo;
        _nannyCertificateRepo = nannyCertificateRepo;
        _nannyAvailabilityRepo = nannyAvailabilityRepo;
        _verificationRequestRepo = verificationRequestRepo;
        _env = env;
        _geo = geo;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string> UploadAvatarAsync(Guid userId, IFormFile file)
    {
        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("NgÆ°á»i dÃ¹ng khÃ´ng tá»“n táº¡i.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png" };
        if (!allowedExts.Contains(ext))
            throw new InvalidOperationException("Chá»‰ cháº¥p nháº­n file áº£nh .jpg, .jpeg hoáº·c .png.");

        var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var allowedTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(contentType))
            throw new InvalidOperationException("Chá»‰ cháº¥p nháº­n áº£nh JPEG/PNG há»£p lá»‡.");

        if (file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("File áº£nh khÃ´ng Ä‘Æ°á»£c vÆ°á»£t quÃ¡ 5MB.");

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{userId}{ext}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var avatarUrl = $"/uploads/avatars/{fileName}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        
        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = userId;
        await _userRepo.SaveChangesAsync();

        return avatarUrl;
    }

    public async Task<PersonalProfileDto> GetPersonalProfileAsync(Guid userId)
    {
        return await BuildProfileDtoAsync(userId);
    }

    public async Task<PersonalProfileDto> GetPublicProfileAsync(Guid requesterUserId, Guid targetUserId)
    {
        var profile = await BuildProfileDtoAsync(targetUserId);

        if (requesterUserId == targetUserId)
            return profile;

        var isTargetParent = profile.Roles.Any(r => r.Equals("parent", StringComparison.OrdinalIgnoreCase));
        var isTargetNanny = profile.Roles.Any(r => r.Equals("nanny", StringComparison.OrdinalIgnoreCase));

        // Parent profile remains private for public view.
        if (isTargetParent)
        {
            profile.Email = string.Empty;
            profile.PhoneNumber = null;
            profile.DateOfBirth = null;
            profile.Age = null;
            profile.Address = null;
            profile.Ward = null;
            profile.Latitude = null;
            profile.Longitude = null;
            profile.FamilyDescription = null;
            profile.NumberOfChildren = null;
            profile.Children = null;
            profile.SpecialNeeds = null;
            profile.Notes = null;
            profile.Characteristic = null;
            profile.ChildAgeGroup = null;
        }
        else if (isTargetNanny)
        {
            // Keep basic contact/location + DOB for age display on nanny detail page.
            // Phone privacy will be handled by UI masking in read-only view.
        }
        else
        {
            profile.Email = string.Empty;
            profile.PhoneNumber = null;
            profile.DateOfBirth = null;
            profile.Age = null;
            profile.Address = null;
            profile.Ward = null;
            profile.Latitude = null;
            profile.Longitude = null;
        }

        return profile;
    }

    private async Task<PersonalProfileDto> BuildProfileDtoAsync(Guid userId)
    {
        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("NgÆ°á»i dÃ¹ng khÃ´ng tá»“n táº¡i.");

        var roles = await _userRepo.GetRolesAsync(userId);
        var isParent = roles.Any(r => r.Equals("parent", StringComparison.OrdinalIgnoreCase));
        var isNanny = roles.Any(r => r.Equals("nanny", StringComparison.OrdinalIgnoreCase));

        string? verificationStatus = null;
        decimal? averageRating = null;
        int? totalReviews = null;
        string? bio = null;
        int? yearsOfExperience = null;
        int? educationLevel = null;
        decimal? expectedSalaryMin = null;
        decimal? expectedSalaryMax = null;
        int? maxTravelDistance = null;
        int? verificationStatusCode = null;
        Guid? nannyProfileId = null;
        List<NannySkillItemDto>? skills = null;
        List<NannyAvailabilityItemDto>? availabilities = null;
        List<NannyCertificateItemDto>? certificates = null;
        var hasHealthCertificate = false;

        if (isNanny)
        {
            var nannyProfile = await _nannyProfileRepo.FindByUserIdAsync(userId);
            if (nannyProfile != null)
            {
                nannyProfileId = nannyProfile.Id;
                bio = nannyProfile.Bio;
                yearsOfExperience = nannyProfile.YearsOfExperience;
                educationLevel = nannyProfile.EducationLevel.HasValue ? (int)nannyProfile.EducationLevel.Value : null;
                expectedSalaryMin = nannyProfile.ExpectedSalaryMin;
                expectedSalaryMax = nannyProfile.ExpectedSalaryMax;
                maxTravelDistance = nannyProfile.MaxTravelDistance;
                averageRating = nannyProfile.AverageRating ?? 0;
                totalReviews = nannyProfile.TotalReviews;
                verificationStatusCode = nannyProfile.VerificationStatus;

                verificationStatus = (Enums.VerificationStatus)nannyProfile.VerificationStatus switch
                {
                    Enums.VerificationStatus.NotSubmitted => "Chưa được xác thực",
                    Enums.VerificationStatus.Pending => "Đang chờ xác thực",
                    Enums.VerificationStatus.Approved => "Đã được xác thực",
                    Enums.VerificationStatus.Rejected => "Bị từ chối xác thực",
                    _ => "Chưa được xác thực"
                };

                var nannySkills = await _nannySkillRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
                skills = nannySkills
                    .Select(s => new NannySkillItemDto
                    {
                        SkillId = s.SkillId,
                        SkillName = s.Skill?.Name,
                        ProficiencyLevel = s.ProficiencyLevel.HasValue ? (int)s.ProficiencyLevel.Value : null
                    })
                    .ToList();

                var nannyAvail = await _nannyAvailabilityRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
                availabilities = nannyAvail
                    .Select(a => new NannyAvailabilityItemDto
                    {
                        DayOfWeek = a.DayOfWeek,
                        IsAvailable = a.IsAvailable,
                        TimeSlot = a.TimeSlot
                    })
                    .ToList();

                var nannyCertificates = await _nannyCertificateRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
                certificates = nannyCertificates
                    .Select(c => new NannyCertificateItemDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        IssuingOrganization = c.IssuingOrganization,
                        CertificateUrl = c.CertificateUrl,
                        VerificationStatus = c.VerificationStatus
                    })
                    .ToList();

                try
                {
                    var verificationRequests = await _verificationRequestRepo.GetRequestsByNannyProfileAsync(nannyProfile.Id);
                    hasHealthCertificate = verificationRequests
                        .SelectMany(r => r.VerificationDocuments)
                        .Any(d => d.DocumentType == (int)Enums.VerificationDocumentType.HealthCertificate && !d.IsDeleted);
                }
                catch (Exception ex)
                {
                    // Avoid breaking profile page if verification schema is temporarily out of sync.
                    _logger.LogWarning(ex, "Skip loading verification request summary for NannyProfileId={NannyProfileId}", nannyProfile.Id);
                    hasHealthCertificate = false;
                }
            }
            else
            {
                verificationStatus = "ChÆ°a Ä‘Æ°á»£c xÃ¡c thá»±c";
            }
        }

        string? familyDescription = null;
        int? numberOfChildren = null;
        List<ChildProfileDto>? children = null;
        string? specialNeeds = null;
        string? notes = null;
        string? characteristic = null;
        byte? childAgeGroup = null;

        if (isParent)
        {
            var parentProfile = await _parentRepo.FindByUserIdAsync(userId);
            if (parentProfile != null)
            {
                familyDescription = parentProfile.FamilyDescription;
                numberOfChildren = parentProfile.NumberOfChildren;

                var childEntities = await _childRepo.GetByParentProfileIdAsync(parentProfile.Id);
                children = childEntities
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new ChildProfileDto
                    {
                        Id = c.Id,
                        ParentProfileId = c.ParentProfileId,
                        SpecialNeeds = c.SpecialNeeds,
                        Notes = c.Notes,
                        Characteristic = c.Characteristic,
                        ChildAgeGroup = (byte?)c.ChildAgeGroup,
                        CreatedAt = c.CreatedAt
                    })
                    .ToList();

                var firstChild = children.FirstOrDefault();
                if (firstChild != null)
                {
                    specialNeeds = firstChild.SpecialNeeds;
                    notes = firstChild.Notes;
                    characteristic = firstChild.Characteristic;
                    childAgeGroup = firstChild.ChildAgeGroup;
                }
            }
        }

        return new PersonalProfileDto
        {
            UserId = user.Id,
            NannyProfileId = nannyProfileId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            DateOfBirth = user.DateOfBirth,
            Age = CalculateAge(user.DateOfBirth),
            Gender = user.Gender,
            Address = user.Address,
            City = user.City,
            District = user.District,
            Ward = user.Ward,
            Latitude = user.Latitude,
            Longitude = user.Longitude,
            Roles = roles,

            FamilyDescription = familyDescription,
            NumberOfChildren = numberOfChildren,
            Children = children,
            SpecialNeeds = specialNeeds,
            Notes = notes,
            Characteristic = characteristic,
            ChildAgeGroup = childAgeGroup,

            Bio = bio,
            YearsOfExperience = yearsOfExperience,
            EducationLevel = educationLevel,
            ExpectedSalaryMin = expectedSalaryMin,
            ExpectedSalaryMax = expectedSalaryMax,
            MaxTravelDistance = maxTravelDistance,
            VerificationStatus = verificationStatus,
            VerificationStatusCode = verificationStatusCode,
            AverageRating = averageRating,
            TotalReviews = totalReviews,
            Skills = skills,
            Availabilities = availabilities,
            Certificates = certificates,
            HasHealthCertificate = hasHealthCertificate
        };
    }

    private static int? CalculateAge(DateOnly? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
            return null;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var dob = dateOfBirth.Value;
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age))
            age--;

        return age >= 0 ? age : null;
    }

    public async Task<PersonalProfileDto> UpdatePersonalInfoAsync(Guid userId, UpdatePersonalInfoRequest request)
    {
        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("NgÆ°á»i dÃ¹ng khÃ´ng tá»“n táº¡i.");

        var roles = await _userRepo.GetRolesAsync(userId);
        var isNanny = roles.Any(r => r.Equals("nanny", StringComparison.OrdinalIgnoreCase));
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (request.DateOfBirth.HasValue && request.DateOfBirth.Value > today)
            throw new InvalidOperationException("Ngay sinh khong duoc lon hon ngay hien tai.");

        if (isNanny)
        {
            var salaryValidationError = SalaryValidationRules.GetFirstError(
                request.ExpectedSalaryMin,
                request.ExpectedSalaryMax,
                "Luong toi thieu",
                "Luong toi da");
            if (!string.IsNullOrWhiteSpace(salaryValidationError))
                throw new InvalidOperationException(salaryValidationError);

            var dobToValidate = request.DateOfBirth ?? user.DateOfBirth;
            if (!dobToValidate.HasValue)
                throw new InvalidOperationException("Nanny pháº£i nháº­p ngÃ ,áy sinh.");

            var dob = dobToValidate.Value;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            if (age <= 30)
                throw new InvalidOperationException("Nanny phai lon hon 30 tuoi.");
        }

        // Map required core fields
        user.FirstName = request.FirstName?.Trim() ?? user.FirstName;
        user.LastName = request.LastName?.Trim() ?? user.LastName;

        // Partial update for optional fields: keep old values when input is empty
        var normalizedPhone = NormalizePhoneNumber(request.PhoneNumber);
        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            if (!IsValidPhoneNumber(normalizedPhone))
                throw new InvalidOperationException("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");

            var isPhoneChanged = !string.Equals(user.PhoneNumber, normalizedPhone, StringComparison.Ordinal);
            if (isPhoneChanged && await _userRepo.IsPhoneInUseAsync(normalizedPhone))
                throw new InvalidOperationException("Số điện thoại đã được đăng ký.");

            user.PhoneNumber = normalizedPhone;
        }
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
            user.AvatarUrl = request.AvatarUrl;

        if (request.DateOfBirth.HasValue)
            user.DateOfBirth = request.DateOfBirth;

        if (request.Gender.HasValue)
            user.Gender = request.Gender;

        if (!string.IsNullOrWhiteSpace(request.Address))
            user.Address = request.Address.Trim();

        if (!string.IsNullOrWhiteSpace(request.City))
            user.City = request.City.Trim();

        if (!string.IsNullOrWhiteSpace(request.District))
            user.District = request.District.Trim();

        if (!string.IsNullOrWhiteSpace(request.Ward))
            user.Ward = request.Ward.Trim();

        // Geocoding fallback by administrative area only (district -> city).
        var coords = await _geo.geocode(null, user.City, user.District);
        if (coords.HasValue)
        {
            user.Latitude = coords.Value.Lat;
            user.Longitude = coords.Value.Lng;
        }
        else if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            user.Latitude = request.Latitude;
            user.Longitude = request.Longitude;
        }

        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = userId;

        if (isNanny)
        {
            var hasNannySpecificPayload = HasNannySpecificPayload(request);
            var nannyProfile = await _nannyProfileRepo.FindByUserIdAsync(userId);
            if (nannyProfile == null && hasNannySpecificPayload)
            {
                nannyProfile = new NannyProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SalaryType = 0,
                    TotalReviews = 0,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    VerificationStatus = (int)Enums.VerificationStatus.NotSubmitted
                };
                _nannyProfileRepo.Add(nannyProfile);
            }

            if (nannyProfile != null)
            {
                if (!string.IsNullOrWhiteSpace(request.Bio))
                    nannyProfile.Bio = request.Bio.Trim();

                if (request.YearsOfExperience.HasValue)
                    nannyProfile.YearsOfExperience = request.YearsOfExperience.Value;

                if (request.EducationLevel.HasValue)
                    nannyProfile.EducationLevel = request.EducationLevel.Value;

                if (request.ExpectedSalaryMin.HasValue)
                    nannyProfile.ExpectedSalaryMin = request.ExpectedSalaryMin.Value;

                if (request.ExpectedSalaryMax.HasValue)
                    nannyProfile.ExpectedSalaryMax = request.ExpectedSalaryMax.Value;

                if (request.MaxTravelDistance.HasValue)
                    nannyProfile.MaxTravelDistance = request.MaxTravelDistance.Value;

                if (request.SkillIds != null)
                {
                    var selectedSkillIds = request.SkillIds
                        .Where(x => x != Guid.Empty)
                        .Distinct()
                        .ToList();

                    var existingSkills = await _nannySkillRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
                    if (existingSkills.Any())
                        _nannySkillRepo.RemoveRange(existingSkills);

                    if (selectedSkillIds.Any())
                    {
                        var now = DateTime.UtcNow;
                        var newSkills = selectedSkillIds.Select(skillId => new NannySkill
                        {
                            Id = Guid.NewGuid(),
                            NannyProfileId = nannyProfile.Id,
                            SkillId = skillId,
                            ProficiencyLevel = null,
                            CreatedAt = now,
                            CreatedBy = userId
                        });
                        _nannySkillRepo.AddRange(newSkills);
                    }
                }

                nannyProfile.UpdatedAt = DateTime.UtcNow;
                nannyProfile.UpdatedBy = userId;
            }
        }

        try
        {
            await _userRepo.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi lưu hồ sơ cá nhân cho UserId={UserId}", userId);
            throw new InvalidOperationException(BuildFriendlyDbUpdateMessage(ex));
        }

        // Fire-and-forget: cập nhật embedding sau khi nanny sửa profile
        if (isNanny)
        {
            var nannyId = (await _nannyProfileRepo.FindByUserIdAsync(userId))?.Id;
            if (nannyId.HasValue)
                _ = EmbedNannyInBackgroundAsync(nannyId.Value);
        }

        return await GetPersonalProfileAsync(userId);
    }

    public async Task AddNannyCertificateAsync(Guid userId, CreateNannyCertificateRequest request)
    {
        var roles = await _userRepo.GetRolesAsync(userId);
        var isNanny = roles.Any(r => r.Equals("nanny", StringComparison.OrdinalIgnoreCase));
        if (!isNanny)
            throw new InvalidOperationException("Chỉ tài khoản bảo mẫu mới có thể thêm chứng chỉ.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Vui lòng nhập tên chứng chỉ.");

        var nannyProfile = await _nannyProfileRepo.FindByUserIdAsync(userId);
        if (nannyProfile == null)
        {
            nannyProfile = new NannyProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SalaryType = 0,
                TotalReviews = 0,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                VerificationStatus = (int)Enums.VerificationStatus.NotSubmitted
            };
            _nannyProfileRepo.Add(nannyProfile);
            await _nannyProfileRepo.SaveChangesAsync();
        }

        var cert = new NannyCertificate
        {
            Id = Guid.NewGuid(),
            NannyProfileId = nannyProfile.Id,
            Name = request.Name.Trim(),
            IssuingOrganization = string.IsNullOrWhiteSpace(request.IssuingOrganization) ? null : request.IssuingOrganization.Trim(),
            CertificateUrl = string.IsNullOrWhiteSpace(request.CertificateUrl) ? null : request.CertificateUrl.Trim(),
            VerificationStatus = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _nannyCertificateRepo.Add(cert);
        await _nannyCertificateRepo.SaveChangesAsync();
    }

    public async Task<List<ChildProfileDto>> GetChildProfilesAsync(Guid userId)
    {
        var roles = await _userRepo.GetRolesAsync(userId);
        if (!roles.Any(r => r.ToLower() == "parent"))
            throw new UnauthorizedAccessException("Chá»‰ ngÆ°á»i dÃ¹ng cÃ³ vá»‹ trÃ­ Parent má»›i cÃ³ thá»ƒ xem.");

        var parentProfile = await _parentRepo.FindByUserIdAsync(userId);
        if (parentProfile == null) return new();

        var children = await _childRepo.GetByParentProfileIdAsync(parentProfile.Id);

        return children.Select(c => new ChildProfileDto
        {
            Id = c.Id,
            ParentProfileId = c.ParentProfileId,
            SpecialNeeds = c.SpecialNeeds,
            Notes = c.Notes,
            Characteristic = c.Characteristic,
            ChildAgeGroup = (byte?)c.ChildAgeGroup,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task<ChildProfileDto> CreateChildProfileAsync(Guid userId, CreateChildProfileRequest request)
    {
        var roles = await _userRepo.GetRolesAsync(userId);
        if (!roles.Any(r => r.Equals("parent", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chá»‰ dÃ nh cho Parent.");

        var normalizedSpecialNeeds = NormalizeOptionalText(request.SpecialNeeds, 1000, "Nhu cầu đặc biệt");
        var normalizedNotes = NormalizeOptionalText(request.Notes, 1000, "Ghi chú");
        var normalizedCharacteristic = NormalizeOptionalText(request.Characteristic, 1000, "Đặc điểm tính cách");
        var childAgeGroup = ValidateAndGetChildAgeGroup(request.ChildAgeGroup);

        var parentProfile = await _parentRepo.FindByUserIdAsync(userId);
        if (parentProfile == null)
        {
            parentProfile = new ParentProfile { Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow, CreatedBy = userId };
            _parentRepo.Add(parentProfile);
            await _parentRepo.SaveChangesAsync();
        }

        var child = new ChildProfile
        {
            Id = Guid.NewGuid(),
            ParentProfileId = parentProfile.Id,
            SpecialNeeds = normalizedSpecialNeeds,
            Notes = normalizedNotes,
            Characteristic = normalizedCharacteristic,
            ChildAgeGroup = childAgeGroup,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _childRepo.Add(child);
        await _childRepo.SaveChangesAsync();

        await EnsureDeclaredChildrenCountAtLeastCreatedAsync(parentProfile, userId);

        return MapToChildDto(child);
    }

    public async Task<ChildProfileDto> UpdateChildProfileAsync(Guid userId, Guid childId, UpdateChildProfileRequest request)
    {
        var roles = await _userRepo.GetRolesAsync(userId);
        if (!roles.Any(r => r.Equals("parent", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chá»‰ dÃ nh cho Parent.");

        var normalizedSpecialNeeds = NormalizeOptionalText(request.SpecialNeeds, 1000, "Nhu cầu đặc biệt");
        var normalizedNotes = NormalizeOptionalText(request.Notes, 1000, "Ghi chú");
        var normalizedCharacteristic = NormalizeOptionalText(request.Characteristic, 1000, "Đặc điểm tính cách");
        var childAgeGroup = ValidateAndGetChildAgeGroup(request.ChildAgeGroup);

        var parentProfile = await _parentRepo.FindByUserIdAsync(userId)
            ?? throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y há»“ sÆ¡ Parent.");

        var child = await _childRepo.FindByIdAndParentAsync(childId, parentProfile.Id)
            ?? throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y con hoáº·c khÃ´ng cÃ³ quyá»n.");

        child.SpecialNeeds = normalizedSpecialNeeds;
        child.Notes = normalizedNotes;
        child.Characteristic = normalizedCharacteristic;
        child.ChildAgeGroup = childAgeGroup;
        child.UpdatedAt = DateTime.UtcNow;
        child.UpdatedBy = userId;

        _childRepo.Update(child);
        await _childRepo.SaveChangesAsync();

        return MapToChildDto(child);
    }

    public async Task DeleteChildProfileAsync(Guid userId, Guid childId)
    {
        var roles = await _userRepo.GetRolesAsync(userId);
        if (!roles.Any(r => r.Equals("parent", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Chá»‰ dÃ nh cho Parent.");

        var parentProfile = await _parentRepo.FindByUserIdAsync(userId)
            ?? throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y há»“ sÆ¡ Parent.");

        var child = await _childRepo.FindByIdAndParentAsync(childId, parentProfile.Id)
            ?? throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y con.");

        child.IsDeleted = true;
        child.UpdatedAt = DateTime.UtcNow;
        child.UpdatedBy = userId;

        _childRepo.Update(child);
        await _childRepo.SaveChangesAsync();
    }

    public Task<ParentProfile?> GetParentProfileByUserIdAsync(Guid userId) =>
        _parentRepo.FindByUserIdAsync(userId);

    public async Task<Guid?> GetParentProfileIdByUserIdAsync(Guid userId)
    {
        var p = await _parentRepo.FindByUserIdAsync(userId);
        return p?.Id;
    }

    public async Task UpdateParentOnboardingProfileAsync(Guid userId, UpdateParentProfileRequest request)
    {
        if (request.NumberOfChildren.HasValue && request.NumberOfChildren.Value < 1)
            throw new InvalidOperationException("So luong tre phai lon hon hoac bang 1.");

        var parentProfile = await _parentRepo.FindByUserIdAsync(userId);
        if (parentProfile == null)
        {
            parentProfile = new ParentProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            _parentRepo.Add(parentProfile);
        }

        var createdChildrenCount = (await _childRepo.GetByParentProfileIdAsync(parentProfile.Id)).Count;
        if (request.NumberOfChildren.HasValue && request.NumberOfChildren.Value < createdChildrenCount)
        {
            throw new InvalidOperationException(
                $"Khong the giam tong so tre xuong {request.NumberOfChildren.Value} vi ban da tao {createdChildrenCount} ho so tre.");
        }

        parentProfile.FamilyDescription = request.FamilyDescription;
        if (request.NumberOfChildren.HasValue)
            parentProfile.NumberOfChildren = request.NumberOfChildren.Value;
        else if (!parentProfile.NumberOfChildren.HasValue && createdChildrenCount > 0)
            parentProfile.NumberOfChildren = createdChildrenCount;

        parentProfile.UpdatedAt = DateTime.UtcNow;
        parentProfile.UpdatedBy = userId;

        await _parentRepo.SaveChangesAsync();
    }

    private ChildProfileDto MapToChildDto(ChildProfile c) => new ChildProfileDto
    {
        Id = c.Id,
        ParentProfileId = c.ParentProfileId,
        SpecialNeeds = c.SpecialNeeds,
        Notes = c.Notes,
        Characteristic = c.Characteristic,
        ChildAgeGroup = (byte?)c.ChildAgeGroup,
        CreatedAt = c.CreatedAt
    };

    private static byte ValidateAndGetChildAgeGroup(ChildAgeGroup? childAgeGroup)
    {
        if (!childAgeGroup.HasValue || !Enum.IsDefined(typeof(ChildAgeGroup), childAgeGroup.Value))
            throw new InvalidOperationException("Nhóm tuổi của trẻ không hợp lệ.");

        return (byte)childAgeGroup.Value;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new InvalidOperationException($"{fieldName} không được vượt quá {maxLength} ký tự.");

        return normalized;
    }

    private static string? NormalizePhoneNumber(string? phoneNumber) =>
        string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

    private static bool IsValidPhoneNumber(string phoneNumber) =>
        Regex.IsMatch(phoneNumber, @"^0\d{9}$");

    private static bool IsUniquePhoneConstraintViolation(DbUpdateException ex)
    {
        var sqlEx = ex.GetBaseException() as SqlException;
        if (sqlEx == null)
            return false;

        var isUniqueViolation = sqlEx.Number == 2601 || sqlEx.Number == 2627;
        if (!isUniqueViolation)
            return false;

        return sqlEx.Message.Contains("UQ_Users_PhoneNumber", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFriendlyDbUpdateMessage(DbUpdateException ex)
    {
        var sqlEx = ex.GetBaseException() as SqlException;
        if (sqlEx == null)
            return "Không thể lưu dữ liệu hồ sơ. Vui lòng kiểm tra lại thông tin.";

        if (IsUniquePhoneConstraintViolation(ex))
            return "Số điện thoại đã được đăng ký.";

        if ((sqlEx.Number == 2601 || sqlEx.Number == 2627)
            && sqlEx.Message.Contains("UQ_Users_Email", StringComparison.OrdinalIgnoreCase))
            return "Email đã được đăng ký.";

        if (sqlEx.Number == 515)
        {
            var (column, table) = TryExtractSqlColumnAndTable(sqlEx.Message);
            if (!string.IsNullOrWhiteSpace(column))
            {
                var target = string.IsNullOrWhiteSpace(table) ? column : $"{table}.{column}";
                return $"Thiếu dữ liệu bắt buộc: {target}. Vui lòng kiểm tra lại thông tin.";
            }

            return "Thiếu dữ liệu bắt buộc để lưu hồ sơ. Vui lòng kiểm tra lại các trường bắt buộc.";
        }

        if (sqlEx.Number == 8152 || sqlEx.Number == 2628)
            return "Một số trường vượt quá độ dài cho phép. Vui lòng kiểm tra họ tên, số điện thoại và địa chỉ.";

        if (sqlEx.Number == 547)
            return "Dữ liệu cập nhật không hợp lệ theo ràng buộc hệ thống.";

        return $"Không thể lưu dữ liệu hồ sơ (SQL {sqlEx.Number}).";
    }

    private static bool HasNannySpecificPayload(UpdatePersonalInfoRequest request) =>
        !string.IsNullOrWhiteSpace(request.Bio)
        || request.YearsOfExperience.HasValue
        || request.EducationLevel.HasValue
        || request.ExpectedSalaryMin.HasValue
        || request.ExpectedSalaryMax.HasValue
        || request.MaxTravelDistance.HasValue
        || (request.SkillIds?.Any(x => x != Guid.Empty) == true);

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

    private async Task EnsureDeclaredChildrenCountAtLeastCreatedAsync(ParentProfile parentProfile, Guid userId)
    {
        var activeChildrenCount = (await _childRepo.GetByParentProfileIdAsync(parentProfile.Id)).Count;
        if (activeChildrenCount <= 0)
            return;

        if (!parentProfile.NumberOfChildren.HasValue || parentProfile.NumberOfChildren.Value < activeChildrenCount)
        {
            parentProfile.NumberOfChildren = activeChildrenCount;
            parentProfile.UpdatedAt = DateTime.UtcNow;
            parentProfile.UpdatedBy = userId;
            await _parentRepo.SaveChangesAsync();
        }
    }

    // Background embedding helper
    private async Task EmbedNannyInBackgroundAsync(Guid nannyProfileId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var embedService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            await embedService.EmbedNannyAsync(nannyProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background re-embed thất bại cho NannyProfileId={NannyProfileId}", nannyProfileId);
        }
    }
}
