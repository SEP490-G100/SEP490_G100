using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Report;
using Nanny_BackEnd.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Moderator")]
public class ModeratorController : ControllerBase
{
    private readonly ReportService _reportService;

    public ModeratorController(ReportService reportService)
    {
        _reportService = reportService;
    }

    // ─────────────────────────────────────────────────────
    // NANNY VERIFICATION
    // ─────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────
    // JOB POSTING MODERATION
    // ─────────────────────────────────────────────────────

    /// GET /api/Moderator/job-postings?status=1&moderationStatus=0&search=lan&page=1&pageSize=10
    [HttpGet("job-postings")]
    public async Task<IActionResult> GetJobPostings(
        [FromQuery] int? status = null,
        [FromQuery] int? moderationStatus = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, totalCount) = await _jobService.GetModeratorJobsAsync(status, moderationStatus, search, page, pageSize);
        return Ok(new { success = true, data = new { items, totalCount, page, pageSize } });
    }

    /// GET /api/Moderator/job-postings/{id}
    [HttpGet("job-postings/{id:guid}")]
    public async Task<IActionResult> GetJobPosting(Guid id)
    {
        try
        {
            var detail = await _jobService.getDetail(id);
            return Ok(new { success = true, data = detail });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    /// PATCH /api/Moderator/job-postings/{id}/review
    [HttpPatch("job-postings/{id:guid}/review")]
    public async Task<IActionResult> ReviewJobPosting(Guid id, [FromBody] ModerateJobPostingRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue) return Unauthorized(new { success = false, message = "Không xác định được moderator." });

        try
        {
            await _jobService.ReviewJobAsync(id, moderatorId.Value, request.Action, request.Note);
            return Ok(new { success = true, message = "Xử lý tin đăng thành công." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    /// PATCH /api/Moderator/job-postings/{id}/deactivate
    [HttpPatch("job-postings/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateJobPosting(Guid id)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue) return Unauthorized(new { success = false, message = "Cannot identify moderator." });

        try
        {
            await _jobService.DeactivateJobAsync(id, moderatorId.Value);
            return Ok(new { success = true, message = "Job posting deactivated successfully." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    // ─────────────────────────────────────────────────────
    // REPORT MODERATION
    // ─────────────────────────────────────────────────────

    /// GET /api/Moderator/reports?status=0&entityType=Profile&search=abc&page=1&pageSize=10
    [HttpGet("reports")]
    public async Task<IActionResult> GetReports(
        [FromQuery] int? status = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var response = await _reportService.GetModeratorReportsAsync(status, entityType, search, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    /// GET /api/Moderator/reports/{id}
    [HttpGet("reports/{id:guid}")]
    public async Task<IActionResult> GetReportDetail(Guid id)
    {
        var result = await _reportService.GetModeratorReportDetailAsync(id);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// PATCH /api/Moderator/reports/{id}/resolve
    [HttpPatch("reports/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveReport(Guid id, [FromBody] ResolveReportRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return BadRequest(new { success = false, message = "Invalid request.", errors });
        }

        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Cannot identify moderator." });

        var result = await _reportService.ResolveReportAsync(id, moderatorId.Value, request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// PATCH /api/Moderator/reports/{id}/status
    [HttpPatch("reports/{id:guid}/status")]
    public async Task<IActionResult> ToggleReportStatus(Guid id, [FromBody] ToggleReportStatusRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Cannot identify moderator." });

        var result = await _reportService.ToggleReportStatusAsync(id, moderatorId.Value, request.IsActive);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
