using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Notification;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly IAdminNotificationService _adminNotificationService;

    public AdminController(
        IExportService exportService,
        IAdminNotificationService adminNotificationService)
    {
        _exportService = exportService;
        _adminNotificationService = adminNotificationService;
    }

    [HttpGet("export")]
    [HttpGet("export-system-data")]
    public async Task<IActionResult> ExportSystemData()
    {
        var fileContents = await _exportService.ExportSystemDataToExcelAsync();
        return File(
            fileContents,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"NannyMatch_SystemData_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("admin-view-notification-list")]
    public async Task<IActionResult> AdminViewNotificationList(
        [FromQuery] string? search = null,
        [FromQuery] bool? isDeleted = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var result = await _adminNotificationService.AdminViewNotificationListAsync(search, isDeleted, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("admin-view-notification-role-list")]
    public async Task<IActionResult> AdminViewNotificationRoleList()
    {
        var roles = await _adminNotificationService.AdminViewNotificationRoleListAsync();
        return Ok(new { success = true, data = roles });
    }

    [HttpGet("admin-view-notification-detail/{id:guid}")]
    public async Task<IActionResult> AdminViewNotificationDetail(Guid id)
    {
        var result = await _adminNotificationService.AdminViewNotificationDetailAsync(id);
        return result == null
            ? NotFound(new { success = false, message = "Khong tim thay thong bao admin." })
            : Ok(new { success = true, data = result });
    }

    [HttpPost("admin-create-notification")]
    public async Task<IActionResult> AdminCreateNotification([FromBody] AdminNotificationUpsertRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var result = await _adminNotificationService.AdminCreateNotificationAsync(adminUserId.Value, request);
            return Ok(new { success = true, message = "Tao thong bao admin thanh cong.", data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("admin-update-notification/{id:guid}")]
    public async Task<IActionResult> AdminUpdateNotification(Guid id, [FromBody] AdminNotificationUpsertRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var result = await _adminNotificationService.AdminUpdateNotificationAsync(id, adminUserId.Value, request);
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

    [HttpPatch("admin-update-notification-status/{id:guid}")]
    public async Task<IActionResult> AdminUpdateNotificationStatus(Guid id, [FromQuery] bool? isDeleted)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        if (!isDeleted.HasValue)
            return BadRequest(new { success = false, message = "Thieu trang thai kich hoat cua thong bao." });

        try
        {
            await _adminNotificationService.AdminUpdateNotificationStatusAsync(id, adminUserId.Value, isDeleted.Value);
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

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

   
}
