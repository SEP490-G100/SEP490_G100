using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Account;
using WebSite.Models.Admin;

namespace WebSite.Controllers;

//[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AdminController(IHttpClientFactory httpFactory, IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
    }

    public async Task<IActionResult> Dashboard()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Lỗi API lấy dữ liệu Dashboard ({(int)response.StatusCode}). Vui lòng kiểm tra lại quyền truy cập.";
                return View(new AdminDashboardDto());
            }

            var result = JsonSerializer.Deserialize<ApiResult<ApiDashboardStatsDto>>(json, JsonOpts);
            return View(result?.Data?.ToViewModel() ?? new AdminDashboardDto());
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối Dashboard: {ex.Message}";
            return View(new AdminDashboardDto());
        }
    }

    // ── Manage Moderators (list) ───────────────────────
    public async Task<IActionResult> ManageModerators(string? search = null, int? status = null, int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status?.ToString() ?? "";

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (status.HasValue) qs.Add($"status={status.Value}");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/moderators?{string.Join("&", qs)}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AccountListResponse>>(json, JsonOpts);
            return View("~/Views/Admin/ModeratorAccount/ManageModerators.cshtml", result?.Data ?? new AccountListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách Moderator.";
            return View("~/Views/Admin/ModeratorAccount/ManageModerators.cshtml", new AccountListResponse());
        }
    }

    // ── Create Moderator GET ────────────────────────────
    public IActionResult CreateModerator() => View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", new CreateModeratorRequest());

    // ── Create Moderator POST ───────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModerator(CreateModeratorRequest model)
    {
        var body = JsonSerializer.Serialize(new
        {
            email = model.Email,
            password = model.Password,
            firstName = model.FirstName,
            lastName = model.LastName,
            phoneNumber = model.PhoneNumber
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/moderators")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                TempData["Error"] = $"API trả về rỗng (HTTP {(int)response.StatusCode}).";
                return View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", model);
            }
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                TempData["Success"] = result.Message ?? "Tạo Moderator thành công!";
                return RedirectToAction(nameof(ManageModerators));
            }
            TempData["Error"] = result?.Message ?? "Tạo Moderator thất bại.";
            return View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", model);
        }
    }

    // ── Edit Moderator GET ──────────────────────────────
    public async Task<IActionResult> EditModerator(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/moderators/{id}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AccountDto>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy Moderator.";
                return RedirectToAction(nameof(ManageModerators));
            }
            return View("~/Views/Admin/ModeratorAccount/EditModerator.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối.";
            return RedirectToAction(nameof(ManageModerators));
        }
    }

    // ── Edit Moderator POST ─────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditModerator(Guid id, UpdateModeratorRequest model)
    {
        var body = JsonSerializer.Serialize(new
        {
            firstName = model.FirstName,
            lastName = model.LastName,
            phoneNumber = model.PhoneNumber,
            status = model.Status
        });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/moderators/{id}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                TempData["Error"] = $"API trả về rỗng (HTTP {(int)response.StatusCode}).";
                return RedirectToAction(nameof(EditModerator), new { id });
            }
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true) { TempData["Success"] = result.Message; return RedirectToAction(nameof(ManageModerators)); }
            TempData["Error"] = result?.Message ?? "Cập nhật thất bại.";
            return RedirectToAction(nameof(EditModerator), new { id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return RedirectToAction(nameof(EditModerator), new { id });
        }
    }

    // ── Delete Moderator POST ───────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteModerator(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/moderators/{id}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            TempData[result?.Success == true ? "Success" : "Error"] = result?.Message ?? "Đã xoá.";
        }
        catch (Exception ex) { TempData["Error"] = $"Lỗi: {ex.Message}"; }
        return RedirectToAction(nameof(ManageModerators));
    }

    public async Task<IActionResult> ManageSubscriptionPlan(string? search = null, string? targetRole = null, bool? isActive = null, int page = 1)
    {
        ViewBag.Search = search ?? "";
        ViewBag.TargetRole = targetRole ?? "";
        ViewBag.IsActive = isActive;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(targetRole)) qs.Add($"targetRole={Uri.EscapeDataString(targetRole)}");
        if (isActive.HasValue) qs.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/subscription-plans?{string.Join("&", qs)}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanListResponse>>(json, JsonOpts);
            return View("~/Views/Admin/SubscriptionPlan/ManageSubscriptionPlan.cshtml", result?.Data ?? new AdminSubscriptionPlanListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách subscription plan.";
            return View("~/Views/Admin/SubscriptionPlan/ManageSubscriptionPlan.cshtml", new AdminSubscriptionPlanListResponse());
        }
    }

    [HttpGet]
    public IActionResult CreateSubscriptionPlan() =>
        View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", new AdminSubscriptionPlanFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubscriptionPlan(AdminSubscriptionPlanFormViewModel model)
    {
        validateSubscriptionPlanForm(model);
        if (!ModelState.IsValid)
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/subscription-plans")
        {
            Content = new StringContent(JsonSerializer.Serialize(buildSubscriptionPlanPayload(model)), Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOpts);

            if (result?.Success == true && result.Data != null)
            {
                TempData["Success"] = "Tạo gói subscription thành công.";
                return RedirectToAction(nameof(ManageSubscriptionPlan));
            }

            ModelState.AddModelError(string.Empty, result?.Message ?? "Không thể tạo subscription plan.");
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Lỗi kết nối: {ex.Message}");
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> ViewSubscriptionPlanDetail(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/subscription-plans/{id}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = result?.Message ?? "Không tìm thấy subscription plan.";
                return RedirectToAction(nameof(ManageSubscriptionPlan));
            }

            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", AdminSubscriptionPlanFormViewModel.FromDetail(result.Data));
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageSubscriptionPlan));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSubscriptionPlan(Guid id, AdminSubscriptionPlanFormViewModel model)
    {
        validateSubscriptionPlanForm(model);
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/subscription-plans/{id}")
        {
            Content = new StringContent(JsonSerializer.Serialize(buildSubscriptionPlanPayload(model)), Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOpts);

            if (result?.Success == true)
            {
                TempData["Success"] = "Cập nhật gói subscription thành công.";
                return RedirectToAction(nameof(ManageSubscriptionPlan));
            }

            ModelState.AddModelError(string.Empty, result?.Message ?? "Không thể cập nhật subscription plan.");
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Lỗi kết nối: {ex.Message}");
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSubscriptionPlanStatus([FromForm] Guid id, [FromForm] bool isActive, [FromForm] string? returnUrl = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/admin/subscription-plans/{id}/status?isActive={isActive.ToString().ToLowerInvariant()}")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { isActive }), Encoding.UTF8, "application/json")
        };
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                TempData["Success"] = result.Message;
                return redirectToReturnUrlOrList(returnUrl);
            }

            TempData["Error"] = result?.Message ?? "Không thể cập nhật trạng thái subscription plan.";
            return redirectToReturnUrlOrList(returnUrl);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return redirectToReturnUrlOrList(returnUrl);
        }
    }

    public async Task<IActionResult> ManageAdminNotification(string? search = null, bool? isDeleted = null, int page = 1)
    {
        ViewBag.Search = search ?? "";
        ViewBag.IsDeleted = isDeleted;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (isDeleted.HasValue) qs.Add($"isDeleted={isDeleted.Value.ToString().ToLowerInvariant()}");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/notifications?{string.Join("&", qs)}");
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

    [HttpGet]
    public async Task<IActionResult> CreateAdminNotification()
    {
        await populateAdminNotificationRolesAsync();
        return View("~/Views/Admin/AdminNotification/CreateAdminNotification.cshtml", new AdminNotificationFormViewModel
        {
            TargetRole = "All"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdminNotification(AdminNotificationFormViewModel model)
    {
        validateAdminNotificationForm(model);
        await populateAdminNotificationRolesAsync();
        if (!ModelState.IsValid)
            return View("~/Views/Admin/AdminNotification/CreateAdminNotification.cshtml", model);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/notifications")
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
                await pushAdminNotificationRealtime(result.Data);
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

    [HttpGet]
    public async Task<IActionResult> ViewAdminNotificationDetail(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/notifications/{id}");
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAdminNotification(Guid id, AdminNotificationFormViewModel model)
    {
        validateAdminNotificationForm(model);
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View("~/Views/Admin/AdminNotification/ViewAdminNotificationDetail.cshtml", model);
        }

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/notifications/{id}")
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdminNotificationStatus([FromForm] Guid id, [FromForm] bool isDeleted, [FromForm] string? returnUrl = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/admin/notifications/{id}/status?isDeleted={isDeleted.ToString().ToLowerInvariant()}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            TempData[result?.Success == true ? "Success" : "Error"] =
                result?.Message ?? "Không thể cập nhật trạng thái thông báo admin.";
            return redirectToAdminNotificationReturnUrlOrList(returnUrl);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return redirectToAdminNotificationReturnUrlOrList(returnUrl);
        }
    }


    // ── Export System Data ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ExportData()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/export");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Lỗi khi xuất dữ liệu: HTTP {(int)response.StatusCode}";
                return RedirectToAction("Dashboard");
            }

            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Replace("\"", "")
                ?? $"NannyMatch_SystemData_{DateTime.Now:yyyyMMdd}.xlsx";

            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối khi xuất Excel: {ex.Message}";
            return RedirectToAction("Dashboard");
        }
    }

    // ── Recommendation Config ───────────────────────────

    public async Task<IActionResult> RecommendationConfig()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/recommendation/config/weights");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<ScoringWeightsDto>>(json, JsonOpts);
            return View(result?.Data ?? new ScoringWeightsDto());
        }
        catch
        {
            TempData["Error"] = "Không thể tải cấu hình recommendation.";
            return View(new ScoringWeightsDto());
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateWeight([FromBody] UpdateWeightDto body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/recommendation/config/weights")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { key = body.Key, value = body.Value }),
                Encoding.UTF8, "application/json")
        };
        AttachToken(request);
        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> ReembedBatch([FromQuery] bool force = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/recommendation/reembed/batch?force={force.ToString().ToLower()}");
        AttachToken(request);
        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    // ── Helper ─────────────────────────────────────────
    private void AttachToken(HttpRequestMessage req)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void validateSubscriptionPlanForm(AdminSubscriptionPlanFormViewModel model)
    {
        if (model.GetFeatures().Count == 0)
            ModelState.AddModelError(nameof(model.FeatureLines), "Please enter at least one feature.");
    }

    private static object buildSubscriptionPlanPayload(AdminSubscriptionPlanFormViewModel model) => new
    {
        name = model.Name.Trim(),
        description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
        targetRole = model.TargetRole,
        price = model.Price,
        durationDays = model.DurationDays,
        sortOrder = model.SortOrder,
        features = model.GetFeatures(),
        benefits = new
        {
            monthlyJobPostLimit = model.MonthlyJobPostLimit,
            monthlyApplicationLimit = model.MonthlyApplicationLimit,
            featuredBadge = model.FeaturedBadge,
            searchPriority = model.SearchPriority,
            listingDurationDays = model.ListingDurationDays
        }
    };

    private IActionResult redirectToReturnUrlOrList(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(ManageSubscriptionPlan));
    }

    private void validateAdminNotificationForm(AdminNotificationFormViewModel model)
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

    private IActionResult redirectToAdminNotificationReturnUrlOrList(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(ManageAdminNotification));
    }

    private async Task pushAdminNotificationRealtime(AdminNotificationDetailViewModel notification)
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
            await _notificationHub.Clients.Group($"role:{notification.TargetRole}").SendAsync("notification:new", payload);
            return;
        }

        foreach (var role in new[] { "Parent", "Nanny", "Moderator" })
        {
            await _notificationHub.Clients.Group($"role:{role}").SendAsync("notification:new", payload);
        }
    }

    private async Task populateAdminNotificationRolesAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/notification-roles");
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


// ── Internal DTOs ───────────────────────────────────────
public class ScoringWeightsDto
{
    public double SemanticWeight  { get; set; } = 0.80;
    public double SalaryWeight    { get; set; } = 0.12;
    public double DistanceWeight  { get; set; } = 0.08;
    public double ColdStartScore  { get; set; } = 0.75;
}

public class UpdateWeightDto
{
    public string Key   { get; set; } = string.Empty;
    public double Value { get; set; }
}
