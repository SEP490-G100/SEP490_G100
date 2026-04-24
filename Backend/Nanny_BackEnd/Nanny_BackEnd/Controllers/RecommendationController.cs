using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/recommendation")]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationService _recSvc;
    private readonly IEmbeddingService _embedSvc;

    public RecommendationController(
        IRecommendationService recSvc,
        IEmbeddingService embedSvc)
    {
        _recSvc = recSvc;
        _embedSvc = embedSvc;
    }

    // ──────────────────────────────────────────────────────────────
    // 1. Read Models (Admin / Moderator)
    // ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet("read-model/nanny/{nannyProfileId:guid}")]
    public async Task<IActionResult> GetNannyReadModel(Guid nannyProfileId)
    {
        var model = await _recSvc.GetNannyReadModelForAdminAsync(nannyProfileId);
        if (model == null) return NotFound(Fail("Không tìm thấy nanny profile."));
        return Ok(Success(model));
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet("read-model/job/{jobId:guid}")]
    public async Task<IActionResult> GetJobReadModel(Guid jobId)
    {
        var model = await _recSvc.GetJobReadModelForAdminAsync(jobId);
        if (model == null) return NotFound(Fail("Không tìm thấy bài đăng."));
        return Ok(Success(model));
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet("read-model/nannies/pending-embed")]
    public async Task<IActionResult> GetPendingEmbedNannies()
    {
        var list = await _recSvc.GetPendingEmbedNanniesForAdminAsync();
        return Ok(Success(list, list.Count));
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet("read-model/jobs/pending-embed")]
    public async Task<IActionResult> GetPendingEmbedJobs()
    {
        var list = await _recSvc.GetPendingEmbedJobsForAdminAsync();
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
            return Ok(new { success = true, message = "Đã cập nhật lại dữ liệu gợi ý cho bảo mẫu." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail($"Lỗi khi cập nhật dữ liệu gợi ý: {ex.Message}"));
        }
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpPost("reembed/job/{jobId:guid}")]
    public async Task<IActionResult> ReembedJob(Guid jobId)
    {
        try
        {
            await _embedSvc.EmbedJobAsync(jobId);
            return Ok(new { success = true, message = "Đã cập nhật lại dữ liệu gợi ý cho bài đăng." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail($"Lỗi khi cập nhật dữ liệu gợi ý: {ex.Message}"));
        }
    }

    [Authorize(Roles = "Admin,Moderator")]
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

    [Authorize]
    [HttpGet("nannies-for-job/{jobId:guid}")]
    public async Task<IActionResult> GetNanniesForJob(
        Guid jobId,
        [FromQuery] int topK = 10,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null)
    {
        topK = Math.Clamp(topK, 1, 50);

        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Moderator");
        var g = await _recSvc.ValidateNanniesForJobGatingAsync(userId, jobId, isAdmin);
        if (!g.IsAllowed)
        {
            if (g.HttpStatus == 403) return Forbid();
            if (g.HttpStatus == 401) return Unauthorized(Fail(g.ErrorMessage ?? ""));
            return StatusCode(g.HttpStatus, Fail(g.ErrorMessage ?? ""));
        }

        var results = await _recSvc.GetTopNanniesForJobAsync(jobId, topK, lat, lng);
        return Ok(Success(results, results.Count));
    }

    [Authorize(Roles = "Nanny")]
    [HttpGet("jobs-for-nanny")]
    public async Task<IActionResult> GetJobsForNanny([FromQuery] int topK = 10)
    {
        topK = Math.Clamp(topK, 1, 50);

        var userId = GetCurrentUserId();
        var g = await _recSvc.ValidateJobsForNannyGatingAsync(userId);
        if (!g.IsAllowed)
        {
            if (g.HttpStatus == 401) return Unauthorized(Fail(g.ErrorMessage ?? ""));
            return StatusCode(g.HttpStatus, Fail(g.ErrorMessage ?? ""));
        }

        var results = await _recSvc.GetTopJobsForNannyAsync(g.NannyProfileId!.Value, topK);
        return Ok(Success(results, results.Count));
    }

    // ──────────────────────────────────────────────────────────────
    // 4. Scoring Config (Admin only)
    // ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin")]
    [HttpGet("config/weights")]
    public async Task<IActionResult> GetWeights()
    {
        var w = await _recSvc.GetRecommendationConfigWeightsAsync();
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
            await _recSvc.UpdateRecommendationConfigWeightAsync(request.Key, request.Value, userId.Value);
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
