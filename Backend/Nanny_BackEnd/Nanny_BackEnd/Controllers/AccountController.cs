using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("admin-view-moderator-account-list")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminViewModeratorAccountList(
        [FromQuery] string? search = null,
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var response = await _accountService.AdminViewModeratorAccountListAsync(search, status, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    [HttpGet("admin-view-moderator-account-detail/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminViewModeratorAccountDetail(Guid id)
    {
        var result = await _accountService.AdminViewModeratorAccountDetailAsync(id);
        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost("admin-create-moderator-account")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminCreateModeratorAccount([FromBody] CreateModeratorRequest request)
    {
        var result = await _accountService.AdminCreateModeratorAccountAsync(request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    [HttpPatch("admin-update-moderator-account/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateModeratorAccount(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        var result = await _accountService.AdminUpdateModeratorAccountAsync(id, request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    [HttpPatch("admin-toggle-moderator-account/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminToggleModeratorAccount(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        var result = await _accountService.AdminToggleModeratorAccountAsync(id, request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    [HttpGet("moderator-view-account-list")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> ModeratorViewAccountList(
        [FromQuery] string? role = null,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var response = await _accountService.ModeratorViewAccountListAsync(role, status, search, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    [HttpGet("moderator-view-account-detail/{id:guid}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> ModeratorViewAccountDetail(Guid id)
    {
        var result = await _accountService.ModeratorViewAccountDetailAsync(id);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    [HttpPatch("moderator-toggle-account-status/{id:guid}/status")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> ModeratorToggleAccountStatus(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        var result = await _accountService.ModeratorToggleAccountStatusAsync(id, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }
}
