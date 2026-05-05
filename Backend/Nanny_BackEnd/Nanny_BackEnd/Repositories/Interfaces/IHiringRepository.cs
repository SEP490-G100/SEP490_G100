using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IHiringRepository
{
    Task<JobPosting?> GetJobPostingByIdAsync(Guid jobPostingId);
    Task<List<JobApplication>> GetApplicantsByJobPostingIdAsync(Guid jobPostingId);
    Task<JobApplication?> GetJobApplicationByIdAsync(Guid id);
    Task<ContactRequest?> GetAcceptedContactRequestAsync(Guid contactRequestId);
    Task<HiringRecord?> GetHiringRecordByIdAsync(Guid id);
    Task<List<HiringRecord>> GetHiringRecordsByUserIdAsync(Guid userId);
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
    void AddNotification(Notification notification);
    Task SaveChangesAsync();
}
