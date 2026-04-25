using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/VerificationRequest")]
public class VerificationRequestController : ControllerBase
{
    private readonly IVerificationRequestService _verificationService;

    public VerificationRequestController(IVerificationRequestService verificationService)
    {
        _verificationService = verificationService;
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("moderator-view-verification-list")]
    public async Task<IActionResult> ModeratorViewVerificationList(
        [FromQuery] int? status = null,
        [FromQuery] int? requestType = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var response = await _verificationService.ModeratorViewVerificationListAsync(status, requestType, search, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("moderator-view-verification-detail/{id:guid}")]
    public async Task<IActionResult> ModeratorViewVerificationDetail(Guid id)
    {
        var result = await _verificationService.ModeratorViewVerificationDetailAsync(id);
        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    [Authorize(Roles = "Moderator")]
    [HttpPatch("moderator-review-verification/{id:guid}")]
    public async Task<IActionResult> ModeratorReviewVerification(Guid id, [FromBody] ReviewVerificationRequest request)
    {
        var moderatorId = GetCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Không xác định được moderator." });

        var result = await _verificationService.ModeratorReviewVerificationAsync(id, moderatorId.Value, request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    [Authorize(Roles = "Nanny")]
    [HttpGet("nanny-view-verification-list")]
    public async Task<IActionResult> NannyGetVerificationRequestList([FromQuery] int? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 3)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var requests = await _verificationService.NannyGetVerificationRequestListAsync(userId.Value, status, page, pageSize);
        return Ok(new { success = true, data = requests });
    }

    [Authorize(Roles = "Nanny")]
    [HttpGet("nanny-view-verification-detail/{id:guid}")]
    public async Task<IActionResult> NannyViewVerificationRequestDetail(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var (success, data, message) = await _verificationService.NannyViewVerificationRequestDetailAsync(userId.Value, id);
        if (!success || data == null)
        {
            return NotFound(new { success = false, message = message ?? "Không tìm thấy yêu cầu xác minh." });
        }

        return Ok(new { success = true, data });
    }

    [Authorize(Roles = "Nanny")]
    [HttpPost("nanny-submit-verification")]
    public async Task<IActionResult> NannySubmitVerificationRequest([FromBody] SubmitVerificationRequestDto model)
    {
        return await SubmitVerificationInternalAsync(model);
    }

    [Authorize(Roles = "Nanny")]
    [HttpPost("nanny-submit-profile-verification")]
    public async Task<IActionResult> NannySubmitProfileVerificationRequest([FromBody] SubmitVerificationRequestDto model)
    {
        model.RequestType = (int)VerificationRequestType.ProfileVerification;
        return await SubmitVerificationInternalAsync(model);
    }

    [Authorize(Roles = "Nanny")]
    [HttpPost("nanny-submit-health-certificate")]
    public async Task<IActionResult> NannySubmitHealthCertificateRequest([FromBody] SubmitVerificationRequestDto model)
    {
        model.RequestType = (int)VerificationRequestType.HealthCertificate;
        return await SubmitVerificationInternalAsync(model);
    }

    private async Task<IActionResult> SubmitVerificationInternalAsync(SubmitVerificationRequestDto model)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            if (model.Documents == null || !model.Documents.Any())
            {
                return BadRequest(new { success = false, message = "Ban phai tai len it nhat mot tai lieu." });
            }

            var (success, message) = await _verificationService.NannySubmitVerificationRequestAsync(userId.Value, model);
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { success = false, message = errorMessage });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
