using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Account;

namespace WebSite.Controllers;

[Authorize(Roles = "Moderator")]
[Route("Moderator")]
public class ModeratorAccountController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModeratorAccountController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    // GET /Moderator/ManageAccount
    [HttpGet("ManageAccount")]
    public async Task<IActionResult> ManageAccount(
        string? search = null,
        string? role = null,
        int? status = null,
        int page = 1)
    {
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
            TempData["Error"] = "Khong the tai danh sach tai khoan. Vui long thu lai.";
            result = new AccountListResponse();
        }

        return View("~/Views/Moderator/Account/ManageAccount.cshtml", result);
    }

    // POST /Moderator/ToggleStatus
    [HttpPost("ToggleStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id, int newStatus)
    {
        var body = JsonSerializer.Serialize(new { status = newStatus });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/moderator-toggle-account-status/{id}/status")
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

            return Json(new
            {
                success = result?.Success ?? false,
                message = result?.Message ?? "Co loi xay ra."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Loi ket noi: {ex.Message}" });
        }
    }

    // GET /Moderator/ViewAccountDetail/{id}
    [HttpGet("ViewAccountDetail/{id:guid}")]
    public async Task<IActionResult> ViewAccountDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Moderator/moderator-view-account-detail/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AccountDto>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Khong tim thay tai khoan.";
                return RedirectToAction(nameof(ManageAccount));
            }

            return View("~/Views/Moderator/Account/ViewAccountDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Loi ket noi den API.";
            return RedirectToAction(nameof(ManageAccount));
        }
    }

    // POST /Moderator/ViewAccountDetail/{id}
    [HttpPost("ViewAccountDetail/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewAccountDetail(Guid id, ViewAccountDetailRequest model)
    {
        var body = JsonSerializer.Serialize(new { status = model.Status });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/moderator-toggle-account-status/{id}/status")
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
            {
                return RedirectToAction(nameof(ManageAccount), new
                {
                    toastType = "success",
                    toastMessage = model.Status == 1
                        ? "Ban da activate account thanh cong"
                        : "Ban da deactivate account thanh cong"
                });
            }

            TempData["Error"] = result?.Message ?? "Cap nhat that bai.";
            return RedirectToAction(nameof(ManageAccount));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
            return RedirectToAction(nameof(ManageAccount));
        }
    }

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

        var url = $"/api/Moderator/moderator-view-account-list?{string.Join("&", qs)}";
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
