using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using System.Text.RegularExpressions;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

/// <summary>
/// Handles account management operations used by Moderator (and potentially Admin).
/// Works with Users and UserRoles tables via IUserRepository.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;

    public UserService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    private static readonly string[] ExcludedRoles = { "Moderator", "Admin" };
    private static readonly string[] AllowedRoles  = { "Parent", "Nanny" };

    /// <summary>
    /// Paginated list of Nanny/Parent accounts, excluding Moderator and Admin.
    /// </summary>
    public async Task<AccountListResponse> GetAccountsAsync(
        string? role,
        int? status,
        string? search,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 3;

        var (users, totalCount) = await _userRepo.GetPagedUsersAsync(
            role, status, search, page, pageSize, ExcludedRoles, AllowedRoles);

        var dtos = users.Select(u => new AccountDto
        {
            Id             = u.Id,
            FirstName      = u.FirstName,
            LastName       = u.LastName,
            Email          = u.Email,
            PhoneNumber    = u.PhoneNumber,
            AvatarUrl      = u.AvatarUrl,
            City           = u.City,
            Status         = u.Status,
            EmailConfirmed = u.EmailConfirmed,
            CreatedAt      = u.CreatedAt,
            LastLoginAt    = u.LastLoginAt,
            Roles          = u.UserRoles
                .Where(ur => !ur.IsDeleted)
                .Select(ur => ur.Role.Name)
                .ToList()
        }).ToList();

        return new AccountListResponse
        {
            Items      = dtos,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };
    }

    /// <summary>
    /// Get full detail of a single user account (with roles).
    /// </summary>
    public async Task<(bool Success, AccountDto? Data, string? Message)> GetAccountAsync(Guid id)
    {
        var user = await _userRepo.GetUserWithRolesAsync(id);

        if (user == null)
            return (false, null, "Không tìm thấy tài khoản.");

        var dto = new AccountDto
        {
            Id             = user.Id,
            FirstName      = user.FirstName,
            LastName       = user.LastName,
            Email          = user.Email,
            PhoneNumber    = user.PhoneNumber,
            AvatarUrl      = user.AvatarUrl,
            DateOfBirth    = user.DateOfBirth,
            Gender         = user.Gender,
            Address        = user.Address,
            City           = user.City,
            District       = user.District,
            Ward           = user.Ward,
            Status         = user.Status,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt      = user.CreatedAt,
            CreatedBy      = user.CreatedBy,
            UpdatedAt      = user.UpdatedAt,
            UpdatedBy      = user.UpdatedBy,
            LastLoginAt    = user.LastLoginAt,
            Roles          = user.UserRoles
                .Where(ur => !ur.IsDeleted)
                .Select(ur => ur.Role.Name)
                .ToList()
        };

        return (true, dto, null);
    }

   

    /// <summary>
    /// Update only the Status of a user account.
    /// </summary>
    public async Task<(bool Success, int StatusCode, string Message, object? Data)> UpdateStatusAsync(Guid id, UpdateAccountStatusRequest request)
    {
        if (request.Status != 0 && request.Status != 1)
            return (false, 400, "Status không hợp lệ. Chỉ chấp nhận 0 (Active) hoặc 1 (Inactive).", null);

        var user = await _userRepo.FindByIdAsync(id);
        if (user == null)
            return (false, 404, "Không tìm thấy tài khoản.", null);

        user.Status = request.Status;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepo.saveChanges();

        var message = request.Status == 0 ? "Đã kích hoạt tài khoản." : "Đã vô hiệu hóa tài khoản.";
        return (true, 200, message, new { user.Id, user.Status });
    }

    // ── Admin: Moderator account management ───────────────────────────────

    private static readonly string ModeratorRole = "Moderator";

    /// <summary>Paginated list of Moderator accounts.</summary>
    public async Task<AccountListResponse> GetModeratorsAsync(
        string? search, int? status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (users, totalCount) = await _userRepo.GetPagedUsersAsync(
            ModeratorRole,
            status,
            search,
            page,
            pageSize,
            Array.Empty<string>(),
            new[] { ModeratorRole });

        var items = users.Select(u => new AccountDto
        {
            Id             = u.Id,
            FirstName      = u.FirstName,
            LastName       = u.LastName,
            Email          = u.Email,
            PhoneNumber    = u.PhoneNumber,
            AvatarUrl      = u.AvatarUrl,
            Status         = u.Status,
            EmailConfirmed = u.EmailConfirmed,
            CreatedAt      = u.CreatedAt,
            LastLoginAt    = u.LastLoginAt,
            Roles          = u.UserRoles
                .Where(ur => !ur.IsDeleted)
                .Select(ur => ur.Role.Name)
                .ToList()
        }).ToList();

        return new AccountListResponse
        {
            Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize
        };
    }

    /// <summary>Get a single Moderator account by Id.</summary>
    public async Task<(bool Success, AccountDto? Data, string? Message)> GetModeratorAsync(Guid id)
    {
        // Load user + roles in one query, then verify they hold the Moderator role
        var user = await _userRepo.GetUserWithRolesAsync(id);

        if (user == null)
            return (false, null, "Không tìm thấy Moderator.");

        var roles = user.UserRoles.Where(ur => !ur.IsDeleted).Select(ur => ur.Role.Name).ToList();

        if (!roles.Contains(ModeratorRole))
            return (false, null, "Tài khoản này không phải Moderator.");

        var dto = new AccountDto
        {
            Id             = user.Id,
            FirstName      = user.FirstName,
            LastName       = user.LastName,
            Email          = user.Email,
            PhoneNumber    = user.PhoneNumber,
            AvatarUrl      = user.AvatarUrl,
            DateOfBirth    = user.DateOfBirth,
            Gender         = user.Gender,
            Address        = user.Address,
            City           = user.City,
            District       = user.District,
            Ward           = user.Ward,
            Status         = user.Status,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt      = user.CreatedAt,
            CreatedBy      = user.CreatedBy,
            UpdatedAt      = user.UpdatedAt,
            UpdatedBy      = user.UpdatedBy,
            LastLoginAt    = user.LastLoginAt,
            Roles          = roles
        };

        return (true, dto, null);
    }

    /// <summary>Create a new Moderator account.</summary>
    public async Task<(bool Success, int StatusCode, string Message, object? Data)> CreateModeratorAsync(
        CreateModeratorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return (false, 400, "Email không hợp lệ.", null);
        if (string.IsNullOrWhiteSpace(request.Password) ||
            !Regex.IsMatch(request.Password, @"^(?=.*[A-Z])(?=.*[^A-Za-z0-9]).{8,}$"))
            return (false, 400, "Mat khau phai co it nhat 8 ky tu, it nhat 1 ky tu in hoa va 1 ky tu dac biet.", null);
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            !Regex.IsMatch(request.PhoneNumber.Trim(), @"^\d{10,11}$"))
            return (false, 400, "Số điện thoại phải là 10-11 chữ số.", null);

        if (await _userRepo.IsEmailInUseAsync(request.Email))
            return (false, 409, "Email này đã được sử dụng.", null);

        var role = await _userRepo.GetRoleByNameAsync(ModeratorRole);
        if (role == null)
            return (false, 500, "Role Moderator không tồn tại trong hệ thống.", null);

        var newUser = new Models.User
        {
            Id             = Guid.NewGuid(),
            Email          = request.Email.Trim().ToLower(),
            PasswordHash   = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName      = request.FirstName.Trim(),
            LastName       = request.LastName.Trim(),
            PhoneNumber    = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Status         = 1,
            EmailConfirmed = true,
            AuthProvider   = 0,
            CreatedAt      = DateTime.UtcNow,
            IsDeleted      = false
        };
        _userRepo.Add(newUser);

        var userRole = new Models.UserRole
        {
            UserId = newUser.Id, RoleId = role.Id,
            CreatedAt = DateTime.UtcNow, IsDeleted = false
        };
        _userRepo.AddUserRole(userRole);

        await _userRepo.saveChanges();
        return (true, 200, "Tạo tài khoản Moderator thành công.", new { newUser.Id });
    }

  

}
