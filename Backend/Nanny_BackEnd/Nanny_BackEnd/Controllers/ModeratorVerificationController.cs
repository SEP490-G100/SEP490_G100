using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Services;
using System.Security.Claims;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Moderator")]
[Authorize(Roles = "Moderator")]
public class ModeratorVerificationController : ControllerBase
{
    private readonly ModeratorVerificationService _moderatorVerificationService;

    public ModeratorVerificationController(ModeratorVerificationService moderatorVerificationService)
    {
        _moderatorVerificationService = moderatorVerificationService;
    }

    // GET /api/Moderator/moderator-view-verification-list?status=0&search=lan&page=1&pageSize=3
    [HttpGet("moderator-view-verification-list")]
    public async Task<IActionResult> ModeratorViewVerificationList(
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var response = await _moderatorVerificationService.ModeratorViewVerificationListAsync(status, search, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    // GET /api/Moderator/moderator-view-verification-detail/{id}
    [HttpGet("moderator-view-verification-detail/{id:guid}")]
    public async Task<IActionResult> ModeratorViewVerificationDetail(Guid id)
    {
        var result = await _moderatorVerificationService.ModeratorViewVerificationDetailAsync(id);
        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    // PATCH /api/Moderator/review-verification/{id}
    [HttpPatch("review-verification/{id:guid}")]
    public async Task<IActionResult> ModeratorReviewVerification(Guid id, [FromBody] ReviewVerificationRequest request)
    {
        var moderatorId = GetCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc moderator." });

        var result = await _moderatorVerificationService.ModeratorReviewVerificationAsync(id, moderatorId.Value, request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
