using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Account;
using WebSite.Models.Admin;

namespace WebSite.Controllers;

//[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AdminController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
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
            return View(result?.Data ?? new AccountListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách Moderator.";
            return View(new AccountListResponse());
        }
    }

    // ── Create Moderator GET ────────────────────────────
    public IActionResult CreateModerator() => View(new CreateModeratorRequest());

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
                return View(model);
            }
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                TempData["Success"] = result.Message ?? "Tạo Moderator thành công!";
                return RedirectToAction(nameof(ManageModerators));
            }
            TempData["Error"] = result?.Message ?? "Tạo Moderator thất bại.";
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return View(model);
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
            return View(result.Data);
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

    // ── Helper ─────────────────────────────────────────
    private void AttachToken(HttpRequestMessage req)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}