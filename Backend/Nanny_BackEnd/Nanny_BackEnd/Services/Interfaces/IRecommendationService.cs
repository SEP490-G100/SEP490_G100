using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Recommendation;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IRecommendationService
{
    Task<List<NannyRecommendResultDto>> GetTopNanniesForJobAsync(
        Guid jobId,
        int topK = 5,
        double? overrideLat = null,
        double? overrideLng = null);
    Task<List<JobRecommendResultDto>> GetTopJobsForNannyAsync(
        Guid nannyProfileId,
        int topK = 5);

    Task<NannyReadModelDto?> GetNannyReadModelForAdminAsync(Guid nannyProfileId);
    Task<JobReadModelDto?> GetJobReadModelForAdminAsync(Guid jobId);
    Task<List<NannyReadModelDto>> GetPendingEmbedNanniesForAdminAsync();
    Task<List<JobReadModelDto>> GetPendingEmbedJobsForAdminAsync();
    Task<ScoringWeights> GetRecommendationConfigWeightsAsync();
    Task UpdateRecommendationConfigWeightAsync(string key, double value, Guid updatedBy);
    Task<NanniesForJobGatingResult> ValidateNanniesForJobGatingAsync(
        Guid? userId,
        Guid jobId,
        bool isAdminOrModerator);
    Task<JobsForNannyGatingResult> ValidateJobsForNannyGatingAsync(Guid? userId);
}
