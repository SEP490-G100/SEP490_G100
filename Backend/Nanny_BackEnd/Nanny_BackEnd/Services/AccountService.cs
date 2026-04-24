using System.Net.Mail;
using System.Text.RegularExpressions;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class AccountService : IAccountService
{
    private const string ModeratorRole = "Moderator";
    private static readonly string[] ExcludedRoles = ["Moderator", "Admin"];
    private static readonly string[] AllowedRoles = ["Parent", "Nanny"];
    private readonly IAccountRepository _accountRepository;

    public AccountService(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<AccountListResponse> AdminViewModeratorAccountListAsync(
        string? search,
        int? status,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (users, totalCount) = await _accountRepository.GetPagedModeratorAccountsAsync(
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
        var user = await _accountRepository.GetModeratorAccountWithRolesAsync(id);
        if (user == null)
            return (false, null, "Khong tim thay Moderator.");

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

        if (await _accountRepository.IsEmailInUseAsync(normalizedEmail))
            return (false, 409, "Email nay da duoc su dung.", null);

        var role = await _accountRepository.GetRoleByNameAsync(ModeratorRole);
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

        _accountRepository.AddUser(newUser);
        _accountRepository.AddUserRole(new UserRole
        {
            UserId = newUser.Id,
            RoleId = role.Id,
            CreatedAt = nowUtc,
            IsDeleted = false
        });

        await _accountRepository.SaveChangesAsync();
        return (true, 200, "Tao tai khoan Moderator thanh cong.", new { newUser.Id });
    }

    public async Task<(bool Success, int StatusCode, string Message)> AdminUpdateModeratorAccountAsync(
        Guid id,
        UpdateAccountStatusRequest request)
    {
        return await AdminToggleModeratorAccountAsync(id, request);
    }

    public async Task<(bool Success, int StatusCode, string Message)> AdminToggleModeratorAccountAsync(
        Guid id,
        UpdateAccountStatusRequest request)
    {
        if (request.Status is not (1 or 2))
            return (false, 400, "Status khong hop le.");

        var user = await _accountRepository.GetModeratorAccountWithRolesAsync(id);
        if (user == null)
            return (false, 404, "Khong tim thay Moderator.");

        ApplyModeratorActiveState(user, request.Status);
        user.UpdatedAt = DateTime.UtcNow;
        await _accountRepository.SaveChangesAsync();
        return (true, 200,
            request.Status == 1
                ? "Kich hoat tai khoan Moderator thanh cong."
                : "Vo hieu hoa tai khoan Moderator thanh cong.");
    }

    public async Task<AccountListResponse> ModeratorViewAccountListAsync(
        string? role,
        int? status,
        string? search,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 3;

        var (users, totalCount) = await _accountRepository.GetPagedAccountsAsync(
            role, status, search, page, pageSize, ExcludedRoles, AllowedRoles);

        var dtos = users.Select(u => new AccountDto
        {
            Id = u.Id,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            AvatarUrl = u.AvatarUrl,
            City = u.City,
            Status = u.Status,
            EmailConfirmed = u.EmailConfirmed,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            Roles = u.UserRoles
                .Where(ur => !ur.IsDeleted)
                .Select(ur => ur.Role.Name)
                .ToList()
        }).ToList();

        return new AccountListResponse
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Success, AccountDto? Data, string? Message)> ModeratorViewAccountDetailAsync(Guid id)
    {
        var user = await _accountRepository.GetAccountWithRolesAsync(id);

        if (user == null)
            return (false, null, "Khong tim thay tai khoan.");

        var dto = new AccountDto
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

        return (true, dto, null);
    }

    public async Task<(bool Success, int StatusCode, string Message, object? Data)> ModeratorToggleAccountStatusAsync(
        Guid id,
        UpdateAccountStatusRequest request)
    {
        if (request.Status is not (1 or 2))
            return (false, 400, "Status khong hop le. Chi chap nhan 1 (Active) hoac 2 (Inactive).", null);

        var user = await _accountRepository.FindByIdAsync(id);
        if (user == null)
            return (false, 404, "Khong tim thay tai khoan.", null);

        user.Status = request.Status;
        user.IsDeleted = request.Status == 2;
        user.UpdatedAt = DateTime.UtcNow;

        await _accountRepository.SaveChangesAsync();

        var message = request.Status == 1
            ? "Da kich hoat tai khoan."
            : "Da vo hieu hoa tai khoan.";

        return (true, 200, message, new { user.Id, user.Status });
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
        if (string.IsNullOrWhiteSpace(request.Password))
            return "Mat khau la bat buoc.";
        if (!IsStrongPassword(request.Password))
            return "Mat khau phai co it nhat 8 ky tu, bao gom chu, so va it nhat 1 ky tu dac biet.";
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return "FirstName la bat buoc.";
        if (!IsValidName(request.FirstName.Trim()))
            return "FirstName chi duoc chua chu cai.";
        if (string.IsNullOrWhiteSpace(request.LastName))
            return "LastName la bat buoc.";
        if (!IsValidName(request.LastName.Trim()))
            return "LastName chi duoc chua chu cai.";

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

    private static bool IsStrongPassword(string password) =>
        Regex.IsMatch(password, @"^(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$");

    private static bool IsValidName(string value) =>
        Regex.IsMatch(value, @"^[\p{L}\s]+$");

    private static string? NormalizePhone(string? phoneNumber) =>
        string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

    private static void ApplyModeratorActiveState(User user, int status)
    {
        if (status == 1)
        {
            user.Status = 1;
            user.IsDeleted = false;
            return;
        }

        user.Status = 2;
        user.IsDeleted = true;
    }
}
