using Nanny_BackEnd.DTOs.Account;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IAccountService
{
    Task<AccountListResponse> AdminViewModeratorAccountListAsync(
        string? search,
        int? status,
        int page,
        int pageSize);
    Task<(bool Success, AccountDto? Data, string? Message)> AdminViewModeratorAccountDetailAsync(Guid id);
    Task<(bool Success, int StatusCode, string Message, object? Data)> AdminCreateModeratorAccountAsync(
        CreateModeratorRequest request);
    Task<(bool Success, int StatusCode, string Message)> AdminUpdateModeratorAccountAsync(
        Guid id,
        UpdateAccountStatusRequest request);
    Task<(bool Success, int StatusCode, string Message)> AdminToggleModeratorAccountAsync(
        Guid id,
        UpdateAccountStatusRequest request);
    Task<AccountListResponse> ModeratorViewAccountListAsync(
        string? role,
        int? status,
        string? search,
        int page,
        int pageSize);
    Task<(bool Success, AccountDto? Data, string? Message)> ModeratorViewAccountDetailAsync(Guid id);
    Task<(bool Success, int StatusCode, string Message, object? Data)> ModeratorToggleAccountStatusAsync(
        Guid id,
        UpdateAccountStatusRequest request);
}
