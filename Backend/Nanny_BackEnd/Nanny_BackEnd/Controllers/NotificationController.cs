using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Notification;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        var notifications = await _notificationService.getMyNotifications(userId.Value, page, pageSize);
        return Ok(new
        {
            success = true,
            total = notifications.Total,
            unreadCount = notifications.UnreadCount,
            data = notifications.Items
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        var unreadCount = await _notificationService.getUnreadCount(userId.Value);
        return Ok(new { success = true, data = new { unreadCount } });
    }

    [HttpPost("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        try
        {
            var notification = await _notificationService.markAsRead(userId.Value, id);
            return Ok(new { success = true, data = notification });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(fail(ex.Message));
        }
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Không xác định được người dùng hiện tại."));

        var affected = await _notificationService.markAllAsRead(userId.Value);
        return Ok(new
        {
            success = true,
            message = affected == 0
                ? "Không có thông báo nào cần cập nhật."
                : $"Đã đánh dấu đã đọc {affected} thông báo.",
            data = new { affected }
        });
    }

    [HttpGet("/api/Admin/admin-view-notification-list")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminViewNotificationList(
        [FromQuery] string? search = null,
        [FromQuery] bool? isDeleted = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var result = await _notificationService.getAdminNotifications(search, isDeleted, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("/api/Admin/admin-view-notification-role-list")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminViewNotificationRoleList()
    {
        var roles = new[] { "Parent", "Nanny", "Moderator" };
        return Ok(new { success = true, data = roles });
    }

    [HttpGet("/api/Admin/admin-view-notification-detail/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminViewNotificationDetail(Guid id)
    {
        var result = await _notificationService.getAdminNotificationDetail(id);
        return result == null
            ? NotFound(new { success = false, message = "Không tìm thấy thông báo admin." })
            : Ok(new { success = true, data = result });
    }

    [HttpPost("/api/Admin/admin-create-notification")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminCreateNotification([FromBody] AdminNotificationUpsertRequest request)
    {
        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(fail("Không xác định được admin hiện tại."));

        try
        {
            var result = await _notificationService.createAdminNotification(adminUserId.Value, request);
            return Ok(new { success = true, message = "Tạo thông báo admin thành công.", data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(fail(ex.Message));
        }
    }

    [HttpPatch("/api/Admin/admin-update-notification/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateNotification(Guid id, [FromBody] AdminNotificationUpsertRequest request)
    {
        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(fail("Không xác định được admin hiện tại."));

        try
        {
            var result = await _notificationService.updateAdminNotification(id, adminUserId.Value, request);
            return Ok(new { success = true, message = "Cập nhật thông báo admin thành công.", data = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(fail(ex.Message));
        }
    }

    [HttpPatch("/api/Admin/admin-update-notification-status/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateNotificationStatus(Guid id, [FromQuery] bool? isDeleted)
    {
        var adminUserId = getCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(fail("Không xác định được admin hiện tại."));

        if (!isDeleted.HasValue)
            return BadRequest(fail("Thiếu trạng thái kích hoạt của thông báo."));

        try
        {
            await _notificationService.toggleAdminNotificationStatus(id, adminUserId.Value, isDeleted.Value);
            return Ok(new
            {
                success = true,
                message = isDeleted.Value
                    ? "Đã vô hiệu hóa thông báo admin."
                    : "Đã kích hoạt thông báo admin."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(fail(ex.Message));
        }
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    private static object fail(string message) => new { success = false, message };
}
