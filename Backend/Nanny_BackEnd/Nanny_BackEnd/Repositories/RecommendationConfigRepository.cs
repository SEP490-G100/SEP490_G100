using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories.Interfaces;

namespace Nanny_BackEnd.Repositories;

public class RecommendationConfigRepository : IRecommendationConfigRepository
{
    private readonly Sep490NannyDbContext _db;
    private readonly IMemoryCache _cache;

    private const string CacheKey = "rec:scoring_weights";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public RecommendationConfigRepository(Sep490NannyDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<ScoringWeights> GetWeightsAsync()
    {
        if (_cache.TryGetValue(CacheKey, out ScoringWeights? cached) && cached != null)
            return cached;

        var settings = await _db.SystemSettings
            .Where(s => s.Key.StartsWith("rec:") && !s.IsDeleted)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var weights = new ScoringWeights(
            Semantic:  Parse(settings, "rec:semantic_weight",  0.80),
            Salary:    Parse(settings, "rec:salary_weight",    0.12),
            Distance:  Parse(settings, "rec:distance_weight",  0.08),
            ColdStart: Parse(settings, "rec:cold_start_score", 0.75)
        );

        _cache.Set(CacheKey, weights, CacheTtl);
        return weights;
    }

    public async Task UpdateWeightAsync(string key, double value, Guid updatedBy)
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key && !s.IsDeleted)
            ?? throw new InvalidOperationException($"Config key '{key}' not found.");

        setting.Value = value.ToString("F4", CultureInfo.InvariantCulture);
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedBy = updatedBy;
        await _db.SaveChangesAsync();

        _cache.Remove(CacheKey);
    }

    private static double Parse(Dictionary<string, string> d, string key, double defaultValue) =>
        d.TryGetValue(key, out var v) &&
        double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r)
            ? r : defaultValue;
}
