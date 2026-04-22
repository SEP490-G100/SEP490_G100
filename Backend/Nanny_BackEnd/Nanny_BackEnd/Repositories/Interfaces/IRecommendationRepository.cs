using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IRecommendationRepository
{
    Task<List<RecommendationRepository.NannyCandidate>> GetNannyCandidatesAsync(Guid jobId);
    Task<List<RecommendationRepository.JobCandidate>> GetJobCandidatesAsync(Guid nannyProfileId);
    Task<NannyReadModelDto?> GetNannyReadModelAsync(Guid nannyProfileId);
    Task<JobReadModelDto?> GetJobReadModelAsync(Guid jobId);
    Task<List<NannyReadModelDto>> GetAllNannyReadModelsAsync();
    Task<List<JobReadModelDto>> GetAllJobReadModelsAsync();
    Task<List<NannyReadModelDto>> GetPendingEmbedNanniesAsync();
    Task<List<JobReadModelDto>> GetPendingEmbedJobsAsync();
    Task SaveNannyEmbeddingAsync(Guid nannyProfileId, string embeddingJson);
    Task SaveJobEmbeddingAsync(Guid jobId, string embeddingJson);
}
