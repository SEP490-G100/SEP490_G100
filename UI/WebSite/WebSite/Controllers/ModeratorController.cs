using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Account;

namespace WebSite.Controllers;

[Authorize(Roles = "Moderator,Admin")]
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
        var allUsers  = await FetchAccountsAsync(page: 1, pageSize: 1);
        var parents   = await FetchAccountsAsync(role: "Parent",    page: 1, pageSize: 1);
        var nannies   = await FetchAccountsAsync(role: "Nanny",     page: 1, pageSize: 1);
        var inactive  = await FetchAccountsAsync(status: 1,         page: 1, pageSize: 1);

        // Recent accounts (for activity feed)
        var recent    = await FetchAccountsAsync(page: 1, pageSize: 5);

        ViewBag.TotalUsers    = allUsers?.TotalCount  ?? 0;
        ViewBag.TotalParents  = parents?.TotalCount   ?? 0;
        ViewBag.TotalNannies  = nannies?.TotalCount   ?? 0;
        ViewBag.TotalInactive = inactive?.TotalCount  ?? 0;
        ViewBag.RecentAccounts = recent?.Items ?? new List<AccountDto>();

        return View();
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ManageAccount
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ManageAccount(
        string? search = null,
        string? role   = null,
        int?    status = null,
        int     page   = 1)
    {
        // Preserve query params for UI
        ViewBag.Search = search;
        ViewBag.Role   = role;
        ViewBag.Status = status?.ToString() ?? "";

        var result = await FetchAccountsAsync(
            role:     role,
            status:   status,
            search:   search,
            page:     page,
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
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token))
            return Json(new { success = false, message = "Phiên đăng nhập hết hạn." });

        var body    = JsonSerializer.Serialize(new { status = newStatus });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/accounts/{id}/status")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

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
    // Placeholder views (will be implemented later)
    // ──────────────────────────────────────────────
    public IActionResult ViewReports()       => View();
    public IActionResult NannyVerification() => View();
    public IActionResult ManageBlogs()       => View();
    public IActionResult ManageFAQ()         => View();
    public IActionResult ModerateJobPostings() => View();

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private async Task<AccountListResponse?> FetchAccountsAsync(
        string? role   = null,
        int?    status = null,
        string? search = null,
        int     page   = 1,
        int     pageSize = 10)
    {
        var token = HttpContext.Session.GetString("AccessToken");

        var qs = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrWhiteSpace(role))   qs.Add($"role={Uri.EscapeDataString(role)}");
        if (status.HasValue)                     qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(search))  qs.Add($"search={Uri.EscapeDataString(search)}");

        var url     = $"/api/account?{string.Join("&", qs)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult<AccountListResponse>>(json, JsonOpts);
            return result?.Success == true ? result.Data : null;
        }
        catch
        {
            return null;
        }
    }
}
