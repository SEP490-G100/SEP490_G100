using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/recommendation")]
public class RecommendationController : ControllerBase
{
    private readonly RecommendationService _recSvc;
    private readonly EmbeddingService _embedSvc;
    private readonly RecommendationRepository _repo;
    private readonly RecommendationConfigRepository _configRepo;
    private readonly Sep490NannyDbContext _db;
    private readonly SubscriptionService _subscriptionService;

    public RecommendationController(
        RecommendationService recSvc,
        EmbeddingService embedSvc,
        RecommendationRepository repo,
        RecommendationConfigRepository configRepo,
        Sep490NannyDbContext db,
        SubscriptionService subscriptionService)
    {
        _recSvc = recSvc;
        _embedSvc = embedSvc;
        _repo = repo;
        _configRepo = configRepo;
        _db = db;
        _subscriptionService = subscriptionService;
    }

    // ──────────────────────────────────────────────────────────────
    // 1. Read Models (Admin / Moderator)
    // ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet("read-model/nanny/{nannyProfileId:guid}")]
    public async Task<IActionResult> GetNannyReadModel(Guid nannyProfileId)
    {
        var model = await _repo.GetNannyReadModelAsync(nannyProfileId);
        if (model == null) return NotFound(Fail("Không tìm thấy nanny profile."));
        return Ok(Success(model));
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet("read-model/job/{jobId:guid}")]
    public async Task<IActionResult> GetJobReadModel(Guid jobId)
    {
        var model = await _repo.GetJobReadModelAsync(jobId);
        if (model == null) return NotFound(Fail("Không tìm thấy job posting."));
        return Ok(Success(model));
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet("read-model/nannies/pending-embed")]
    public async Task<IActionResult> GetPendingEmbedNannies()
    {
        var list = await _repo.GetPendingEmbedNanniesAsync();
        return Ok(Success(list, list.Count));
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet("read-model/jobs/pending-embed")]
    public async Task<IActionResult> GetPendingEmbedJobs()
    {
        var list = await _repo.GetPendingEmbedJobsAsync();
        return Ok(Success(list, list.Count));
    }

    // ──────────────────────────────────────────────────────────────
    // 2. Re-embed Triggers (Admin / Moderator)
    // ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Moderator")]
    [HttpPost("reembed/nanny/{nannyProfileId:guid}")]
    public async Task<IActionResult> ReembedNanny(Guid nannyProfileId)
    {
        try
        {
            await _embedSvc.EmbedNannyAsync(nannyProfileId);
            return Ok(new { success = true, message = "Đã re-embed nanny thành công." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail($"Lỗi khi embed: {ex.Message}"));
        }
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpPost("reembed/job/{jobId:guid}")]
    public async Task<IActionResult> ReembedJob(Guid jobId)
    {
        try
        {
            await _embedSvc.EmbedJobAsync(jobId);
            return Ok(new { success = true, message = "Đã re-embed job thành công." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail($"Lỗi khi embed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Batch embed tất cả nanny + job có Embedding = NULL.
    /// </summary>
    [Authorize(Roles = "Admin,Moderator")]
    /// <summary>
    /// Batch embed.
    /// ?force=false (mặc định): chỉ embed record có Embedding IS NULL.
    /// ?force=true: re-embed TẤT CẢ (dùng sau khi đổi template).
    /// </summary>
    [HttpPost("reembed/batch")]
    public async Task<IActionResult> ReembedBatch([FromQuery] bool force = false)
    {
        try
        {
            int nannyCount, jobCount;

            if (force)
            {
                nannyCount = await _embedSvc.EmbedAllNanniesForceAsync();
                jobCount   = await _embedSvc.EmbedAllJobsForceAsync();
            }
            else
            {
                nannyCount = await _embedSvc.EmbedAllPendingNanniesAsync();
                jobCount   = await _embedSvc.EmbedAllPendingJobsAsync();
            }

            return Ok(new
            {
                success = true,
                message = $"Đã embed {nannyCount} nanny và {jobCount} job." + (force ? " (force)" : ""),
                nanniesEmbedded = nannyCount,
                jobsEmbedded = jobCount,
                forced = force
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail($"Lỗi khi batch embed: {ex.Message}"));
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 3. Recommendation Endpoints
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parent/Admin xem top K nanny phù hợp với một job posting.
    /// GET /api/recommendation/nannies-for-job/{jobId}?topK=5&amp;lat=10.7&amp;lng=106.7
    /// </summary>
    [Authorize]
    [HttpGet("nannies-for-job/{jobId:guid}")]
    public async Task<IActionResult> GetNanniesForJob(
        Guid jobId,
        [FromQuery] int topK = 10,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null)
    {
        topK = Math.Clamp(topK, 1, 50);

        // Kiểm tra job tồn tại và thuộc về parent hiện tại (hoặc Admin)
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Moderator");
        if (!isAdmin)
        {
            var parent = await _db.ParentProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId.Value && !p.IsDeleted);
            if (parent == null) return Forbid();

            var benefits = await _subscriptionService.getBenefitsForParentProfile(parent.Id);
            if (!benefits.CanUseRecommendation)
                return StatusCode(StatusCodes.Status402PaymentRequired, Fail("Tính năng gợi ý AI yêu cầu gói Plus hoặc Pro."));

            var jobExists = await _db.JobPostings
                .AnyAsync(j => j.Id == jobId && j.ParentProfileId == parent.Id && !j.IsDeleted);
            if (!jobExists) return NotFound(Fail("Không tìm thấy job posting."));
        }

        var results = await _recSvc.GetTopNanniesForJobAsync(jobId, topK, lat, lng);
        // lat/lng từ client override tọa độ job trong DB để tính khoảng cách chính xác hơn
        return Ok(Success(results, results.Count));
    }

    /// <summary>
    /// Nanny xem top K job phù hợp với bản thân.
    /// GET /api/recommendation/jobs-for-nanny?topK=5
    /// </summary>
    [Authorize(Roles = "Nanny")]
    [HttpGet("jobs-for-nanny")]
    public async Task<IActionResult> GetJobsForNanny([FromQuery] int topK = 10)
    {
        topK = Math.Clamp(topK, 1, 50);

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        var nanny = await _db.NannyProfiles
            .FirstOrDefaultAsync(n => n.UserId == userId.Value && !n.IsDeleted);
        if (nanny == null) return NotFound(Fail("Không tìm thấy nanny profile."));

        var benefits = await _subscriptionService.getBenefitsForNannyProfile(nanny.Id);
        if (!benefits.CanUseRecommendation)
            return StatusCode(StatusCodes.Status402PaymentRequired, Fail("Tính năng gợi ý AI yêu cầu gói Plus hoặc Pro."));

        var results = await _recSvc.GetTopJobsForNannyAsync(nanny.Id, topK);
        return Ok(Success(results, results.Count));
    }

    // ──────────────────────────────────────────────────────────────
    // 4. Scoring Config (Admin only)
    // ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin")]
    [HttpGet("config/weights")]
    public async Task<IActionResult> GetWeights()
    {
        var w = await _configRepo.GetWeightsAsync();
        return Ok(Success(new
        {
            semantic_weight  = w.Semantic,
            salary_weight    = w.Salary,
            distance_weight  = w.Distance,
            cold_start_score = w.ColdStart
        }));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("config/weights")]
    public async Task<IActionResult> UpdateWeight([FromBody] UpdateWeightRequest request)
    {
        var allowed = new[] { "rec:semantic_weight", "rec:salary_weight", "rec:distance_weight", "rec:cold_start_score" };
        if (!allowed.Contains(request.Key))
            return BadRequest(Fail($"Key không hợp lệ. Cho phép: {string.Join(", ", allowed)}"));

        if (request.Value < 0 || request.Value > 1)
            return BadRequest(Fail("Value phải trong khoảng [0, 1]."));

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            await _configRepo.UpdateWeightAsync(request.Key, request.Value, userId.Value);
            return Ok(new { success = true, message = $"Đã cập nhật {request.Key} = {request.Value}." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(Fail(ex.Message));
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static object Success(object data, int? total = null) =>
        total.HasValue
            ? new { success = true, total, data }
            : new { success = true, data };

    private static object Fail(string message) => new { success = false, message };
}
