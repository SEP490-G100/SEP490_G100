using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;

namespace WebSite.Controllers;

[Authorize]
public class RecommendationController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public RecommendationController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    private void SetAuthHeader()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = !string.IsNullOrEmpty(token)
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    // ── AJAX: Nanny xem top K job gợi ý ─────────────────────
    // GET /Recommendation/JobsData?topK=5
    [Authorize(Roles = "Nanny")]
    [HttpGet]
    public async Task<IActionResult> JobsData([FromQuery] int topK = 5)
    {
        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync($"/api/recommendation/jobs-for-nanny?topK={topK}");
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return Json(new { success = false, requiresUpgrade = true, data = Array.Empty<object>() });
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, data = Array.Empty<object>() });

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<List<JobRecommendItem>>>(json, JsonOpts);

            // Map sang shape đơn giản cho UI — không expose score kỹ thuật
            var items = (result?.Data ?? new()).Select(j => new
            {
                jobId       = j.JobId,
                title       = j.Title,
                city        = j.City,
                district    = j.District,
                salaryMin   = j.SalaryMin,
                salaryMax   = j.SalaryMax,
                salaryNegotiable = j.SalaryNegotiable,
                distanceKm  = j.DistanceKm,
                requiredSkills = j.RequiredSkills?.Select(s => s.SkillName).ToList(),
                finalScore  = j.FinalScore,
                matchLabel  = MatchLabel(j.FinalScore),
                matchTier   = MatchTier(j.FinalScore)   // "high" | "good" | "ok"
            });

            return Json(new { success = true, data = items });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message, data = Array.Empty<object>() });
        }
    }

    // ── AJAX: Parent xem top K nanny gợi ý cho 1 job ────────
    // GET /Recommendation/NanniesData?jobId=...&topK=5
    [HttpGet]
    public async Task<IActionResult> NanniesData([FromQuery] Guid jobId, [FromQuery] int topK = 5)
    {
        SetAuthHeader();
        if (jobId == Guid.Empty)
            return Json(new { success = false, data = Array.Empty<object>() });

        try
        {
            var response = await _http.GetAsync($"/api/recommendation/nannies-for-job/{jobId}?topK={topK}");
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return Json(new { success = false, requiresUpgrade = true, data = Array.Empty<object>() });
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, data = Array.Empty<object>() });

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<List<NannyRecommendItem>>>(json, JsonOpts);

            var items = (result?.Data ?? new()).Select(n => new
            {
                nannyProfileId  = n.NannyProfileId,
                fullName        = n.FullName,
                avatarUrl       = n.AvatarUrl,
                bio             = n.Bio,
                yearsOfExperience = n.YearsOfExperience,
                averageRating   = n.AverageRating,
                totalReviews    = n.TotalReviews,
                distanceKm      = n.DistanceKm,
                skills          = n.Skills?.Select(s => s.SkillName).ToList(),
                finalScore      = n.FinalScore,
                matchLabel      = MatchLabel(n.FinalScore),
                matchTier       = MatchTier(n.FinalScore)
            });

            return Json(new { success = true, data = items });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message, data = Array.Empty<object>() });
        }
    }

    // ── Admin: batch re-embed ────────────────────────────────
    [Authorize(Roles = "Admin,Moderator")]
    [HttpPost]
    public async Task<IActionResult> ReembedBatch()
    {
        SetAuthHeader();
        try
        {
            var response = await _http.PostAsync("/api/recommendation/reembed/batch", null);
            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── Helpers ──────────────────────────────────────────────

    private static string MatchLabel(double score) => score switch
    {
        >= 0.82 => "Rất phù hợp",
        >= 0.65 => "Phù hợp tốt",
        _       => "Có thể phù hợp"
    };

    private static string MatchTier(double score) => score switch
    {
        >= 0.82 => "high",
        >= 0.65 => "good",
        _       => "ok"
    };
}

// ── Internal DTOs cho deserialize backend response ──────────
internal class JobRecommendItem
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; }
    public double? DistanceKm { get; set; }
    public List<SkillItem>? RequiredSkills { get; set; }
    public double FinalScore { get; set; }
}

internal class NannyRecommendItem
{
    public Guid NannyProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public decimal? AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public double? DistanceKm { get; set; }
    public List<SkillItem>? Skills { get; set; }
    public double FinalScore { get; set; }
}

internal class SkillItem
{
    public string SkillName { get; set; } = string.Empty;
}
