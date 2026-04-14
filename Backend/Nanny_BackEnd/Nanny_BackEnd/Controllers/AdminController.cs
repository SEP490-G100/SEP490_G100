using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.DTOs.Subscription;
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
    private readonly SubscriptionService _subscriptionService;

    public AdminController(
        DashboardService dashboardService,
        UserService userService,
        ExportService exportService,
        SubscriptionService subscriptionService)
    {
        _dashboardService    = dashboardService;
        _userService         = userService;
        _exportService       = exportService;
        _subscriptionService = subscriptionService;
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

    // ════════════════════════════════════════════════
    // SUBSCRIPTION PLAN MANAGEMENT
    // ════════════════════════════════════════════════

    // GET /api/admin/subscription-plans  — All plans (incl. inactive)
    [HttpGet("subscription-plans")]
    public async Task<IActionResult> GetAllPlans()
    {
        var plans = await _subscriptionService.getAllPlansForAdmin();
        return Ok(new { success = true, data = plans });
    }

    // POST /api/admin/subscription-plans  — Create plan
    [HttpPost("subscription-plans")]
    public async Task<IActionResult> CreatePlan([FromBody] AdminCreatePlanRequest request)
    {
        try
        {
            var plan = await _subscriptionService.createPlan(request);
            return Ok(new { success = true, message = "Tạo gói thành công.", data = plan });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // PATCH /api/admin/subscription-plans/{id}  — Update plan
    [HttpPatch("subscription-plans/{id:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] AdminUpdatePlanRequest request)
    {
        try
        {
            await _subscriptionService.updatePlan(id, request);
            return Ok(new { success = true, message = "Cập nhật gói thành công." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    // DELETE /api/admin/subscription-plans/{id}  — Soft delete
    [HttpDelete("subscription-plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        try
        {
            await _subscriptionService.deletePlan(id);
            return Ok(new { success = true, message = "Đã xóa gói thành công." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }
}
