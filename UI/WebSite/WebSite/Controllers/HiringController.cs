using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Contract;

namespace WebSite.Controllers;

[Authorize]
[Route("Hiring")]
public class HiringController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public HiringController(IHttpClientFactory httpFactory, IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
    }

    [HttpGet("ViewHiringHistory")]
    public IActionResult ViewHiringHistory()
    {
        if (!User.IsInRole("Parent") && !User.IsInRole("Nanny"))
            return RedirectToAction("Index", "Home");

        return View("~/Views/Hiring/ViewHiringHistory.cshtml");
    }

    [HttpGet("Api/History")]
    public async Task<IActionResult> HistoryApi()
    {
        if (!User.IsInRole("Parent") && !User.IsInRole("Nanny"))
            return StatusCode(403, new { success = false, message = "Ban khong co quyen xem lich su thue." });

        SetBearerToken();
        try
        {
            var response = await _http.GetAsync("/api/contracts");
            if (!response.IsSuccessStatusCode)
                return await Proxy(() => Task.FromResult(response));

            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<ContractListResponseViewModel>>(body, JsonOpts);
            var grouped = result?.Data ?? new ContractListResponseViewModel();

            var merged = grouped.Active
                .Concat(grouped.Pending)
                .Concat(grouped.History)
                .OrderByDescending(item => item.CreatedAt)
                .ToList();

            return Json(new { success = true, data = merged });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message, data = Array.Empty<object>() });
        }
    }

    [HttpGet("{jobPostingId:guid}/Applicants")]
    public async Task<IActionResult> Applicants(Guid jobPostingId)
    {
        SetBearerToken();
        return await Proxy(() => _http.GetAsync($"/api/hiring/{jobPostingId}/applicants"));
    }

    [HttpPost("{jobPostingId:guid}/Applicants/{jobAppId:guid}/Approve")]
    public async Task<IActionResult> Approve(Guid jobPostingId, Guid jobAppId)
    {
        SetBearerToken();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/{jobPostingId}/applicants/{jobAppId}/approve",
            EmptyJson()));
    }

    [HttpGet("{jobPostingId:guid}/Applicants/{jobAppId:guid}/NannyContext")]
    public async Task<IActionResult> NannyContext(Guid jobPostingId, Guid jobAppId)
    {
        SetBearerToken();
        return await Proxy(() => _http.GetAsync($"/api/hiring/{jobPostingId}/applicants/{jobAppId}/nanny-context"));
    }

    [HttpPost("{jobPostingId:guid}/Applicants/{jobAppId:guid}/Hire")]
    public async Task<IActionResult> Hire(Guid jobPostingId, Guid jobAppId)
    {
        try
        {
            SetBearerToken();
            var body = await ReadBodyAsync();
            var response = await _http.PostAsync(
                $"/api/hiring/{jobPostingId}/applicants/{jobAppId}/hire",
                JsonContent(body));

            return await ProxyWithRealtimeHireAsync(response);
        }
        catch (Exception)
        {
            return new JsonResult(new { success = false, message = "Khong the ket noi may chu luc nay. Vui long thu lai." }) { StatusCode = 500 };
        }
    }

    [HttpPost("ContactRequests/{contactRequestId:guid}/Hire")]
    public async Task<IActionResult> HireFromContactRequest(Guid contactRequestId)
    {
        try
        {
            SetBearerToken();
            var body = await ReadBodyAsync();
            var response = await _http.PostAsync(
                $"/api/hiring/contact-requests/{contactRequestId}/hire",
                JsonContent(body));

            return await ProxyWithRealtimeHireAsync(response);
        }
        catch (Exception)
        {
            return new JsonResult(new { success = false, message = "Khong the ket noi may chu luc nay. Vui long thu lai." }) { StatusCode = 500 };
        }
    }

    [HttpGet("Record")]
    public async Task<IActionResult> Record([FromQuery] Guid hiringRecordId)
    {
        SetBearerToken();
        return await Proxy(() => _http.GetAsync($"/api/hiring/records/{hiringRecordId}"));
    }

    [HttpPost("Records/{hiringRecordId:guid}/Respond")]
    public async Task<IActionResult> Respond(Guid hiringRecordId)
    {
        SetBearerToken();
        var body = await ReadBodyAsync();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/records/{hiringRecordId}/respond",
            JsonContent(body)));
    }

    [HttpPost("Records/{hiringRecordId:guid}/Complete")]
    public async Task<IActionResult> Complete(Guid hiringRecordId)
    {
        SetBearerToken();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/records/{hiringRecordId}/complete",
            EmptyJson()));
    }

    [HttpPost("Records/{hiringRecordId:guid}/Cancel")]
    public async Task<IActionResult> Cancel(Guid hiringRecordId)
    {
        SetBearerToken();
        return await Proxy(() => _http.PostAsync(
            $"/api/hiring/records/{hiringRecordId}/cancel",
            EmptyJson()));
    }

    private async Task<IActionResult> ProxyWithRealtimeHireAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var content = string.IsNullOrWhiteSpace(body) ? "{}" : body;

        if (response.IsSuccessStatusCode &&
            TryParseHireRealtimePayload(content, out var parentUserId, out var nannyUserId, out var parentName))
        {
            var resolvedParentName = string.IsNullOrWhiteSpace(parentName)
                ? BuildParentNameFromClaims()
                : parentName.Trim();

            if (nannyUserId != Guid.Empty)
            {
                await _notificationHub.Clients.Group($"user:{nannyUserId}").SendAsync("notification:new", new
                {
                    type = "hiring-confirmed-nanny",
                    title = "Thong bao tu NannyMatch",
                    message = $"Bo me {resolvedParentName} da thue ban.",
                    toastType = "success"
                });
            }

            if (parentUserId != Guid.Empty)
            {
                await _notificationHub.Clients.Group($"user:{parentUserId}").SendAsync("notification:new", new
                {
                    type = "hiring-confirmed-parent",
                    title = "Thông báo từ NannyMatch",
                    message = "Bạn đã xác nhận thuê bảo mẫu thành công.",
                    toastType = "success"
                });
            }
        }

        return new ContentResult
        {
            Content = content,
            ContentType = "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    private static bool TryParseHireRealtimePayload(string json, out Guid parentUserId, out Guid nannyUserId, out string parentName)
    {
        parentUserId = Guid.Empty;
        nannyUserId = Guid.Empty;
        parentName = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var successEl) && successEl.ValueKind == JsonValueKind.False)
                return false;

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return false;

            if (data.TryGetProperty("parentUserId", out var parentUserIdEl) &&
                parentUserIdEl.ValueKind == JsonValueKind.String)
            {
                Guid.TryParse(parentUserIdEl.GetString(), out parentUserId);
            }

            if (data.TryGetProperty("nannyUserId", out var nannyUserIdEl) &&
                nannyUserIdEl.ValueKind == JsonValueKind.String)
            {
                Guid.TryParse(nannyUserIdEl.GetString(), out nannyUserId);
            }

            if (data.TryGetProperty("parentName", out var parentNameEl) &&
                parentNameEl.ValueKind == JsonValueKind.String)
            {
                parentName = parentNameEl.GetString() ?? string.Empty;
            }

            return parentUserId != Guid.Empty || nannyUserId != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }

    private string BuildParentNameFromClaims()
    {
        var firstName = User.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
        var lastName = User.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;
        var fullName = $"{firstName} {lastName}".Trim();

        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName;

        return User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? "Nguoi dung";
    }

    private void SetBearerToken()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> ReadBodyAsync()
    {
        using var reader = new System.IO.StreamReader(Request.Body);
        return await reader.ReadToEndAsync();
    }

    private static StringContent EmptyJson() => new("{}", Encoding.UTF8, "application/json");
    private static StringContent JsonContent(string body) =>
        new(string.IsNullOrWhiteSpace(body) ? "{}" : body, Encoding.UTF8, "application/json");

    private static async Task<IActionResult> Proxy(Func<Task<HttpResponseMessage>> action)
    {
        try
        {
            var response = await action();
            var body = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = string.IsNullOrWhiteSpace(body) ? "{}" : body,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception)
        {
            return new JsonResult(new { success = false, message = "Khong the ket noi may chu luc nay. Vui long thu lai." }) { StatusCode = 500 };
        }
    }
}
