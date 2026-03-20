using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Verification;
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
    [HttpGet("nanny-requests")]
    public async Task<IActionResult> GetMyRequests()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var requests = await _verificationService.GetNannyRequestsAsync(userId);
        return Ok(new { success = true, data = requests });
    }

    [Authorize(Roles = "Nanny")]
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitVerificationRequestDto model)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        if (model.Documents == null || !model.Documents.Any())
            return BadRequest(new { success = false, message = "Bạn phải tải lên ít nhất một tài liệu." });

        var (success, message) = await _verificationService.SubmitRequestAsync(userId, model);

        if (!success)
            return BadRequest(new { success = false, message });

        return Ok(new { success = true, message });
    }
}
