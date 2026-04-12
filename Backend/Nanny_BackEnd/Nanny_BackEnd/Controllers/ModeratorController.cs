using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Report;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Enums;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Moderator")]
public class ModeratorController : ControllerBase
{
    private readonly UserService _userService;
    private readonly VerificationRequestService _verificationService;
    private readonly JobService _jobService;
    private readonly ReportService _reportService;

    public ModeratorController(
        UserService userService,
        VerificationRequestService verificationService,
        JobService jobService,
        ReportService reportService)
    {
        _userService = userService;
        _verificationService = verificationService;
        _jobService = jobService;
        _reportService = reportService;
    }

    // MODERATOR MANAGE ACCOUNTS
    // GET /api/Moderator/accounts?role=Nanny&status=0&search=lan&page=1&pageSize=3
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? role = null,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var response = await _userService.GetAccountsAsync(role, status, search, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    /// GET /api/Moderator/accounts/{id}
    [HttpGet("accounts/{id:guid}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        var result = await _userService.GetAccountAsync(id);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// PATCH /api/Moderator/accounts/{id}
    [HttpPatch("accounts/{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountRequest request)
    {
        var result = await _userService.UpdateAccountAsync(id, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// PATCH /api/Moderator/accounts/{id}/status
    [HttpPatch("accounts/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        var result = await _userService.UpdateStatusAsync(id, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    // ─────────────────────────────────────────────────────
    // NANNY VERIFICATION
    // ─────────────────────────────────────────────────────

    /// GET /api/Moderator/verifications?status=0&search=lan&page=1&pageSize=3
    [HttpGet("verifications")]
    public async Task<IActionResult> GetVerifications(
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var response = await _verificationService.GetListAsync(status, search, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    /// GET /api/Moderator/verifications/{id}
    [HttpGet("verifications/{id:guid}")]
    public async Task<IActionResult> GetVerification(Guid id)
    {
        var result = await _verificationService.GetDetailAsync(id);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// PATCH /api/Moderator/verifications/{id}/review
    /// Body: { "action": 1 } = Approve | { "action": 2, "rejectionReason": "..." } = Reject
    [HttpPatch("verifications/{id:guid}/review")]
    public async Task<IActionResult> ReviewVerification(Guid id, [FromBody] ReviewVerificationRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc moderator." });

        var result = await _verificationService.ReviewAsync(id, moderatorId.Value, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

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
