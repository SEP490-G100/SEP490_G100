using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IVerificationRequestRepository
{
    Task<(List<VerificationRequest> Items, int TotalCount)> GetModeratorListAsync(
        int? status,
        int? requestType,
        string? search,
        int page,
        int pageSize);
    Task<(List<VerificationRequest> Items, int TotalCount)> GetListAsync(
        int? status,
        string? search,
        int page,
        int pageSize);
    Task<VerificationRequest?> GetByIdAsync(Guid id);
    Task<NannyProfile?> GetNannyProfileAsync(Guid nannyProfileId);
    Task<NannyProfile?> GetNannyProfileByUserIdAsync(Guid userId);
    Task<List<VerificationRequest>> GetRequestsByNannyProfileAsync(Guid nannyProfileId);
    void AddRequest(VerificationRequest request);
    Task SaveChangesAsync();
}
