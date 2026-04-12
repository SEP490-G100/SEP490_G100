using System.Net.Mail;
using System.Text.RegularExpressions;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class AdminAccountService
{
    private const string ModeratorRole = "Moderator";
    private readonly AdminAccountRepository _adminAccountRepository;

    public AdminAccountService(AdminAccountRepository adminAccountRepository)
    {
        _adminAccountRepository = adminAccountRepository;
    }

    public async Task<AccountListResponse> AdminViewModeratorAccountListAsync(
        string? search,
        int? status,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (users, totalCount) = await _adminAccountRepository.GetPagedModeratorAccountsAsync(
            search, status, page, pageSize);

        var items = users.Select(MapAccountDto).ToList();

        return new AccountListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Success, AccountDto? Data, string? Message)> AdminViewModeratorAccountDetailAsync(Guid id)
    {
        var user = await _adminAccountRepository.GetUserWithRolesAsync(id);
        if (user == null)
            return (false, null, "Khong tim thay Moderator.");

        var roles = user.UserRoles
            .Where(ur => !ur.IsDeleted)
            .Select(ur => ur.Role.Name)
            .ToList();

        if (!roles.Contains(ModeratorRole))
            return (false, null, "Tai khoan nay khong phai Moderator.");

        return (true, MapAccountDto(user), null);
    }

    public async Task<(bool Success, int StatusCode, string Message, object? Data)> AdminCreateModeratorAccountAsync(
        CreateModeratorRequest request)
    {
        var validationMessage = ValidateCreateRequest(request);
        if (validationMessage != null)
            return (false, 400, validationMessage, null);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = NormalizePhone(request.PhoneNumber);

        if (await _adminAccountRepository.IsEmailInUseAsync(normalizedEmail))
            return (false, 409, "Email nay da duoc su dung.", null);

        var role = await _adminAccountRepository.GetRoleByNameAsync(ModeratorRole);
        if (role == null)
            return (false, 500, "Role Moderator khong ton tai trong he thong.", null);

        var nowUtc = DateTime.UtcNow;
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = normalizedPhone,
            Status = 1,
            EmailConfirmed = true,
            AuthProvider = 0,
            CreatedAt = nowUtc,
            IsDeleted = false
        };

        _adminAccountRepository.AddUser(newUser);
        _adminAccountRepository.AddUserRole(new UserRole
        {
            UserId = newUser.Id,
            RoleId = role.Id,
            CreatedAt = nowUtc,
            IsDeleted = false
        });

        await _adminAccountRepository.SaveChangesAsync();
        return (true, 200, "Tao tai khoan Moderator thanh cong.", new { newUser.Id });
    }

    public async Task<(bool Success, int StatusCode, string Message)> AdminUpdateModeratorAccountAsync(
        Guid id,
        UpdateModeratorRequest request)
    {
        var user = await _adminAccountRepository.GetModeratorAccountWithRolesAsync(id);
        if (user == null)
            return (false, 404, "Khong tim thay Moderator.");

        var validationMessage = ValidateUpdateRequest(request);
        if (validationMessage != null)
            return (false, 400, validationMessage);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = NormalizePhone(request.PhoneNumber);
        user.Status = request.Status;
        user.UpdatedAt = DateTime.UtcNow;

        await _adminAccountRepository.SaveChangesAsync();
        return (true, 200, "Cap nhat Moderator thanh cong.");
    }

    public async Task<(bool Success, int StatusCode, string Message)> AdminDeleteModeratorAccountAsync(Guid id)
    {
        var user = await _adminAccountRepository.GetUserWithRolesAsync(id);
        if (user == null)
            return (false, 404, "Khong tim thay tai khoan.");

        var roles = user.UserRoles
            .Where(ur => !ur.IsDeleted)
            .Select(ur => ur.Role.Name)
            .ToList();

        if (!roles.Contains(ModeratorRole))
            return (false, 403, "Tai khoan nay khong phai Moderator.");

        await _adminAccountRepository.HardDeleteUserAsync(id);
        await _adminAccountRepository.SaveChangesAsync();
        return (true, 200, "Da xoa tai khoan Moderator.");
    }

    private static AccountDto MapAccountDto(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        AvatarUrl = user.AvatarUrl,
        DateOfBirth = user.DateOfBirth,
        Gender = user.Gender,
        Address = user.Address,
        City = user.City,
        District = user.District,
        Ward = user.Ward,
        Status = user.Status,
        EmailConfirmed = user.EmailConfirmed,
        CreatedAt = user.CreatedAt,
        CreatedBy = user.CreatedBy,
        UpdatedAt = user.UpdatedAt,
        UpdatedBy = user.UpdatedBy,
        LastLoginAt = user.LastLoginAt,
        Roles = user.UserRoles
            .Where(ur => !ur.IsDeleted)
            .Select(ur => ur.Role.Name)
            .ToList()
    };

    private static string? ValidateCreateRequest(CreateModeratorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return "Email la bat buoc.";
        if (!IsValidEmail(request.Email.Trim()))
            return "Email khong hop le.";
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return "Mat khau phai co it nhat 8 ky tu.";
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return "FirstName la bat buoc.";
        if (string.IsNullOrWhiteSpace(request.LastName))
            return "LastName la bat buoc.";

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !IsValidPhone(request.PhoneNumber.Trim()))
            return "So dien thoai phai la 10-11 chu so.";

        return null;
    }

    private static string? ValidateUpdateRequest(UpdateModeratorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return "FirstName la bat buoc.";
        if (string.IsNullOrWhiteSpace(request.LastName))
            return "LastName la bat buoc.";
        if (request.Status is not (0 or 1))
            return "Status khong hop le.";
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !IsValidPhone(request.PhoneNumber.Trim()))
            return "So dien thoai phai la 10-11 chu so.";

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidPhone(string phoneNumber) =>
        Regex.IsMatch(phoneNumber, @"^\d{10,11}$");

    private static string? NormalizePhone(string? phoneNumber) =>
        string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
}
