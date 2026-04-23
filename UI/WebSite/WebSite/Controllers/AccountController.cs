using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.Account;
using WebSite.Models.Admin;

namespace WebSite.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AccountController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/Admin/ManageModerators")]
    public async Task<IActionResult> ManageModerators(string? search = null, int? status = null, int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status?.ToString() ?? "";

        var qs = new List<string> { $"page={page}", "pageSize=10" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (status.HasValue) qs.Add($"status={status.Value}");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Account/admin-view-moderator-account-list?{string.Join("&", qs)}");
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
            TempData["Error"] = "Khong the tai danh sach dieu hanh vien.";
            return View("~/Views/Admin/ModeratorAccount/ManageModerators.cshtml", new AccountListResponse());
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/Admin/CreateModerator")]
    public IActionResult CreateModerator() =>
        View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", new CreateModeratorRequest());

    [Authorize(Roles = "Admin")]
    [HttpPost("/Admin/CreateModerator")]
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Account/admin-create-moderator-account")
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
                        toastMessage = result.Message ?? "Tao tai khoan dieu hanh vien thanh cong."
                    });
            }

            TempData["Error"] = result?.Message ?? "Tao tai khoan dieu hanh vien that bai.";
            return View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
            return View("~/Views/Admin/ModeratorAccount/CreateModerator.cshtml", model);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/Admin/EditModerator/{id:guid}")]
    public async Task<IActionResult> EditModerator(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Account/admin-view-moderator-account-detail/{id}");
        AttachToken(request);
        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AccountDto>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Khong tim thay dieu hanh vien.";
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

    [Authorize(Roles = "Admin")]
    [HttpPost("/Admin/EditModerator/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditModerator(Guid id, [FromForm] int status)
    {
        var body = JsonSerializer.Serialize(new
        {
            status
        });

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Account/admin-update-moderator-account/{id}")
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
                        toastMessage = "Da thay doi thong tin tai khoan thanh cong"
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

    [Authorize(Roles = "Admin")]
    [HttpPost("/Admin/ToggleModeratorStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleModeratorStatus(
        [FromForm] Guid id,
        [FromForm] int status,
        [FromForm] string? returnUrl = null)
    {
        var body = JsonSerializer.Serialize(new { status });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Account/admin-toggle-moderator-account/{id}")
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
                var toastMessage = status == 1
                    ? "Da kich hoat tai khoan thanh cong"
                    : "Da vo hieu hoa tai khoan thanh cong";
                var toastType = status == 1 ? "success" : "warning";
                return RedirectToReturnUrlOrList(returnUrl, toastType, toastMessage);
            }

            TempData["Error"] = result?.Message ?? "Khong the cap nhat trang thai dieu hanh vien.";
            return RedirectToReturnUrlOrList(returnUrl);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
            return RedirectToReturnUrlOrList(returnUrl);
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ManageAccount")]
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

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/ToggleStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id, int newStatus)
    {
        var body = JsonSerializer.Serialize(new { status = newStatus });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Account/moderator-toggle-account-status/{id}/status")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AttachToken(request);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            return Json(new
            {
                success = result?.Success ?? false,
                message = result?.Message ?? "Da xay ra loi."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Loi ket noi: {ex.Message}" });
        }
    }

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ViewAccountDetail/{id:guid}")]
    public async Task<IActionResult> ViewAccountDetail(Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Account/moderator-view-account-detail/{id}");
        AttachToken(request);

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

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/ViewAccountDetail/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewAccountDetail(Guid id, ViewAccountDetailRequest model)
    {
        var body = JsonSerializer.Serialize(new { status = model.Status });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Account/moderator-toggle-account-status/{id}/status")
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
                return RedirectToAction(nameof(ManageAccount), new
                {
                    toastType = "success",
                    toastMessage = model.Status == 1
                        ? "Ban da kich hoat tai khoan thanh cong"
                        : "Ban da vo hieu hoa tai khoan thanh cong"
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
        int pageSize = 10)
    {
        var qs = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(role)) qs.Add($"role={Uri.EscapeDataString(role)}");
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");

        var url = $"/api/Account/moderator-view-account-list?{string.Join("&", qs)}";
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
