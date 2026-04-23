using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Account;
using WebSite.Models.Admin;
using WebSite.Models.Moderator;

namespace WebSite.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DashboardController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/Admin/Dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/Admin/dashboard");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = $"Loi API lay du lieu Dashboard ({(int)response.StatusCode}). Vui long kiem tra lai quyen truy cap.";
                return View("~/Views/Admin/Dashboard.cshtml", new AdminDashboardDto());
            }

            var result = JsonSerializer.Deserialize<ApiResult<ApiDashboardStatsDto>>(json, JsonOpts);
            return View("~/Views/Admin/Dashboard.cshtml", result?.Data?.ToViewModel() ?? new AdminDashboardDto());
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi Dashboard: {ex.Message}";
            return View("~/Views/Admin/Dashboard.cshtml", new AdminDashboardDto());
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/Dashboard")]
    public async Task<IActionResult> DashboardModerator()
    {
        var model = new ModeratorDashboardDto();

        var dashboardRequest = new HttpRequestMessage(HttpMethod.Get, "/api/Moderator/dashboard");
        AttachToken(dashboardRequest);

        try
        {
            var dashboardResponse = await _http.SendAsync(dashboardRequest);
            var dashboardJson = await dashboardResponse.Content.ReadAsStringAsync();
            var dashboardResult = JsonSerializer.Deserialize<ApiResult<ApiModeratorDashboardStatsDto>>(dashboardJson, JsonOpts);
            model = dashboardResult?.Data?.ToViewModel() ?? new ModeratorDashboardDto();
        }
        catch
        {
            TempData["Error"] = "Khong the tai du lieu bang dieu khien.";
        }

        var recent = await FetchAccountsAsync(page: 1, pageSize: 5);
        model.RecentAccounts = recent?.Items ?? new List<AccountDto>();

        return View("~/Views/Moderator/Dashboard.cshtml", model);
    }

    private async Task<AccountListResponse?> FetchAccountsAsync(
        string? role = null,
        int? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 3)
    {
        var qs = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(role)) qs.Add($"role={Uri.EscapeDataString(role)}");
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");

        var url = $"/api/Moderator/moderator-view-account-list?{string.Join("&", qs)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AttachToken(request);

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

    private void AttachToken(HttpRequestMessage request)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
