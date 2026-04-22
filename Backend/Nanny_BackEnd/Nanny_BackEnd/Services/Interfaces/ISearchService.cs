using System;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Search;

namespace Nanny_BackEnd.Services.Interfaces;

public interface ISearchService
{
    Task<Guid?> GetNannyProfileIdByUserIdAsync(Guid userId);
    Task<ApplyToJobServiceResult> ApplyToJobAsync(Guid userId, Guid jobPostingId);
    Task<NannyMyApplicationsListResult?> GetMyApplicationsAsync(Guid userId, int page, int pageSize);
    Task<WithdrawApplicationFailureResult> WithdrawApplicationAsync(Guid userId, Guid applicationId);
    Task<(GetParentJobApplicationsFailure? Error, string? ErrorMessage, ParentJobApplicationsListResult? Data)>
        GetJobApplicationsForParentAsync(Guid userId, Guid jobPostingId, int? status);
    Task<ReviewJobApplicationServiceResult> ReviewJobApplicationAsync(
        Guid userId, Guid applicationId, ReviewJobApplicationRequestDto? request);
}
