using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Admin;

namespace WebSite.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin")]
public class AdminDashboardController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AdminDashboardController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [HttpGet("Dashboard")]
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
                TempData["Error"] = $"Lỗi API lấy dữ liệu Dashboard ({(int)response.StatusCode}). Vui lòng kiểm tra lại quyền truy cập.";
                return View("~/Views/Admin/Dashboard.cshtml", new AdminDashboardDto());
            }

            var result = JsonSerializer.Deserialize<ApiResult<ApiDashboardStatsDto>>(json, JsonOpts);
            return View("~/Views/Admin/Dashboard.cshtml", result?.Data?.ToViewModel() ?? new AdminDashboardDto());
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối Dashboard: {ex.Message}";
            return View("~/Views/Admin/Dashboard.cshtml", new AdminDashboardDto());
        }
    }

    private void AttachToken(HttpRequestMessage request)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
