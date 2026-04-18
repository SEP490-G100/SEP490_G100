using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Admin;

namespace WebSite.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin")]
public class AdminSubcriptionPlanController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AdminSubcriptionPlanController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [HttpGet("ManageSubscriptionPlan")]
    public async Task<IActionResult> ManageSubscriptionPlan(
        string? search = null,
        string? targetRole = null,
        bool? isActive = null,
        int page = 1)
    {
        ViewBag.Search = search ?? "";
        ViewBag.TargetRole = targetRole ?? "";
        ViewBag.IsActive = isActive;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(targetRole)) qs.Add($"targetRole={Uri.EscapeDataString(targetRole)}");
        if (isActive.HasValue) qs.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Admin/admin-view-subscription-plan-list?{string.Join("&", qs)}");
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
            TempData["Error"] = "Khong the tai danh sach subscription plan.";
            return View("~/Views/Admin/SubscriptionPlan/ManageSubscriptionPlan.cshtml", new AdminSubscriptionPlanListResponse());
        }
    }

    [HttpGet("CreateSubscriptionPlan")]
    public IActionResult CreateSubscriptionPlan() =>
        View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", new AdminSubscriptionPlanFormViewModel());

    [HttpPost("CreateSubscriptionPlan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubscriptionPlan(AdminSubscriptionPlanFormViewModel model)
    {
        ValidateSubscriptionPlanForm(model);
        if (!ModelState.IsValid)
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Admin/admin-create-subscription-plan")
        {
            Content = new StringContent(JsonSerializer.Serialize(BuildSubscriptionPlanPayload(model)), Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOpts);

            if (result?.Success == true && result.Data != null)
            {
                return RedirectToAction(nameof(ManageSubscriptionPlan), new
                {
                    toastType = "success",
                    toastMessage = "Bạn đã tạo gói subscription thành công"
                });
            }

            ModelState.AddModelError(nameof(model.Name), result?.Message ?? "Khong the tao subscription plan.");
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(nameof(model.Name), $"Loi ket noi: {ex.Message}");
            return View("~/Views/Admin/SubscriptionPlan/CreateSubscriptionPlan.cshtml", model);
        }
    }

    [HttpGet("ViewSubscriptionPlanDetail/{id:guid}")]
    public async Task<IActionResult> ViewSubscriptionPlanDetail(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Admin/admin-view-subscription-plan-detail/{id}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = result?.Message ?? "Khong tim thay subscription plan.";
                return RedirectToAction(nameof(ManageSubscriptionPlan));
            }

            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", AdminSubscriptionPlanFormViewModel.FromDetail(result.Data));
        }
        catch
        {
            TempData["Error"] = "Loi ket noi den API.";
            return RedirectToAction(nameof(ManageSubscriptionPlan));
        }
    }

    [HttpPost("UpdateSubscriptionPlan/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSubscriptionPlan(Guid id, AdminSubscriptionPlanFormViewModel model)
    {
        ValidateSubscriptionPlanForm(model);
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Admin/admin-update-subscription-plan/{id}")
        {
            Content = new StringContent(JsonSerializer.Serialize(BuildSubscriptionPlanPayload(model)), Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AdminSubscriptionPlanDetailViewModel>>(json, JsonOpts);

            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageSubscriptionPlan), new
                {
                    toastType = "success",
                    toastMessage = "Bạn đã chỉnh sửa gói subscription thành công"
                });
            }

            ModelState.AddModelError(nameof(model.Name), result?.Message ?? "Khong the cap nhat subscription plan.");
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(nameof(model.Name), $"Loi ket noi: {ex.Message}");
            model.Id = id;
            return View("~/Views/Admin/SubscriptionPlan/ViewSubscriptionPlanDetail.cshtml", model);
        }
    }

    [HttpPost("ToggleSubscriptionPlanStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSubscriptionPlanStatus(
        [FromForm] Guid id,
        [FromForm] bool isActive,
        [FromForm] string? returnUrl = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/Admin/admin-update-subscription-plan-status/{id}?isActive={isActive.ToString().ToLowerInvariant()}")
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
                var toastMessage = isActive
                    ? "Đã kích hoạt gói thành công"
                    : "Đã vô hiệu hóa gói thành công";
                var toastType = isActive ? "success" : "warning";
                return RedirectToReturnUrlOrList(
                    returnUrl,
                    toastType,
                    toastMessage);
            }

            return RedirectToReturnUrlOrList(
                returnUrl,
                "error",
                result?.Message ?? "Khong the cap nhat trang thai subscription plan.");
        }
        catch (Exception ex)
        {
            return RedirectToReturnUrlOrList(returnUrl, "error", $"Loi ket noi: {ex.Message}");
        }
    }

    private void AttachToken(HttpRequestMessage request)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void ValidateSubscriptionPlanForm(AdminSubscriptionPlanFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Description))
            ModelState.AddModelError(nameof(model.Description), "Please enter a description.");

        if (model.GetFeatures().Count == 0)
            ModelState.AddModelError(nameof(model.FeatureLines), "Please enter at least one feature.");
    }

    private static object BuildSubscriptionPlanPayload(AdminSubscriptionPlanFormViewModel model) => new
    {
        name = model.Name.Trim(),
        description = model.Description.Trim(),
        targetRole = model.TargetRole,
        price = model.Price,
        durationDays = model.DurationDays,
        sortOrder = model.SortOrder,
        features = model.GetFeatures(),
        canUseRecommendation = model.CanUseRecommendation,
        benefits = new
        {
            monthlyJobPostLimit = model.MonthlyJobPostLimit,
            monthlyApplicationLimit = model.MonthlyApplicationLimit,
            featuredBadge = model.FeaturedBadge,
            searchPriority = model.SearchPriority,
            listingDurationDays = model.ListingDurationDays
        }
    };

    private IActionResult RedirectToReturnUrlOrList(
        string? returnUrl,
        string? toastType = null,
        string? toastMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(AppendToastQuery(returnUrl, toastType, toastMessage));

        if (!string.IsNullOrWhiteSpace(toastMessage))
        {
            return RedirectToAction(
                nameof(ManageSubscriptionPlan),
                new { toastType = toastType ?? "info", toastMessage });
        }

        return RedirectToAction(nameof(ManageSubscriptionPlan));
    }

    private static string AppendToastQuery(string url, string? toastType, string? toastMessage)
    {
        var updatedUrl = url;

        if (!string.IsNullOrWhiteSpace(toastType))
            updatedUrl = AppendQuery(updatedUrl, "toastType", toastType);

        if (!string.IsNullOrWhiteSpace(toastMessage))
            updatedUrl = AppendQuery(updatedUrl, "toastMessage", toastMessage);

        return updatedUrl;
    }

    private static string AppendQuery(string url, string key, string value)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}
