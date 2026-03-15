using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class ProfileService
{
    private readonly UserRepository _userRepo;
    private readonly ParentRepository _parentRepo;
    private readonly ChildRepository _childRepo;

    public ProfileService(UserRepository userRepo, ParentRepository parentRepo, ChildRepository childRepo)
    {
        _userRepo = userRepo;
        _parentRepo = parentRepo;
        _childRepo = childRepo;
    }

    public async Task<PersonalProfileDto> GetPersonalProfileAsync(Guid userId)
    {
        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        var roles = await _userRepo.GetRolesAsync(userId);

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

    public async Task<PersonalProfileDto> UpdatePersonalInfoAsync(Guid userId, UpdatePersonalInfoRequest request)
    {
        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        // Map data
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

        // Luôn cố gắng tự geocode lại theo địa chỉ (Address + City + District + Ward) khi cập nhật thông tin
        // để đảm bảo toạ độ luôn khớp với địa chỉ mới.
        var (lat, lng) = await Nanny_BackEnd.Helpers.GeoHelper.ResolveLatLngAsync(
            user.Address,
            user.City,
            user.District,
            user.Ward);

        if (lat.HasValue && lng.HasValue)
        {
            // Nếu geocode thành công thì ưu tiên dùng toạ độ tìm được
            user.Latitude = lat;
            user.Longitude = lng;
        }
        else if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            // Nếu geocode thất bại thì mới dùng toạ độ client gửi lên (nếu có)
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
            throw new UnauthorizedAccessException("Chỉ người dùng có vị trí Parent mới có thể xem.");

        var parentProfile = await _parentRepo.FindByUserIdAsync(userId);
        if (parentProfile == null) return new();

        var children = await _childRepo.GetByParentProfileIdAsync(parentProfile.Id);

        return children.Select(c => new ChildProfileDto
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
        }).ToList();
    }

    public async Task<ChildProfileDto> CreateChildProfileAsync(Guid userId, CreateChildProfileRequest request)
    {
        var roles = await _userRepo.GetRolesAsync(userId);
        if (!roles.Any(r => r.ToLower() == "parent"))
            throw new UnauthorizedAccessException("Chỉ dành cho Parent.");

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
            Name = request.Name,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            SpecialNeeds = request.SpecialNeeds,
            Allergies = request.Allergies,
            Notes = request.Notes,
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
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ Parent.");

        var child = await _childRepo.FindByIdAndParentAsync(childId, parentProfile.Id)
            ?? throw new InvalidOperationException("Không tìm thấy con hoặc không có quyền.");

        child.Name = request.Name;
        child.DateOfBirth = request.DateOfBirth;
        child.Gender = request.Gender;
        child.SpecialNeeds = request.SpecialNeeds;
        child.Allergies = request.Allergies;
        child.Notes = request.Notes;
        child.UpdatedAt = DateTime.UtcNow;
        child.UpdatedBy = userId;

        _childRepo.Update(child);
        await _childRepo.SaveChangesAsync();

        return MapToChildDto(child);
    }

    public async Task DeleteChildProfileAsync(Guid userId, Guid childId)
    {
        var parentProfile = await _parentRepo.FindByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ Parent.");

        var child = await _childRepo.FindByIdAndParentAsync(childId, parentProfile.Id)
            ?? throw new InvalidOperationException("Không tìm thấy con.");

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
        Name = c.Name,
        DateOfBirth = c.DateOfBirth,
        Gender = c.Gender,
        SpecialNeeds = c.SpecialNeeds,
        Allergies = c.Allergies,
        Notes = c.Notes,
        CreatedAt = c.CreatedAt
    };
}