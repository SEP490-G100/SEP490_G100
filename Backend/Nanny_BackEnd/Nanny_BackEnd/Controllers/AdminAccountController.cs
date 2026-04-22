using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Admin")]
[Authorize(Roles = "Admin")]
public class AdminAccountController : ControllerBase
{
    private readonly IAdminAccountService _adminAccountService;

    public AdminAccountController(IAdminAccountService adminAccountService)
    {
        _adminAccountService = adminAccountService;
    }

    [HttpGet("admin-view-moderator-account-list")]
    public async Task<IActionResult> AdminViewModeratorAccountList(
        [FromQuery] string? search = null,
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var response = await _adminAccountService.AdminViewModeratorAccountListAsync(search, status, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    [HttpGet("admin-view-moderator-account-detail/{id:guid}")]
    public async Task<IActionResult> AdminViewModeratorAccountDetail(Guid id)
    {
        var result = await _adminAccountService.AdminViewModeratorAccountDetailAsync(id);
        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost("admin-create-moderator-account")]
    public async Task<IActionResult> AdminCreateModeratorAccount([FromBody] CreateModeratorRequest request)
    {
        var result = await _adminAccountService.AdminCreateModeratorAccountAsync(request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    [HttpPatch("admin-update-moderator-account/{id:guid}")]
    public async Task<IActionResult> AdminUpdateModeratorAccount(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        var result = await _adminAccountService.AdminUpdateModeratorAccountAsync(id, request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    [HttpPatch("admin-toggle-moderator-account/{id:guid}")]
    public async Task<IActionResult> AdminToggleModeratorAccount(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        var result = await _adminAccountService.AdminToggleModeratorAccountAsync(id, request);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }
}
