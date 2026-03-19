using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Account;

namespace WebSite.Controllers;

//[Authorize(Roles = "Moderator")]
public class ModeratorController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModeratorController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/Dashboard
    // ──────────────────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        // Fetch a small page to get total counts for each role/status
        var allUsers = await FetchAccountsAsync(page: 1, pageSize: 1);
        var parents = await FetchAccountsAsync(role: "Parent", page: 1, pageSize: 1);
        var nannies = await FetchAccountsAsync(role: "Nanny", page: 1, pageSize: 1);
        var inactive = await FetchAccountsAsync(status: 0, page: 1, pageSize: 1);

        // Recent accounts (for activity feed)
        var recent = await FetchAccountsAsync(page: 1, pageSize: 5);

        ViewBag.TotalUsers = allUsers?.TotalCount ?? 0;
        ViewBag.TotalParents = parents?.TotalCount ?? 0;
        ViewBag.TotalNannies = nannies?.TotalCount ?? 0;
        ViewBag.TotalInactive = inactive?.TotalCount ?? 0;
        ViewBag.RecentAccounts = recent?.Items ?? new List<AccountDto>();

        return View();
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ManageAccount
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ManageAccount(
        string? search = null,
        string? role = null,
        int? status = null,
        int page = 1)
    {
        // Preserve query params for UI
        ViewBag.Search = search;
        ViewBag.Role = role;
        ViewBag.Status = status?.ToString() ?? "";

        var result = await FetchAccountsAsync(
            role: role,
            status: status,
            search: search,
            page: page,
            pageSize: 3);

        if (result == null)
        {
            TempData["Error"] = "Không thể tải danh sách tài khoản. Vui lòng thử lại.";
            result = new AccountListResponse();
        }

        return View(result);
    }

    // ──────────────────────────────────────────────
    // POST /Moderator/ToggleStatus
    // Called via AJAX from ManageAccount page
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id, int newStatus)
    {
        var body = JsonSerializer.Serialize(new { status = newStatus });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/accounts/{id}/status")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        // Gắn token nếu có
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            return Json(new
            {
                success = result?.Success ?? false,
                message = result?.Message ?? "Có lỗi xảy ra."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Lỗi kết nối: {ex.Message}" });
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/EditAccount/{id}
    // ──────────────────────────────────────────────
    public async Task<IActionResult> EditAccount(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Moderator/accounts/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AccountDto>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản.";
                return RedirectToAction(nameof(ManageAccount));
            }
            return View(result.Data);
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageAccount));
        }
    }

    // ──────────────────────────────────────────────
    // POST /Moderator/EditAccount/{id}
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccount(Guid id, EditAccountRequest model)
    {
        var body = JsonSerializer.Serialize(new { status = model.Status, phoneNumber = model.PhoneNumber });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/accounts/{id}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
                TempData["Success"] = result.Message ?? "Cập nhật thành công.";
            else
                TempData["Error"] = result?.Message ?? "Cập nhật thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageAccount));
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ManageNannyVerification
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ManageNannyVerification(string? search = null, int? status = null, int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");


        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/Moderator/verifications?{string.Join("&", qs)}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<WebSite.Models.Verification.VerificationRequestListResponse>>(json, JsonOpts);
            return View(result?.Data ?? new WebSite.Models.Verification.VerificationRequestListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách xác minh.";
            return View(new WebSite.Models.Verification.VerificationRequestListResponse());
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ViewNannyVerificationDetail/{id}
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ViewNannyVerificationDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Moderator/verifications/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<WebSite.Models.Verification.VerificationRequestDetailDto>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu xác minh.";
                return RedirectToAction(nameof(ManageNannyVerification));
            }
            return View(result.Data);
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
    }

    // ──────────────────────────────────────────────
    // POST /Moderator/ReviewVerification/{id}
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewVerification(Guid id, int action, string? rejectionReason)
    {
        var body = JsonSerializer.Serialize(new
        {
            action,
            rejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason.Trim()
        });
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/verifications/{id}/review")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
                TempData["Success"] = action == 2 ? "Đã duyệt hồ sơ thành công." : "Đã từ chối hồ sơ.";
            else
                TempData["Error"] = result?.Message ?? "Xử lý thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageNannyVerification));
    }

    public IActionResult ViewReports() => View();
    public IActionResult ManageBlogs() => View();
    public IActionResult ManageFAQ() => View();
    public IActionResult ModerateJobPostings() => View();


    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private async Task<AccountListResponse?> FetchAccountsAsync(
        string? role = null,
        int? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 3)
    {
        var token = HttpContext.Session.GetString("AccessToken");

        var qs = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrWhiteSpace(role)) qs.Add($"role={Uri.EscapeDataString(role)}");
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");

        var url = $"/api/Moderator/accounts?{string.Join("&", qs)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AccountListResponse>>(json, JsonOpts);
            return result?.Success == true ? result.Data : null;
        }
        catch
        {
            return null;
        }
    }
}