using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class RecommendationService
{
    private readonly RecommendationRepository _repo;
    private readonly ILogger<RecommendationService> _logger;

    private const float ColdStartScore = 0.75f;

    public RecommendationService(
        RecommendationRepository repo,
        ILogger<RecommendationService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────
    // Top K nannies cho một job
    // ──────────────────────────────────────────────────────────────

    public async Task<List<NannyRecommendResultDto>> GetTopNanniesForJobAsync(
        Guid jobId,
        int topK = 5,
        double? jobLat = null,
        double? jobLng = null)
    {
        var candidates = await _repo.GetNannyCandidatesAsync(jobId);
        if (candidates.Count == 0) return new List<NannyRecommendResultDto>();

        // Lấy embedding của job
        var jobModel = await _repo.GetJobReadModelAsync(jobId);
        var jobVector = EmbeddingService.DeserializeEmbedding(jobModel?.Embedding);

        // Dùng toạ độ job (từ param hoặc từ DB)
        var jLat = jobLat ?? (double?)jobModel?.Latitude;
        var jLng = jobLng ?? (double?)jobModel?.Longitude;

        var results = new List<NannyRecommendResultDto>();

        foreach (var c in candidates)
        {
            // 1. Distance filter (Haversine in-memory)
            double? distKm = null;
            if (jLat.HasValue && jLng.HasValue && c.Latitude.HasValue && c.Longitude.HasValue)
            {
                distKm = HaversineKm(
                    (double)c.Latitude.Value, (double)c.Longitude.Value,
                    jLat.Value, jLng.Value);

                if (c.MaxTravelDistance.HasValue && distKm > c.MaxTravelDistance.Value)
                    continue; // nanny không đi xa tới đó
            }
            else if (c.MaxTravelDistance.HasValue)
            {
                // Không có toạ độ → không thể tính khoảng cách → giữ lại (conservative)
                distKm = null;
            }

            // 2. Cosine similarity
            var nannyVector = EmbeddingService.DeserializeEmbedding(c.Embedding);
            bool embeddingWasNull = nannyVector == null || jobVector == null;
            float cosine = embeddingWasNull
                ? ColdStartScore
                : CosineSimilarity(nannyVector!, jobVector!);

            // 3. Business boost
            double boost = ComputeBoost(c.AverageRating, distKm, c.MaxTravelDistance);

            double finalScore = cosine * boost;

            results.Add(new NannyRecommendResultDto
            {
                NannyProfileId = c.NannyProfileId,
                FullName = c.FullName,
                AvatarUrl = c.AvatarUrl,
                Bio = c.Bio,
                YearsOfExperience = c.YearsOfExperience,
                EducationLevel = c.EducationLevel,
                AverageRating = c.AverageRating,
                TotalReviews = c.TotalReviews,
                DistanceKm = distKm.HasValue ? Math.Round(distKm.Value, 2) : null,
                Skills = c.Skills,
                CosineScore = cosine,
                BusinessBoost = Math.Round(boost, 4),
                FinalScore = Math.Round(finalScore, 4),
                EmbeddingWasNull = embeddingWasNull
            });
        }

        return results
            .OrderByDescending(r => r.FinalScore)
            .Take(topK)
            .ToList();
    }

    // ──────────────────────────────────────────────────────────────
    // Top K jobs cho một nanny
    // ──────────────────────────────────────────────────────────────

    public async Task<List<JobRecommendResultDto>> GetTopJobsForNannyAsync(
        Guid nannyProfileId,
        int topK = 5)
    {
        var candidates = await _repo.GetJobCandidatesAsync(nannyProfileId);
        if (candidates.Count == 0) return new List<JobRecommendResultDto>();

        // Lấy embedding + toạ độ của nanny
        var nannyModel = await _repo.GetNannyReadModelAsync(nannyProfileId);
        var nannyVector = EmbeddingService.DeserializeEmbedding(nannyModel?.Embedding);
        var nLat = (double?)nannyModel?.Latitude;
        var nLng = (double?)nannyModel?.Longitude;
        var maxDist = nannyModel?.MaxTravelDistance;

        var results = new List<JobRecommendResultDto>();

        foreach (var j in candidates)
        {
            // 1. Distance filter (Haversine in-memory)
            double? distKm = null;
            if (nLat.HasValue && nLng.HasValue && j.Latitude.HasValue && j.Longitude.HasValue)
            {
                distKm = HaversineKm(
                    nLat.Value, nLng.Value,
                    (double)j.Latitude.Value, (double)j.Longitude.Value);

                if (maxDist.HasValue && distKm > maxDist.Value)
                    continue; // ngoài giới hạn đi lại
            }

            // 2. Cosine similarity
            var jobVector = EmbeddingService.DeserializeEmbedding(j.Embedding);
            bool embeddingWasNull = nannyVector == null || jobVector == null;
            float cosine = embeddingWasNull
                ? ColdStartScore
                : CosineSimilarity(nannyVector!, jobVector!);

            // 3. Business boost (không có rating của job → chỉ distance penalty)
            double boost = ComputeBoost(rating: null, distKm, maxDist);

            double finalScore = cosine * boost;

            results.Add(new JobRecommendResultDto
            {
                JobId = j.JobId,
                Title = j.Title,
                Description = j.Description,
                City = j.City,
                District = j.District,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                SalaryNegotiable = j.SalaryNegotiable,
                DistanceKm = distKm.HasValue ? Math.Round(distKm.Value, 2) : null,
                RequiredSkills = j.RequiredSkills,
                CosineScore = cosine,
                BusinessBoost = Math.Round(boost, 4),
                FinalScore = Math.Round(finalScore, 4),
                EmbeddingWasNull = embeddingWasNull
            });
        }

        return results
            .OrderByDescending(r => r.FinalScore)
            .Take(topK)
            .ToList();
    }

    // ──────────────────────────────────────────────────────────────
    // Business Boost
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// boost ∈ [0.85, 1.15]
    /// + Rating bonus:   quadratic scale, max +15% khi rating = 5.0
    /// - Distance penalty: ratio-based, max -15% khi dist = maxDist
    /// </summary>
    private static double ComputeBoost(
        decimal? rating,
        double? distKm,
        int? maxDist)
    {
        double boost = 1.0;

        // Rating bonus (max +15%)
        if (rating.HasValue && rating.Value >= 3.5m)
        {
            double normalized = ((double)rating.Value - 3.5) / 1.5; // 0→1
            boost += 0.15 * normalized * normalized;
        }

        // Distance penalty (max -15%)
        if (distKm.HasValue && maxDist.HasValue && maxDist.Value > 0)
        {
            double ratio = distKm.Value / maxDist.Value; // 0→1
            if (ratio > 0.5)
            {
                double over = ratio - 0.5; // 0→0.5
                boost -= 0.30 * over;      // max -15% khi ratio=1
            }
        }

        // Floor
        boost = Math.Max(0.85, boost);
        return boost;
    }

    // ──────────────────────────────────────────────────────────────
    // Math utilities
    // ──────────────────────────────────────────────────────────────

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0f;
        return (float)(dot / (Math.Sqrt(magA) * Math.Sqrt(magB)));
    }

    private static double HaversineKm(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
