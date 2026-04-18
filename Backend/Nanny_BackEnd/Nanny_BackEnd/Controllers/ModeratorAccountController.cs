using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Moderator")]
[Authorize(Roles = "Moderator")]
public class ModeratorAccountController : ControllerBase
{
    private readonly ModeratorAccountService _moderatorAccountService;

    public ModeratorAccountController(ModeratorAccountService moderatorAccountService)
    {
        _moderatorAccountService = moderatorAccountService;
    }

    // GET /api/Moderator/moderator-view-account-list?role=Nanny&status=0&search=lan&page=1&pageSize=3
    [HttpGet("moderator-view-account-list")]
    public async Task<IActionResult> ModeratorViewAccountList(
        [FromQuery] string? role = null,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var response = await _moderatorAccountService.ModeratorViewAccountListAsync(role, status, search, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    // GET /api/Moderator/moderator-view-account-detail/{id}
    [HttpGet("moderator-view-account-detail/{id:guid}")]
    public async Task<IActionResult> ModeratorViewAccountDetail(Guid id)
    {
        var result = await _moderatorAccountService.ModeratorViewAccountDetailAsync(id);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    // PATCH /api/Moderator/moderator-update-account-status/{id}/status
    [HttpPatch("moderator-update-account-status/{id:guid}/status")]
    public async Task<IActionResult> ModeratorUpdateAccountStatus(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        var result = await _moderatorAccountService.ModeratorUpdateAccountStatusAsync(id, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }
}
