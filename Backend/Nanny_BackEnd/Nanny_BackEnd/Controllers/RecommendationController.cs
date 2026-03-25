using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
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
    private readonly Sep490NannyDbContext _db;

    public RecommendationController(
        RecommendationService recSvc,
        EmbeddingService embedSvc,
        RecommendationRepository repo,
        Sep490NannyDbContext db)
    {
        _recSvc = recSvc;
        _embedSvc = embedSvc;
        _repo = repo;
        _db = db;
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
    [HttpPost("reembed/batch")]
    public async Task<IActionResult> ReembedBatch()
    {
        try
        {
            var nannyCount = await _embedSvc.EmbedAllPendingNanniesAsync();
            var jobCount = await _embedSvc.EmbedAllPendingJobsAsync();

            return Ok(new
            {
                success = true,
                message = $"Đã embed {nannyCount} nanny và {jobCount} job.",
                nanniesEmbedded = nannyCount,
                jobsEmbedded = jobCount
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
        [FromQuery] int topK = 5,
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

            var jobExists = await _db.JobPostings
                .AnyAsync(j => j.Id == jobId && j.ParentProfileId == parent.Id && !j.IsDeleted);
            if (!jobExists) return NotFound(Fail("Không tìm thấy job posting."));
        }

        var results = await _recSvc.GetTopNanniesForJobAsync(jobId, topK, lat, lng);
        return Ok(Success(results, results.Count));
    }

    /// <summary>
    /// Nanny xem top K job phù hợp với bản thân.
    /// GET /api/recommendation/jobs-for-nanny?topK=5
    /// </summary>
    [Authorize(Roles = "Nanny")]
    [HttpGet("jobs-for-nanny")]
    public async Task<IActionResult> GetJobsForNanny([FromQuery] int topK = 5)
    {
        topK = Math.Clamp(topK, 1, 50);

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        var nanny = await _db.NannyProfiles
            .FirstOrDefaultAsync(n => n.UserId == userId.Value && !n.IsDeleted);
        if (nanny == null) return NotFound(Fail("Không tìm thấy nanny profile."));

        var results = await _recSvc.GetTopJobsForNannyAsync(nanny.Id, topK);
        return Ok(Success(results, results.Count));
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
