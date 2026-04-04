using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class ProfileService
{
    private readonly UserRepository _userRepo;
    private readonly ParentRepository _parentRepo;
    private readonly ChildRepository _childRepo;
    private readonly NannyProfileRepository _nannyProfileRepo;
    private readonly NannySkillRepository _nannySkillRepo;
    private readonly NannyCertificateRepository _nannyCertificateRepo;
    private readonly NannyAvailabilityRepository _nannyAvailabilityRepo;
    private readonly IWebHostEnvironment _env;
    private readonly GeocodingService _geo;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        UserRepository userRepo,
        ParentRepository parentRepo,
        ChildRepository childRepo,
        NannyProfileRepository nannyProfileRepo,
        NannySkillRepository nannySkillRepo,
        NannyCertificateRepository nannyCertificateRepo,
        NannyAvailabilityRepository nannyAvailabilityRepo,
        IWebHostEnvironment env,
        GeocodingService geo,
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
        List<NannySkillItemDto>? skills = null;
        List<NannyAvailabilityItemDto>? availabilities = null;
        List<NannyCertificateItemDto>? certificates = null;

        if (isNanny)
        {
            var nannyProfile = await _nannyProfileRepo.FindByUserIdAsync(userId);
            if (nannyProfile != null)
            {
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
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            DateOfBirth = user.DateOfBirth,
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
            Certificates = certificates
        };
    }

    public async Task<PersonalProfileDto> UpdatePersonalInfoAsync(Guid userId, UpdatePersonalInfoRequest request)
    {
        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("NgÆ°á»i dÃ¹ng khÃ´ng tá»“n táº¡i.");

        var roles = await _userRepo.GetRolesAsync(userId);
        var isNanny = roles.Any(r => r.Equals("nanny", StringComparison.OrdinalIgnoreCase));
        if (isNanny)
        {
            var dobToValidate = request.DateOfBirth ?? user.DateOfBirth;
            if (!dobToValidate.HasValue)
                throw new InvalidOperationException("Nanny pháº£i nháº­p ngÃ ,áy sinh.");

            var today = DateOnly.FromDateTime(DateTime.Today);
            var dob = dobToValidate.Value;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            if (age < 18)
                throw new InvalidOperationException("Nanny phải đủ 18 tuổi trở lên.");
        }

        // Map required core fields
        user.FirstName = request.FirstName?.Trim() ?? user.FirstName;
        user.LastName = request.LastName?.Trim() ?? user.LastName;

        // Partial update for optional fields: keep old values when input is empty
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            user.PhoneNumber = request.PhoneNumber.Trim();
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

        // Geocode like job posting (full location -> city/district fallback).
        var locationForGeo = string.Join(", ",
            new[] { user.Address, user.Ward }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));

        var coords = await _geo.geocode(locationForGeo, user.City, user.District);
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
            var nannyProfile = await _nannyProfileRepo.FindByUserIdAsync(userId);
            if (nannyProfile == null)
            {
                nannyProfile = new NannyProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    VerificationStatus = (int)Enums.VerificationStatus.NotSubmitted
                };
                _nannyProfileRepo.Add(nannyProfile);
            }

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

        await _userRepo.SaveChangesAsync();

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
        if (!roles.Any(r => r.ToLower() == "parent"))
            throw new UnauthorizedAccessException("Chá»‰ dÃ nh cho Parent.");

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
            SpecialNeeds = request.SpecialNeeds,
            Notes = request.Notes,
            Characteristic = request.Characteristic,
            ChildAgeGroup = (byte)request.ChildAgeGroup,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _childRepo.Add(child);
        await _childRepo.SaveChangesAsync();

        return MapToChildDto(child);
    }

    public async Task<ChildProfileDto> UpdateChildProfileAsync(Guid userId, Guid childId, UpdateChildProfileRequest request)
    {
        var parentProfile = await _parentRepo.FindByUserIdAsync(userId)
            ?? throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y há»“ sÆ¡ Parent.");

        var child = await _childRepo.FindByIdAndParentAsync(childId, parentProfile.Id)
            ?? throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y con hoáº·c khÃ´ng cÃ³ quyá»n.");

        child.SpecialNeeds = request.SpecialNeeds;
        child.Notes = request.Notes;
        child.Characteristic = request.Characteristic;
        child.ChildAgeGroup = (byte)request.ChildAgeGroup;
        child.UpdatedAt = DateTime.UtcNow;
        child.UpdatedBy = userId;

        _childRepo.Update(child);
        await _childRepo.SaveChangesAsync();

        return MapToChildDto(child);
    }

    public async Task DeleteChildProfileAsync(Guid userId, Guid childId)
    {
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
    
    // Background embedding helper
    private async Task EmbedNannyInBackgroundAsync(Guid nannyProfileId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var embedService = scope.ServiceProvider.GetRequiredService<EmbeddingService>();
            await embedService.EmbedNannyAsync(nannyProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background re-embed thất bại cho NannyProfileId={NannyProfileId}", nannyProfileId);
        }
    }
}
