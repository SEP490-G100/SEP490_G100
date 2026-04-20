using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NannyVerificationRequestController : ControllerBase
{
    private readonly VerificationRequestService _verificationService;

    public NannyVerificationRequestController(VerificationRequestService verificationService)
    {
        _verificationService = verificationService;
    }

    [Authorize(Roles = "Nanny")]
    [HttpGet("nanny-view-verification-list")]
    public async Task<IActionResult> NannyGetVerificationRequestList([FromQuery] int? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 3)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var requests = await _verificationService.NannyGetVerificationRequestListAsync(userId, status, page, pageSize);
        return Ok(new { success = true, data = requests });
    }

    [Authorize(Roles = "Nanny")]
    [HttpGet("nanny-view-verification-detail/{id:guid}")]
    public async Task<IActionResult> NannyViewVerificationRequestDetail(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var (success, data, message) = await _verificationService.NannyViewVerificationRequestDetailAsync(userId, id);
        if (!success || data == null)
        {
            return NotFound(new { success = false, message = message ?? "Khong tim thay yeu cau xac minh." });
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
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            if (model.Documents == null || !model.Documents.Any())
            {
                return BadRequest(new { success = false, message = "Ban phai tai len it nhat mot tai lieu." });
            }

            var (success, message) = await _verificationService.NannySubmitVerificationRequestAsync(userId, model);
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
}
