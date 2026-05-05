using Nanny_BackEnd.DTOs.Hiring;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IHiringService
{
    Task<List<JobApplicantDto>> GetApplicantsAsync(Guid jobPostingId, Guid parentUserId);
    Task<List<HiringRecordListItemDto>> GetMyHiringRecordsAsync(Guid userId);
    Task ApproveApplicantAsync(Guid jobPostingId, Guid jobAppId, Guid parentUserId);
    Task<NannyHireContextDto> GetNannyHireContextAsync(Guid jobPostingId, Guid jobAppId, Guid parentUserId);
    Task<HiringConfirmedDto> ConfirmHiringAsync(
        Guid jobPostingId, Guid jobAppId, Guid parentUserId, ConfirmHiringDto dto);
    Task<HiringConfirmedDto> ConfirmHiringByContactRequestAsync(
        Guid contactRequestId, Guid parentUserId, ConfirmHiringDto dto);
    Task<HiringOfferDetailDto> GetHiringOfferDetailAsync(Guid hiringRecordId, Guid currentUserId);
    Task CancelHiringRequestAsync(Guid hiringRecordId, Guid parentUserId);
    Task RespondHiringRequestAsync(Guid hiringRecordId, Guid nannyUserId, bool isAccepted);
    Task CompleteHiringAsync(Guid hiringRecordId, Guid parentUserId);
    Task<Guid> CreateContractForHiringAsync(Guid hiringRecordId, Guid parentUserId);
}
