using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IModeratorVerificationRepository
{
    Task<(List<VerificationRequest> Items, int TotalCount)> GetListAsync(
        int? status,
        int? requestType,
        string? search,
        int page,
        int pageSize);
    Task<VerificationRequest?> GetByIdAsync(Guid id);
    Task<NannyProfile?> GetNannyProfileAsync(Guid nannyProfileId);
    Task SaveChangesAsync();
}
