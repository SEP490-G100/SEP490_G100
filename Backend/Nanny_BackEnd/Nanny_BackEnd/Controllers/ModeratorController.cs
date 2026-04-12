using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Report;
using Nanny_BackEnd.Services;

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

    // GET /api/Moderator/reports?status=0&entityType=Profile&search=abc&page=1&pageSize=10
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

    // GET /api/Moderator/reports/{id}
    [HttpGet("reports/{id:guid}")]
    public async Task<IActionResult> GetReportDetail(Guid id)
    {
        var result = await _reportService.GetModeratorReportDetailAsync(id);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    // PATCH /api/Moderator/reports/{id}/resolve
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

    // PATCH /api/Moderator/reports/{id}/status
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
