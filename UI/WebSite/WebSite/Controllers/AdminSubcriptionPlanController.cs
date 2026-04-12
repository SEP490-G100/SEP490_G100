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
            TempData["Error"] = "Không thể tải danh sách subscription plan.";
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
                TempData["Success"] = result.Message;
                return RedirectToReturnUrlOrList(returnUrl);
            }

            TempData["Error"] = result?.Message ?? "Không thể cập nhật trạng thái subscription plan.";
            return RedirectToReturnUrlOrList(returnUrl);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return RedirectToReturnUrlOrList(returnUrl);
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
        if (model.GetFeatures().Count == 0)
            ModelState.AddModelError(nameof(model.FeatureLines), "Please enter at least one feature.");
    }

    private static object BuildSubscriptionPlanPayload(AdminSubscriptionPlanFormViewModel model) => new
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

    private IActionResult RedirectToReturnUrlOrList(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(ManageSubscriptionPlan));
    }
}
