using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IHiringRepository
{
    Task<List<ContractTemplate>> GetActiveContractTemplatesAsync();
    Task<ContractTemplate?> GetActiveContractTemplateByIdAsync(Guid id);
    Task<JobPosting?> GetJobPostingByIdAsync(Guid jobPostingId);
    Task<List<JobApplication>> GetApplicantsByJobPostingIdAsync(Guid jobPostingId);
    Task<JobApplication?> GetJobApplicationByIdAsync(Guid id);
    Task<ContactRequest?> GetAcceptedContactRequestAsync(Guid contactRequestId);
    Task<List<JobApplication>> GetOtherActiveApplicantsAsync(Guid jobPostingId, Guid excludedJobAppId);
    Task<List<JobApplication>> GetOtherPendingApplicantsAsync(Guid jobPostingId, Guid excludedJobAppId);
    Task<HiringRecord?> GetHiringRecordByIdAsync(Guid id);
    Task<List<HiringRecord>> GetCompletedUnreviewedHiringsForParentAsync(
        Guid parentUserId,
        IReadOnlyCollection<Guid> reviewedHiringRecordIds);
    Task<HiringRecord?> GetLatestHiringRecordByJobApplicationIdAsync(Guid jobApplicationId);
    void AddHiringRecord(HiringRecord hiringRecord);
    void AddJobPosting(JobPosting jobPosting);
    void AddJobApplication(JobApplication jobApplication);
    Task<Contract?> GetContractByHiringRecordIdAsync(Guid hiringRecordId);
    void AddContract(Contract contract);
    Task<ParentProfile?> GetParentProfileByUserIdAsync(Guid userId);
    Task<Conversation?> FindOneToOneConversationAsync(Guid userA, Guid userB);
    void AddConversation(Conversation conversation);
    void AddConversationParticipant(ConversationParticipant participant);
    void AddMessage(Message message);
    void AddNotification(Notification notification);
    Task SaveChangesAsync();
}
