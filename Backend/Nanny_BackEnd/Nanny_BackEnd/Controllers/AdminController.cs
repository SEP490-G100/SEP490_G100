using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly UserService _userService;
    private readonly ExportService _exportService;

    public AdminController(DashboardService dashboardService, UserService userService, ExportService exportService)
    {
        _dashboardService = dashboardService;
        _userService      = userService;
        _exportService    = exportService;
    }

    // ────────────────────────────────────────────────
    // GET /api/admin/export
    // ────────────────────────────────────────────────
    [HttpGet("export")]
    public async Task<IActionResult> ExportSystemData()
    {
        var fileContents = await _exportService.ExportSystemDataToExcelAsync();
        return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"NannyMatch_SystemData_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // ────────────────────────────────────────────────
    // GET /api/admin/dashboard
    // ────────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var stats = await _dashboardService.GetDashboardStatsAsync();
        return Ok(new { success = true, data = stats });
    }

    // ────────────────────────────────────────────────
    // GET /api/admin/moderators?search=&status=&page=1&pageSize=10
    // ────────────────────────────────────────────────
    [HttpGet("moderators")]
    public async Task<IActionResult> GetModerators(
        [FromQuery] string? search   = null,
        [FromQuery] int?    status   = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 3)
    {
        var response = await _userService.GetModeratorsAsync(search, status, page, pageSize);
        return Ok(new { success = true, data = response });
    }

    // ────────────────────────────────────────────────
    // GET /api/admin/moderators/{id}  — Get detail
    // ────────────────────────────────────────────────
    [HttpGet("moderators/{id:guid}")]
    public async Task<IActionResult> GetModerator(Guid id)
    {
        var result = await _userService.GetModeratorAsync(id);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    // ────────────────────────────────────────────────
    // POST /api/admin/moderators  — Create moderator
    // ────────────────────────────────────────────────
    [HttpPost("moderators")]
    public async Task<IActionResult> CreateModerator([FromBody] CreateModeratorRequest request)
    {
        var result = await _userService.CreateModeratorAsync(request);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    // ────────────────────────────────────────────────
    // PATCH /api/admin/moderators/{id}  — Edit moderator
    // ────────────────────────────────────────────────
    [HttpPatch("moderators/{id:guid}")]
    public async Task<IActionResult> UpdateModerator(Guid id, [FromBody] UpdateModeratorRequest request)
    {
        var result = await _userService.UpdateModeratorAsync(id, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    // ────────────────────────────────────────────────
    // DELETE /api/admin/moderators/{id}  — Soft delete
    // ────────────────────────────────────────────────
    [HttpDelete("moderators/{id:guid}")]
    public async Task<IActionResult> DeleteModerator(Guid id)
    {
        var result = await _userService.DeleteModeratorAsync(id);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }
}
