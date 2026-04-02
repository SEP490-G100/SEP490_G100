using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.DTOs.Notification;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Repositories;
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

    [HttpGet("subscription-plans")]
    public async Task<IActionResult> GetSubscriptionPlans(
        [FromServices] SubscriptionService subscriptionService,
        [FromQuery] string? search = null,
        [FromQuery] string? targetRole = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var result = await subscriptionService.getAdminPlans(search, targetRole, isActive, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("subscription-plans/{id:guid}")]
    public async Task<IActionResult> GetSubscriptionPlanDetail(
        Guid id,
        [FromServices] SubscriptionService subscriptionService)
    {
        var plan = await subscriptionService.getAdminPlanDetail(id);
        return plan == null
            ? NotFound(new { success = false, message = "Khong tim thay goi subscription." })
            : Ok(new { success = true, data = plan });
    }

    [HttpPost("subscription-plans")]
    public async Task<IActionResult> CreateSubscriptionPlan(
        [FromBody] AdminSubscriptionPlanUpsertRequest request,
        [FromServices] SubscriptionService subscriptionService)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Du lieu khong hop le.", errors = ModelState });

        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var plan = await subscriptionService.createAdminPlan(adminUserId.Value, request);
            return Ok(new { success = true, message = "Tao goi subscription thanh cong.", data = plan });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("subscription-plans/{id:guid}")]
    public async Task<IActionResult> UpdateSubscriptionPlan(
        Guid id,
        [FromBody] AdminSubscriptionPlanUpsertRequest request,
        [FromServices] SubscriptionService subscriptionService)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Du lieu khong hop le.", errors = ModelState });

        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var plan = await subscriptionService.updateAdminPlan(id, adminUserId.Value, request);
            return Ok(new { success = true, message = "Cap nhat goi subscription thanh cong.", data = plan });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("subscription-plans/{id:guid}/status")]
    public async Task<IActionResult> ToggleSubscriptionPlanStatus(
        Guid id,
        [FromBody] AdminSubscriptionPlanStatusRequest? request,
        [FromQuery] bool? isActive,
        [FromServices] SubscriptionService subscriptionService)
    {
        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        var targetIsActive = isActive ?? request?.IsActive;
        if (!targetIsActive.HasValue)
            return BadRequest(new { success = false, message = "Thieu trang thai kich hoat cua goi subscription." });

        try
        {
            await subscriptionService.toggleAdminPlanStatus(id, adminUserId.Value, targetIsActive.Value);
            return Ok(new
            {
                success = true,
                message = targetIsActive.Value
                    ? "Da kich hoat goi subscription."
                    : "Da vo hieu hoa goi subscription."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetAdminNotifications(
        [FromQuery] string? search = null,
        [FromQuery] bool? isDeleted = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3,
        [FromServices] NotificationService notificationService = null!)
    {
        var result = await notificationService.getAdminNotifications(search, isDeleted, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("notification-roles")]
    public async Task<IActionResult> GetAdminNotificationRoles([FromServices] UserRepository userRepository)
    {
        var roles = await userRepository.GetNotificationAssignableRolesAsync();
        return Ok(new { success = true, data = roles });
    }

    [HttpGet("notifications/{id:guid}")]
    public async Task<IActionResult> GetAdminNotificationDetail(
        Guid id,
        [FromServices] NotificationService notificationService)
    {
        var result = await notificationService.getAdminNotificationDetail(id);
        return result == null
            ? NotFound(new { success = false, message = "Khong tim thay thong bao admin." })
            : Ok(new { success = true, data = result });
    }

    [HttpPost("notifications")]
    public async Task<IActionResult> CreateAdminNotification(
        [FromBody] AdminNotificationUpsertRequest request,
        [FromServices] NotificationService notificationService)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Du lieu khong hop le.", errors = ModelState });

        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var result = await notificationService.createAdminNotification(adminUserId.Value, request);
            return Ok(new { success = true, message = "Tao thong bao admin thanh cong.", data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("notifications/{id:guid}")]
    public async Task<IActionResult> UpdateAdminNotification(
        Guid id,
        [FromBody] AdminNotificationUpsertRequest request,
        [FromServices] NotificationService notificationService)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Du lieu khong hop le.", errors = ModelState });

        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var result = await notificationService.updateAdminNotification(id, adminUserId.Value, request);
            return Ok(new { success = true, message = "Cap nhat thong bao admin thanh cong.", data = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("notifications/{id:guid}/status")]
    public async Task<IActionResult> ToggleAdminNotificationStatus(
        Guid id,
        [FromQuery] bool? isDeleted,
        [FromServices] NotificationService notificationService)
    {
        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        if (!isDeleted.HasValue)
            return BadRequest(new { success = false, message = "Thieu trang thai kich hoat cua thong bao." });

        try
        {
            await notificationService.toggleAdminNotificationStatus(id, adminUserId.Value, isDeleted.Value);
            return Ok(new
            {
                success = true,
                message = isDeleted.Value
                    ? "Da vo hieu hoa thong bao admin."
                    : "Da kich hoat thong bao admin."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
