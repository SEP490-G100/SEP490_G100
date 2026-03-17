using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize(Roles = "Moderator")]
public class ModeratorController : ControllerBase
{
    private readonly UserService _userService;
    private readonly VerificationRequestService _verificationService;

    public ModeratorController(UserService userService, VerificationRequestService verificationService)
    {
        _userService = userService;
        _verificationService = verificationService;
    }

    // ─────────────────────────────────────────────────────
    // ACCOUNT MANAGEMENT
    // ─────────────────────────────────────────────────────

    /// GET /api/Moderator/accounts?role=Nanny&status=0&search=lan&page=1&pageSize=10
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? role = null,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
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
        var result = await _verificationService.ReviewAsync(id, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }
}
