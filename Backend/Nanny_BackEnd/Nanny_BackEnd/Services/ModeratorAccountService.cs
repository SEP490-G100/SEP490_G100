using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class ModeratorAccountService : IModeratorAccountService
{
    private static readonly string[] ExcludedRoles = ["Moderator", "Admin"];
    private static readonly string[] AllowedRoles = ["Parent", "Nanny"];

    private readonly IModeratorAccountRepository _moderatorAccountRepository;

    public ModeratorAccountService(IModeratorAccountRepository moderatorAccountRepository)
    {
        _moderatorAccountRepository = moderatorAccountRepository;
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

        var (users, totalCount) = await _moderatorAccountRepository.GetPagedAccountsAsync(
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
        var user = await _moderatorAccountRepository.GetAccountWithRolesAsync(id);

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

        var user = await _moderatorAccountRepository.FindByIdAsync(id);
        if (user == null)
            return (false, 404, "Khong tim thay tai khoan.", null);

        user.Status = request.Status;
        user.IsDeleted = request.Status == 2;
        user.UpdatedAt = DateTime.UtcNow;

        await _moderatorAccountRepository.SaveChangesAsync();

        var message = request.Status == 1
            ? "Da kich hoat tai khoan."
            : "Da vo hieu hoa tai khoan.";

        return (true, 200, message, new { user.Id, user.Status });
    }
}
