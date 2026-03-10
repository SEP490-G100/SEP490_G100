using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Models;
using Microsoft.EntityFrameworkCore;

namespace Nanny_BackEnd.Services;

public class ProfileService
{
    private readonly Sep490NannyDbContext _context;

    public ProfileService(Sep490NannyDbContext context)
    {
        _context = context;
    }

    // View personal profile
    public async Task<PersonalProfileDto> GetPersonalProfileAsync(Guid userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        // Get user roles
        var roles = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

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
            Roles = roles
        };
    }

    // Update personal information
    public async Task<PersonalProfileDto> UpdatePersonalInfoAsync(Guid userId, UpdatePersonalInfoRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.AvatarUrl = request.AvatarUrl;
        user.DateOfBirth = request.DateOfBirth;
        user.Gender = request.Gender;
        user.Address = request.Address;
        user.City = request.City;
        user.District = request.District;
        user.Ward = request.Ward;
        user.Latitude = request.Latitude;
        user.Longitude = request.Longitude;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = userId;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return await GetPersonalProfileAsync(userId);
    }

    // Check if user has Parent role
    public async Task<bool> IsParentAsync(Guid userId)
    {
        return await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.Role.Name.ToLower() == "parent");
    }

    // Get child profiles list (only for Parent)
    public async Task<List<ChildProfileDto>> GetChildProfilesAsync(Guid userId)
    {
        var isParent = await IsParentAsync(userId);
        if (!isParent)
            throw new UnauthorizedAccessException("Chỉ người dùng có vị trí Parent mới có thể xem danh sách con em.");

        var parentProfile = await _context.ParentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

        if (parentProfile == null)
            return new();

        var childProfiles = await _context.ChildProfiles
            .AsNoTracking()
            .Where(c => c.ParentProfileId == parentProfile.Id && !c.IsDeleted)
            .Select(c => new ChildProfileDto
            {
                Id = c.Id,
                ParentProfileId = c.ParentProfileId,
                Name = c.Name,
                DateOfBirth = c.DateOfBirth,
                Gender = c.Gender,
                SpecialNeeds = c.SpecialNeeds,
                Allergies = c.Allergies,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return childProfiles;
    }

    // Create child profile (only for Parent)
    public async Task<ChildProfileDto> CreateChildProfileAsync(Guid userId, CreateChildProfileRequest request)
    {
        var isParent = await IsParentAsync(userId);
        if (!isParent)
            throw new UnauthorizedAccessException("Chỉ người dùng có vị trí Parent mới có thể thêm con em.");

        // Get or create ParentProfile
        var parentProfile = await _context.ParentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
        
        if (parentProfile == null)
        {
            parentProfile = new ParentProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            _context.ParentProfiles.Add(parentProfile);
            await _context.SaveChangesAsync();
        }

        var childProfile = new ChildProfile
        {
            Id = Guid.NewGuid(),
            ParentProfileId = parentProfile.Id,
            Name = request.Name,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            SpecialNeeds = request.SpecialNeeds,
            Allergies = request.Allergies,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.ChildProfiles.Add(childProfile);
        await _context.SaveChangesAsync();

        return new ChildProfileDto
        {
            Id = childProfile.Id,
            ParentProfileId = childProfile.ParentProfileId,
            Name = childProfile.Name,
            DateOfBirth = childProfile.DateOfBirth,
            Gender = childProfile.Gender,
            SpecialNeeds = childProfile.SpecialNeeds,
            Allergies = childProfile.Allergies,
            Notes = childProfile.Notes,
            CreatedAt = childProfile.CreatedAt
        };
    }

    // Update child profile (only for Parent)
    public async Task<ChildProfileDto> UpdateChildProfileAsync(Guid userId, Guid childId, UpdateChildProfileRequest request)
    {
        var isParent = await IsParentAsync(userId);
        if (!isParent)
            throw new UnauthorizedAccessException("Chỉ người dùng có vị trí Parent mới có thể cập nhật thông tin con em.");

        var parentProfile = await _context.ParentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ Parent của người dùng.");

        var childProfile = await _context.ChildProfiles
            .FirstOrDefaultAsync(c => c.Id == childId && c.ParentProfileId == parentProfile.Id && !c.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ con em hoặc bạn không có quyền chỉnh sửa.");

        childProfile.Name = request.Name;
        childProfile.DateOfBirth = request.DateOfBirth;
        childProfile.Gender = request.Gender;
        childProfile.SpecialNeeds = request.SpecialNeeds;
        childProfile.Allergies = request.Allergies;
        childProfile.Notes = request.Notes;
        childProfile.UpdatedAt = DateTime.UtcNow;
        childProfile.UpdatedBy = userId;

        _context.ChildProfiles.Update(childProfile);
        await _context.SaveChangesAsync();

        return new ChildProfileDto
        {
            Id = childProfile.Id,
            ParentProfileId = childProfile.ParentProfileId,
            Name = childProfile.Name,
            DateOfBirth = childProfile.DateOfBirth,
            Gender = childProfile.Gender,
            SpecialNeeds = childProfile.SpecialNeeds,
            Allergies = childProfile.Allergies,
            Notes = childProfile.Notes,
            CreatedAt = childProfile.CreatedAt
        };
    }

    // Delete child profile (only for Parent)
    public async Task DeleteChildProfileAsync(Guid userId, Guid childId)
    {
        var isParent = await IsParentAsync(userId);
        if (!isParent)
            throw new UnauthorizedAccessException("Chỉ người dùng có vị trí Parent mới có thể xóa con em.");

        var parentProfile = await _context.ParentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ Parent của người dùng.");

        var childProfile = await _context.ChildProfiles
            .FirstOrDefaultAsync(c => c.Id == childId && c.ParentProfileId == parentProfile.Id && !c.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ con em hoặc bạn không có quyền xóa.");

        childProfile.IsDeleted = true;
        childProfile.UpdatedAt = DateTime.UtcNow;
        childProfile.UpdatedBy = userId;

        _context.ChildProfiles.Update(childProfile);
        await _context.SaveChangesAsync();
    }
}
