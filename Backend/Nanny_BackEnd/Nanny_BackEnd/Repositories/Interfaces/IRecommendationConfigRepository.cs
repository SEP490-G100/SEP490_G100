using Nanny_BackEnd.DTOs.Recommendation;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IRecommendationConfigRepository
{
    Task<ScoringWeights> GetWeightsAsync();
    Task UpdateWeightAsync(string key, double value, Guid updatedBy);
}
