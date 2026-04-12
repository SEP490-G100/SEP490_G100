using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Account;
using WebSite.Models.Admin;

namespace WebSite.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin")]
public class AdminAccountController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AdminAccountController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [HttpGet("ManageModerators")]
    public async Task<IActionResult> ManageModerators(string? search = null, int? status = null, int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status?.ToString() ?? "";

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (status.HasValue) qs.Add($"status={status.Value}");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Admin/admin-view-moderator-account-list?{string.Join("&", qs)}");
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

    [HttpGet("CreateModerator")]
    public IActionResult CreateModerator() =>
        View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", new CreateModeratorRequest());

    [HttpPost("CreateModerator")]
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Admin/admin-create-moderator-account")
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

    [HttpGet("EditModerator/{id:guid}")]
    public async Task<IActionResult> EditModerator(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Admin/admin-view-moderator-account-detail/{id}");
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

    [HttpPost("EditModerator/{id:guid}")]
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

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Admin/admin-update-moderator-account/{id}")
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
            if (result?.Success == true)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(ManageModerators));
            }

            TempData["Error"] = result?.Message ?? "Cập nhật thất bại.";
            return RedirectToAction(nameof(EditModerator), new { id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return RedirectToAction(nameof(EditModerator), new { id });
        }
    }

    [HttpPost("DeleteModerator")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteModerator(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/Admin/admin-delete-moderator-account/{id}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            TempData[result?.Success == true ? "Success" : "Error"] = result?.Message ?? "Đã xoá.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageModerators));
    }

    private void AttachToken(HttpRequestMessage request)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
