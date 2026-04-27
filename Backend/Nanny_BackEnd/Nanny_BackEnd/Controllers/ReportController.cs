using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Report;
using Nanny_BackEnd.Exceptions;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // POST /api/reports/job-postings/{id}
    [HttpPost("job-postings/{id:guid}")]
    public async Task<IActionResult> ReportJobPosting(Guid id, [FromBody] CreateReportRequest request)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng hiện tại."));

        try
        {
            var reportId = await _reportService.ReportJobPostingAsync(id, userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Báo cáo bài đăng đã được gửi thành công.",
                data = new { reportId, jobPostingId = id }
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (RateLimitExceededException ex) { return RateLimit(ex); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    // POST /api/reports/profiles/{id}
    [HttpPost("profiles/{id:guid}")]
    public async Task<IActionResult> ReportProfile(Guid id, [FromBody] CreateReportRequest request)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng hiện tại."));

        try
        {
            var reportId = await _reportService.ReportProfileAsync(id, userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Báo cáo hồ sơ đã được gửi thành công.",
                data = new { reportId, profileUserId = id }
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (RateLimitExceededException ex) { return RateLimit(ex); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    // GET /api/Moderator/reports?status=0&entityType=Profile&search=abc&page=1&pageSize=10
    [Authorize(Roles = "Moderator")]
    [HttpGet("moderator-view-report-list")]
    public async Task<IActionResult> ModeratorViewReportList(
        [FromQuery] int? status = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var response = await _reportService.GetModeratorReportsAsync(status, entityType, search, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    // GET /api/Moderator/reports/{id}
    [Authorize(Roles = "Moderator")]
    [HttpGet("moderator-view-report-detail/{id:guid}")]
    public async Task<IActionResult> ModeratorViewReportDetail(Guid id)
    {
        var result = await _reportService.GetModeratorReportDetailAsync(id);
        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    // PATCH /api/Moderator/reports/{id}/resolve
    [Authorize(Roles = "Moderator")]
    [HttpPatch("moderator-resolve-report/{id:guid}")]
    public async Task<IActionResult> ModeratorResolveReport(Guid id, [FromBody] ResolveReportRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Không xác định được điều hành viên." });

        var result = await _reportService.ResolveReportAsync(id, moderatorId.Value, request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    // PATCH /api/Moderator/reports/{id}/status
    [Authorize(Roles = "Moderator")]
    [HttpPatch("moderator-toggle-report-status/{id:guid}")]
    public async Task<IActionResult> ModeratorToggleReportStatus(Guid id, [FromBody] ToggleReportStatusRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Không xác định được điều hành viên." });

        var result = await _reportService.ToggleReportStatusAsync(id, moderatorId.Value, request.IsActive);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static object Fail(string message) => new { success = false, message };

    private IActionResult RateLimit(RateLimitExceededException ex)
    {
        Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString();
        return StatusCode(429, new
        {
            success = false,
            code = ex.Code,
            message = ex.Message,
            retryAfterSeconds = ex.RetryAfterSeconds,
            cooldownUntilUtc = ex.CooldownUntilUtc
        });
    }
}
