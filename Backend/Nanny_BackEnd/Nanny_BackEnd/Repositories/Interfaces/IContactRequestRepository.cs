using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IContactRequestRepository
{
    Task<ContactRequest?> FindByParentAndNannyNotDeletedAsync(Guid parentProfileId, Guid nannyProfileId);
    void Add(ContactRequest entity);
    Task SaveChangesAsync();

    Task<(
        List<ContactRequest> Items,
        int Total,
        int Pending,
        int Accepted,
        int Rejected)> GetReceivedListForNannyAsync(Guid nannyProfileId, int? status);

    Task<(
        List<ContactRequest> Items,
        int Total,
        int Pending,
        int Accepted,
        int Rejected)> GetSentListForParentAsync(Guid parentProfileId, int? status);

    Task<ContactRequest?> GetByIdForDetailNoTrackingAsync(Guid contactRequestId);
    Task<ContactRequest?> GetByIdForNannyReviewTrackingAsync(Guid contactRequestId, Guid nannyProfileId);
}
