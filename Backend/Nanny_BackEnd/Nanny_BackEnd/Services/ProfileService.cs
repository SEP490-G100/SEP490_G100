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
    private readonly NannyAvailabilityRepository _nannyAvailabilityRepo;
    private readonly IWebHostEnvironment _env;
    private readonly GeocodingService _geo;

    public ProfileService(
        UserRepository userRepo,
        ParentRepository parentRepo,
        ChildRepository childRepo,
        NannyProfileRepository nannyProfileRepo,
        NannySkillRepository nannySkillRepo,
        NannyAvailabilityRepository nannyAvailabilityRepo,
        IWebHostEnvironment env,
        GeocodingService geo)
    {
        _userRepo = userRepo;
        _parentRepo = parentRepo;
        _childRepo = childRepo;
        _nannyProfileRepo = nannyProfileRepo;
        _nannySkillRepo = nannySkillRepo;
        _nannyAvailabilityRepo = nannyAvailabilityRepo;
        _env = env;
        _geo = geo;
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
        List<NannySkillItemDto>? skills = null;
        List<NannyAvailabilityItemDto>? availabilities = null;

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
                averageRating = nannyProfile.AverageRating;
                totalReviews = nannyProfile.TotalReviews;

                verificationStatus = nannyProfile.VerificationStatus switch
                {
                    Enums.VerificationStatus.NotSubmitted => "ChÆ°a Ä‘Æ°á»£c xÃ¡c thá»±c",
                    Enums.VerificationStatus.Pending => "Äang chá» xÃ¡c thá»±c",
                    Enums.VerificationStatus.Approved => "ÄÃ£ Ä‘Æ°á»£c xÃ¡c thá»±c",
                    Enums.VerificationStatus.Rejected => "Bá»‹ tá»« chá»‘i xÃ¡c thá»±c",
                    _ => "ChÆ°a Ä‘Æ°á»£c xÃ¡c thá»±c"
                };

                var nannySkills = await _nannySkillRepo.GetByNannyProfileIdAsync(nannyProfile.Id);
                skills = nannySkills
                    .Select(s => new NannySkillItemDto
                    {
                        SkillId = s.SkillId,
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
            AverageRating = averageRating,
            TotalReviews = totalReviews,
            Skills = skills,
            Availabilities = availabilities
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
            if (!request.DateOfBirth.HasValue)
                throw new InvalidOperationException("Nanny pháº£i nháº­p ngÃ y sinh.");

            var today = DateOnly.FromDateTime(DateTime.Today);
            var dob = request.DateOfBirth.Value;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            if (age < 18)
                throw new InvalidOperationException("Nanny pháº£i Ä‘á»§ 18 tuá»•i trá»Ÿ lÃªn.");
        }

        // Map data
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
            user.AvatarUrl = request.AvatarUrl;
        user.DateOfBirth = request.DateOfBirth;
        user.Gender = request.Gender;
        user.Address = request.Address;
        user.City = request.City;
        user.District = request.District;
        user.Ward = request.Ward;

        // Geocode like job posting (full location -> city/district fallback).
        var locationForGeo = string.Join(", ",
            new[] { request.Address, request.Ward }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));

        var coords = await _geo.geocode(locationForGeo, request.City, request.District);
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

        await _userRepo.SaveChangesAsync();
        return await GetPersonalProfileAsync(userId);
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
            ChildAgeGroup = request.ChildAgeGroup,
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
        child.ChildAgeGroup = request.ChildAgeGroup;
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
}

