using Nanny_BackEnd.DTOs.Nanny;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IContactRequestService
{
    Task<ContactRequestEndpointResult> SendAsync(Guid userId, Guid nannyProfileId, string? message);
    Task<ContactRequestEndpointResult> GetReceivedAsync(Guid userId, int? status);
    Task<ContactRequestEndpointResult> GetSentAsync(Guid userId, int? status);
    Task<ContactRequestEndpointResult> GetDetailAsync(Guid userId, Guid contactRequestId, bool isParent, bool isNanny);
    Task<ContactRequestEndpointResult> ReviewAsync(
        Guid userId,
        Guid contactRequestId,
        int action,
        string? responseMessage);
}
