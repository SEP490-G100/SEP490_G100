using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class RecommendationService : IRecommendationService
{
    private readonly IRecommendationRepository _repo;
    private readonly IRecommendationConfigRepository _configRepo;
    private readonly IParentRepository _parentRepo;
    private readonly INannyProfileRepository _nannyRepo;
    private readonly IJobRepository _jobRepo;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IRecommendationRepository repo,
        IRecommendationConfigRepository configRepo,
        IParentRepository parentRepo,
        INannyProfileRepository nannyRepo,
        IJobRepository jobRepo,
        ISubscriptionService subscriptionService,
        ILogger<RecommendationService> logger)
    {
        _repo = repo;
        _configRepo = configRepo;
        _parentRepo = parentRepo;
        _nannyRepo = nannyRepo;
        _jobRepo = jobRepo;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────
    // Top K nannies cho một job
    // ──────────────────────────────────────────────────────────────

    public async Task<List<NannyRecommendResultDto>> GetTopNanniesForJobAsync(
        Guid jobId,
        int topK = 10,
        double? overrideLat = null,
        double? overrideLng = null)
    {
        var candidates = await _repo.GetNannyCandidatesAsync(jobId);
        if (candidates.Count == 0) return new List<NannyRecommendResultDto>();

        var w = await _configRepo.GetWeightsAsync();
        var jobModel = await _repo.GetJobReadModelAsync(jobId);
        var jobVector = EmbeddingService.DeserializeEmbedding(jobModel?.Embedding);

        // Dùng tọa độ từ client nếu có, fallback về tọa độ job trong DB
        var jLat = overrideLat ?? (double?)jobModel?.Latitude;
        var jLng = overrideLng ?? (double?)jobModel?.Longitude;
        var jobRequiredSkillIds = jobModel?.RequiredSkillIds ?? new List<Guid>();

        var results = new List<NannyRecommendResultDto>();

        foreach (var c in candidates)
        {
            // 1. Semantic score (cosine)
            var nannyVector = EmbeddingService.DeserializeEmbedding(c.Embedding);
            bool embeddingWasNull = nannyVector == null || jobVector == null;
            double semanticScore = embeddingWasNull
                ? w.ColdStart
                : CosineSimilarity(nannyVector!, jobVector!);

            // 2. Salary score — range overlap
            double salaryScore = CalcSalaryScore(
                c.ExpectedSalaryMin, c.ExpectedSalaryMax,
                jobModel?.SalaryMin, jobModel?.SalaryMax,
                jobModel?.SalaryNegotiable ?? false);

            // 3. Distance score — soft (no hard cutoff)
            double? distKm = null;
            if (jLat.HasValue && jLng.HasValue && c.Latitude.HasValue && c.Longitude.HasValue)
                distKm = HaversineKm(
                    (double)c.Latitude.Value, (double)c.Longitude.Value,
                    jLat.Value, jLng.Value);

            double distanceScore = CalcDistanceScore(distKm, c.MaxTravelDistance);

            // 4. Hybrid score — weights from DB config
            double hybridScore = w.Semantic  * semanticScore
                               + w.Salary    * salaryScore
                               + w.Distance  * distanceScore;

            // 5. Business boost (rating only)
            double boost = CalcBoost(c.AverageRating);
            double finalScore = Math.Min(1.0, hybridScore * boost);

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
                Latitude = c.Latitude.HasValue ? (double?)c.Latitude.Value : null,
                Longitude = c.Longitude.HasValue ? (double?)c.Longitude.Value : null,
                Skills = c.Skills,
                SemanticScore = Math.Round(semanticScore, 4),
                SkillScore = 0,
                SalaryScore = Math.Round(salaryScore, 4),
                DistanceScore = Math.Round(distanceScore, 4),
                HybridScore = Math.Round(hybridScore, 4),
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
        int topK = 10)
    {
        var candidates = await _repo.GetJobCandidatesAsync(nannyProfileId);
        if (candidates.Count == 0) return new List<JobRecommendResultDto>();

        var w = await _configRepo.GetWeightsAsync();
        var nannyModel = await _repo.GetNannyReadModelAsync(nannyProfileId);
        var nannyVector = EmbeddingService.DeserializeEmbedding(nannyModel?.Embedding);
        var nLat = (double?)nannyModel?.Latitude;
        var nLng = (double?)nannyModel?.Longitude;
        var maxDist = nannyModel?.MaxTravelDistance;

        var results = new List<JobRecommendResultDto>();

        foreach (var j in candidates)
        {
            // 1. Semantic score (cosine)
            var jobVector = EmbeddingService.DeserializeEmbedding(j.Embedding);
            bool embeddingWasNull = nannyVector == null || jobVector == null;
            double semanticScore = embeddingWasNull
                ? w.ColdStart
                : CosineSimilarity(nannyVector!, jobVector!);

            // 2. Salary score — range overlap
            double salaryScore = CalcSalaryScore(
                nannyModel?.ExpectedSalaryMin, nannyModel?.ExpectedSalaryMax,
                j.SalaryMin, j.SalaryMax,
                j.SalaryNegotiable);

            // 3. Distance score — soft (no hard cutoff)
            double? distKm = null;
            if (nLat.HasValue && nLng.HasValue && j.Latitude.HasValue && j.Longitude.HasValue)
                distKm = HaversineKm(
                    nLat.Value, nLng.Value,
                    (double)j.Latitude.Value, (double)j.Longitude.Value);

            double distanceScore = CalcDistanceScore(distKm, maxDist);

            // 4. Hybrid score — weights from DB config
            double hybridScore = w.Semantic  * semanticScore
                               + w.Salary    * salaryScore
                               + w.Distance  * distanceScore;

            // 5. Business boost (rating only — job không có rating)
            double boost = CalcBoost(rating: null);
            double finalScore = Math.Min(1.0, hybridScore * boost);

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
                Latitude = j.Latitude.HasValue ? (double?)j.Latitude.Value : null,
                Longitude = j.Longitude.HasValue ? (double?)j.Longitude.Value : null,
                RequiredSkills = j.RequiredSkills,
                SemanticScore = Math.Round(semanticScore, 4),
                SkillScore = 0,
                SalaryScore = Math.Round(salaryScore, 4),
                DistanceScore = Math.Round(distanceScore, 4),
                HybridScore = Math.Round(hybridScore, 4),
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

    public Task<NannyReadModelDto?> GetNannyReadModelForAdminAsync(Guid nannyProfileId) =>
        _repo.GetNannyReadModelAsync(nannyProfileId);

    public Task<JobReadModelDto?> GetJobReadModelForAdminAsync(Guid jobId) =>
        _repo.GetJobReadModelAsync(jobId);

    public Task<List<NannyReadModelDto>> GetPendingEmbedNanniesForAdminAsync() =>
        _repo.GetPendingEmbedNanniesAsync();

    public Task<List<JobReadModelDto>> GetPendingEmbedJobsForAdminAsync() =>
        _repo.GetPendingEmbedJobsAsync();

    public Task<ScoringWeights> GetRecommendationConfigWeightsAsync() =>
        _configRepo.GetWeightsAsync();

    public Task UpdateRecommendationConfigWeightAsync(string key, double value, Guid updatedBy) =>
        _configRepo.UpdateWeightAsync(key, value, updatedBy);

    public async Task<NanniesForJobGatingResult> ValidateNanniesForJobGatingAsync(
        Guid? userId,
        Guid jobId,
        bool isAdminOrModerator)
    {
        if (!userId.HasValue)
        {
            return new NanniesForJobGatingResult
            {
                IsAllowed = false,
                HttpStatus = 401,
                ErrorMessage = "Không xác định được người dùng."
            };
        }

        if (isAdminOrModerator)
        {
            return new NanniesForJobGatingResult { IsAllowed = true, HttpStatus = 200 };
        }

        var parent = await _parentRepo.FindByUserIdAsync(userId.Value);
        if (parent == null)
        {
            return new NanniesForJobGatingResult
            {
                IsAllowed = false,
                HttpStatus = 403,
                ErrorMessage = null
            };
        }

        var benefits = await _subscriptionService.getBenefitsForParentProfile(parent.Id);
        if (!benefits.CanUseRecommendation)
        {
            return new NanniesForJobGatingResult
            {
                IsAllowed = false,
                HttpStatus = 402,
                ErrorMessage = "Tính năng gợi ý AI yêu cầu gói Plus hoặc Pro."
            };
        }

        var jobExists = await _jobRepo.JobPostingExistsForParentAsync(jobId, parent.Id);
        if (!jobExists)
        {
            return new NanniesForJobGatingResult
            {
                IsAllowed = false,
                HttpStatus = 404,
                ErrorMessage = "Không tìm thấy job posting."
            };
        }

        return new NanniesForJobGatingResult { IsAllowed = true, HttpStatus = 200 };
    }

    public async Task<JobsForNannyGatingResult> ValidateJobsForNannyGatingAsync(Guid? userId)
    {
        if (!userId.HasValue)
        {
            return new JobsForNannyGatingResult
            {
                IsAllowed = false,
                HttpStatus = 401,
                ErrorMessage = "Không xác định được người dùng."
            };
        }

        var nanny = await _nannyRepo.FindByUserIdAsync(userId.Value);
        if (nanny == null)
        {
            return new JobsForNannyGatingResult
            {
                IsAllowed = false,
                HttpStatus = 404,
                ErrorMessage = "Không tìm thấy nanny profile."
            };
        }

        var benefits = await _subscriptionService.getBenefitsForNannyProfile(nanny.Id);
        if (!benefits.CanUseRecommendation)
        {
            return new JobsForNannyGatingResult
            {
                IsAllowed = false,
                HttpStatus = 402,
                ErrorMessage = "Tính năng gợi ý AI yêu cầu gói Plus hoặc Pro."
            };
        }

        return new JobsForNannyGatingResult
        {
            IsAllowed = true,
            HttpStatus = 200,
            NannyProfileId = nanny.Id
        };
    }

    // ──────────────────────────────────────────────────────────────
    // Score components
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Điểm lương: kỳ vọng bảo mẫu [nLo,nHi] so với mức tin [jLo,jHi].
    ///
    /// Ngữ nghĩa (nanny-centric): "Job này có thể trả bảo mẫu đến bao nhiêu % kỳ vọng?"
    ///   score = (min(jHi, nHi) − nLo) / (nHi − nLo)
    ///
    /// - jLo ≥ nHi  : job trả vượt toàn bộ trần kỳ vọng → 1.0
    ///                (ví dụ bảo mẫu kỳ vọng 8–11tr, tin 20–30tr)
    /// - jHi < nLo  : job không đạt sàn tối thiểu → soft penalty (0.05–0.35)
    /// - Overlap     : reachable = min(jHi, nHi) − nLo  → score = reachable / nannyRange
    ///                (ví dụ bảo mẫu 8–11tr, tin 10–30tr → reachable=3/3 → 1.0)
    /// </summary>
    private static double CalcSalaryScore(
        decimal? nannyMin, decimal? nannyMax,
        decimal? jobMin,   decimal? jobMax,
        bool salaryNegotiable)
    {
        if ((!nannyMin.HasValue && !nannyMax.HasValue) ||
            (!jobMin.HasValue  && !jobMax.HasValue))
            return 0.8;

        double nLo = (double)(nannyMin ?? nannyMax!.Value * 0.8m);
        double nHi = (double)(nannyMax ?? nannyMin!.Value * 1.2m);
        double jLo = (double)(jobMin   ?? jobMax!.Value  * 0.8m);
        double jHi = (double)(jobMax   ?? jobMin!.Value  * 1.2m);

        if (nLo > nHi) (nLo, nHi) = (nHi, nLo);
        if (jLo > jHi) (jLo, jHi) = (jHi, jLo);

        // Case 1: sàn tin đã ≥ trần kỳ vọng → job trả vượt hoàn toàn → 1.0
        if (jLo >= nHi)
        {
            return salaryNegotiable ? 1.0 : 1.0; // luôn 1.0 dù negotiable hay không
        }

        // Case 2: trần tin < sàn kỳ vọng → job không đủ → soft penalty
        if (jHi < nLo)
        {
            double gap   = (nLo - jHi) / Math.Max(1.0, nLo);
            double score = Math.Max(0.05, 0.35 - gap * 0.45);
            if (salaryNegotiable) score = Math.Min(1.0, score * 1.2);
            return score;
        }

        // Case 3: có giao cắt — đo từ sàn kỳ vọng bảo mẫu đến mức cao nhất job có thể trả
        // reachable = min(jHi, nHi) − nLo
        //   → nếu jHi ≥ nHi: job đáp ứng được toàn bộ kỳ vọng → 1.0
        //   → nếu jHi < nHi: job chỉ đáp ứng được một phần trên của kỳ vọng
        double reachable  = Math.Min(jHi, nHi) - nLo;
        double nannyRange = Math.Max(1.0, nHi - nLo);
        double score2     = reachable > 0
            ? Math.Min(1.0, reachable / nannyRange)
            : 0.4;

        if (salaryNegotiable) score2 = Math.Min(1.0, score2 * 1.2);
        return score2;
    }

    /// <summary>
    /// Distance score: soft — không hard cutoff.
    /// Trong MaxTravelDistance: 1.0 → 0.5. Vượt qua: giảm dần, floor 0.1.
    /// </summary>
    private static double CalcDistanceScore(double? distKm, int? maxDist)
    {
        if (!distKm.HasValue || !maxDist.HasValue || maxDist.Value == 0)
            return 0.8;

        double ratio = distKm.Value / maxDist.Value;

        if (ratio <= 1.0)
            return 1.0 - ratio * 0.5;

        return Math.Max(0.1, 0.5 - (ratio - 1.0) * 0.4);
    }

    /// <summary>
    /// Business boost: chỉ rating bonus (max +15%).
    /// boost ∈ [1.0, 1.15]
    /// </summary>
    private static double CalcBoost(decimal? rating)
    {
        double boost = 1.0;

        if (rating.HasValue && rating.Value >= 3.5m)
        {
            double normalized = ((double)rating.Value - 3.5) / 1.5;
            boost += 0.15 * normalized * normalized;
        }

        return boost;
    }

    // ──────────────────────────────────────────────────────────────
    // Math utilities
    // ──────────────────────────────────────────────────────────────

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
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
