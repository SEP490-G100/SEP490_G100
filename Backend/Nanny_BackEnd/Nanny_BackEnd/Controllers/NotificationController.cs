using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            return Unauthorized(fail("Khong xac dinh duoc nguoi dung hien tai."));

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
            return Unauthorized(fail("Khong xac dinh duoc nguoi dung hien tai."));

        var unreadCount = await _notificationService.getUnreadCount(userId.Value);
        return Ok(new { success = true, data = new { unreadCount } });
    }

    [HttpPost("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(fail("Khong xac dinh duoc nguoi dung hien tai."));

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
            return Unauthorized(fail("Khong xac dinh duoc nguoi dung hien tai."));

        var affected = await _notificationService.markAllAsRead(userId.Value);
        return Ok(new
        {
            success = true,
            message = affected == 0
                ? "Khong co thong bao nao can cap nhat."
                : $"Da danh dau da doc {affected} thong bao.",
            data = new { affected }
        });
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    private static object fail(string message) => new { success = false, message };
}
