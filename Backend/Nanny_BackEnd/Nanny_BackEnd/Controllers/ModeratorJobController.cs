using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Moderator")]
[Authorize(Roles = "Moderator")]
public class ModeratorJobController : ControllerBase
{
    private readonly IModeratorJobService _moderatorJobService;

    public ModeratorJobController(IModeratorJobService moderatorJobService)
    {
        _moderatorJobService = moderatorJobService;
    }

    // GET /api/Moderator/moderator-view-job-list?status=1&moderationStatus=0&search=lan&page=1&pageSize=10
    [HttpGet("moderator-view-job-list")]
    public async Task<IActionResult> ModeratorViewJobList(
        [FromQuery] int? status = null,
        [FromQuery] int? moderationStatus = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, totalCount) = await _moderatorJobService.ModeratorViewJobListAsync(
            status,
            moderationStatus,
            search,
            page,
            pageSize);

        return Ok(new { success = true, data = new { items, totalCount, page, pageSize } });
    }

    // GET /api/Moderator/moderator-view-job-detail/{id}
    [HttpGet("moderator-view-job-detail/{id:guid}")]
    public async Task<IActionResult> ModeratorViewJobDetail(Guid id)
    {
        try
        {
            var detail = await _moderatorJobService.ModeratorViewJobDetailAsync(id);
            return Ok(new { success = true, data = detail });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    // PATCH /api/Moderator/moderator-review-job/{id}
    [HttpPatch("moderator-review-job/{id:guid}")]
    public async Task<IActionResult> ModeratorReviewJob(Guid id, [FromBody] ModerateJobPostingRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc moderator." });

        try
        {
            await _moderatorJobService.ModeratorReviewJobAsync(id, moderatorId.Value, request);
            return Ok(new { success = true, message = "Xu ly tin dang thanh cong." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // PATCH /api/Moderator/moderator-deactivate-job-posting/{id}
    [HttpPatch("moderator-deactivate-job-posting/{id:guid}")]
    public async Task<IActionResult> ModeratorDeactivateJobPosting(Guid id)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Cannot identify moderator." });

        try
        {
            await _moderatorJobService.ModeratorDeactivateJobAsync(id, moderatorId.Value);
            return Ok(new { success = true, message = "Job posting deactivated successfully." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
