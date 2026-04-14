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
            TempData["Error"] = "Khong the tai danh sach Moderator.";
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
                TempData["Error"] = $"API tra ve rong (HTTP {(int)response.StatusCode}).";
                return View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", model);
            }

            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                return RedirectToAction(
                    nameof(ManageModerators),
                    new
                    {
                        toastType = "success",
                        toastMessage = result.Message ?? "Tao Moderator thanh cong."
                    });
            }

            TempData["Error"] = result?.Message ?? "Tao Moderator that bai.";
            return View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
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
                TempData["Error"] = "Khong tim thay Moderator.";
                return RedirectToAction(nameof(ManageModerators));
            }

            return View("~/Views/Admin/ModeratorAccount/EditModerator.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Loi ket noi.";
            return RedirectToAction(nameof(ManageModerators));
        }
    }

    [HttpPost("EditModerator/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditModerator(Guid id, [FromForm] int status)
    {
        var body = JsonSerializer.Serialize(new
        {
            status
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
                TempData["Error"] = $"API tra ve rong (HTTP {(int)response.StatusCode}).";
                return RedirectToAction(nameof(EditModerator), new { id });
            }

            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                return RedirectToAction(
                    nameof(ManageModerators),
                    new
                    {
                        toastType = "success",
                        toastMessage = result.Message ?? "Cap nhat Moderator thanh cong."
                    });
            }

            TempData["Error"] = result?.Message ?? "Cap nhat that bai.";
            return RedirectToAction(nameof(EditModerator), new { id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
            return RedirectToAction(nameof(EditModerator), new { id });
        }
    }

    [HttpPost("ToggleModeratorStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleModeratorStatus(
        [FromForm] Guid id,
        [FromForm] int status,
        [FromForm] string? returnUrl = null)
    {
        var body = JsonSerializer.Serialize(new { status });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Admin/admin-toggle-moderator-account/{id}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                return RedirectToReturnUrlOrList(returnUrl, "success", "Đã chỉnh sửa trạng thái tài khoản thành công");
            }

            TempData["Error"] = result?.Message ?? "Khong the cap nhat trang thai Moderator.";
            return RedirectToReturnUrlOrList(returnUrl);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
            return RedirectToReturnUrlOrList(returnUrl);
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
            TempData[result?.Success == true ? "Success" : "Error"] = result?.Message ?? "Da xoa.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageModerators));
    }

    private void AttachToken(HttpRequestMessage request)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

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
                nameof(ManageModerators),
                new { toastType = toastType ?? "info", toastMessage });
        }

        return RedirectToAction(nameof(ManageModerators));
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
