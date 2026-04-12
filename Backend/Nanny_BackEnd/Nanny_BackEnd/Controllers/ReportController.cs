using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Report;
using Nanny_BackEnd.Exceptions;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportController(ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpPost("job-postings/{id:guid}")]
    public async Task<IActionResult> ReportJobPosting(Guid id, [FromBody] CreateReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Khong xac dinh duoc nguoi dung hien tai."));

        try
        {
            var reportId = await _reportService.ReportJobPostingAsync(id, userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Bao cao bai dang da duoc gui thanh cong.",
                data = new { reportId, jobPostingId = id }
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (RateLimitExceededException ex) { return RateLimit(ex); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("profiles/{id:guid}")]
    public async Task<IActionResult> ReportProfile(Guid id, [FromBody] CreateReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Khong xac dinh duoc nguoi dung hien tai."));

        try
        {
            var reportId = await _reportService.ReportProfileAsync(id, userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Bao cao ho so da duoc gui thanh cong.",
                data = new { reportId, profileUserId = id }
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (RateLimitExceededException ex) { return RateLimit(ex); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static object Fail(string message) => new { success = false, message };

    private static object FailValidation(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return new { success = false, message = "Du lieu khong hop le.", errors };
    }

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
