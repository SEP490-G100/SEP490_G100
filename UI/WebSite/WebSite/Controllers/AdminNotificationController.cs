using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Admin;

namespace WebSite.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin")]
public class AdminNotificationController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AdminNotificationController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
    }

    [HttpGet("ManageAdminNotification")]
    public async Task<IActionResult> ManageAdminNotification(string? search = null, bool? isDeleted = null, int page = 1)
    {
        ViewBag.Search = search ?? "";
        ViewBag.IsDeleted = isDeleted;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (isDeleted.HasValue) qs.Add($"isDeleted={isDeleted.Value.ToString().ToLowerInvariant()}");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Admin/admin-view-notification-list?{string.Join("&", qs)}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminNotificationListResponse>>(json, JsonOpts);
            return View("~/Views/Admin/AdminNotification/ManageAdminNotification.cshtml", result?.Data ?? new AdminNotificationListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách thông báo admin.";
            return View("~/Views/Admin/AdminNotification/ManageAdminNotification.cshtml", new AdminNotificationListResponse());
        }
    }

    [HttpGet("CreateAdminNotification")]
    public async Task<IActionResult> CreateAdminNotification()
    {
        await PopulateAdminNotificationRolesAsync();
        return View("~/Views/Admin/AdminNotification/CreateAdminNotification.cshtml", new AdminNotificationFormViewModel
        {
            TargetRole = "All"
        });
    }

    [HttpPost("CreateAdminNotification")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdminNotification(AdminNotificationFormViewModel model)
    {
        ValidateAdminNotificationForm(model);
        await PopulateAdminNotificationRolesAsync();
        if (!ModelState.IsValid)
            return View("~/Views/Admin/AdminNotification/CreateAdminNotification.cshtml", model);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Admin/admin-create-notification")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                title = model.Title.Trim(),
                content = model.Content.Trim(),
                targetType = string.Equals(model.TargetRole, "All", StringComparison.OrdinalIgnoreCase) ? "All" : "Role",
                targetRole = string.Equals(model.TargetRole, "All", StringComparison.OrdinalIgnoreCase) ? null : model.TargetRole?.Trim()
            }), Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminNotificationDetailViewModel>>(json, JsonOpts);

            if (result?.Success == true && result.Data != null)
            {
                await PushAdminNotificationRealtime(result.Data);
                TempData["Success"] = "Tạo thông báo admin thành công.";
                return RedirectToAction(nameof(ManageAdminNotification));
            }

            ModelState.AddModelError(string.Empty, result?.Message ?? "Không thể tạo thông báo admin.");
            return View("~/Views/Admin/AdminNotification/CreateAdminNotification.cshtml", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Lỗi kết nối: {ex.Message}");
            return View("~/Views/Admin/AdminNotification/CreateAdminNotification.cshtml", model);
        }
    }

    [HttpGet("ViewAdminNotificationDetail/{id:guid}")]
    public async Task<IActionResult> ViewAdminNotificationDetail(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Admin/admin-view-notification-detail/{id}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminNotificationDetailViewModel>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = result?.Message ?? "Không tìm thấy thông báo admin.";
                return RedirectToAction(nameof(ManageAdminNotification));
            }

            return View("~/Views/Admin/AdminNotification/ViewAdminNotificationDetail.cshtml", AdminNotificationFormViewModel.FromDetail(result.Data));
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageAdminNotification));
        }
    }

    [HttpPost("UpdateAdminNotification/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAdminNotification(Guid id, AdminNotificationFormViewModel model)
    {
        ValidateAdminNotificationForm(model);
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View("~/Views/Admin/AdminNotification/ViewAdminNotificationDetail.cshtml", model);
        }

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Admin/admin-update-notification/{id}")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                title = model.Title.Trim(),
                content = model.Content.Trim(),
                targetType = model.TargetType,
                targetRole = model.TargetType == "Role" ? model.TargetRole?.Trim() : null
            }), Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminNotificationDetailViewModel>>(json, JsonOpts);

            if (result?.Success == true)
            {
                TempData["Success"] = "Cập nhật thông báo admin thành công.";
                return RedirectToAction(nameof(ManageAdminNotification));
            }

            ModelState.AddModelError(string.Empty, result?.Message ?? "Không thể cập nhật thông báo admin.");
            model.Id = id;
            return View("~/Views/Admin/AdminNotification/ViewAdminNotificationDetail.cshtml", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Lỗi kết nối: {ex.Message}");
            model.Id = id;
            return View("~/Views/Admin/AdminNotification/ViewAdminNotificationDetail.cshtml", model);
        }
    }

    [HttpPost("ToggleAdminNotificationStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdminNotificationStatus(
        [FromForm] Guid id,
        [FromForm] bool isDeleted,
        [FromForm] string? returnUrl = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/Admin/admin-update-notification-status/{id}?isDeleted={isDeleted.ToString().ToLowerInvariant()}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            TempData[result?.Success == true ? "Success" : "Error"] =
                result?.Message ?? "Không thể cập nhật trạng thái thông báo admin.";
            return RedirectToAdminNotificationReturnUrlOrList(returnUrl);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return RedirectToAdminNotificationReturnUrlOrList(returnUrl);
        }
    }

    private void AttachToken(HttpRequestMessage request)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void ValidateAdminNotificationForm(AdminNotificationFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");

        if (string.IsNullOrWhiteSpace(model.Content))
            ModelState.AddModelError(nameof(model.Content), "Content is required.");

        if (string.IsNullOrWhiteSpace(model.TargetRole))
            ModelState.AddModelError(nameof(model.TargetRole), "Role recipient is required.");

        if (string.Equals(model.TargetRole, "Admin", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.TargetRole), "Admin cannot receive admin broadcast notifications.");
    }

    private IActionResult RedirectToAdminNotificationReturnUrlOrList(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(ManageAdminNotification));
    }

    private async Task PushAdminNotificationRealtime(AdminNotificationDetailViewModel notification)
    {
        var payload = new
        {
            title = notification.Title,
            content = notification.Content,
            type = "admin-broadcast",
            actionUrl = (string?)null
        };

        if (string.Equals(notification.TargetType, "Role", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(notification.TargetRole))
        {
            await _notificationHub.Clients.Group($"role:{notification.TargetRole}")
                .SendAsync("notification:new", payload);
            return;
        }

        foreach (var role in new[] { "Parent", "Nanny", "Moderator" })
        {
            await _notificationHub.Clients.Group($"role:{role}")
                .SendAsync("notification:new", payload);
        }
    }

    private async Task PopulateAdminNotificationRolesAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/Admin/admin-view-notification-role-list");
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<List<string>>>(json, JsonOpts);
            ViewBag.AdminNotificationRoles = result?.Data ?? new List<string>();
        }
        catch
        {
            ViewBag.AdminNotificationRoles = new List<string>();
        }
    }
}
