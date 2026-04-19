using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Verification;

namespace WebSite.Controllers;

[Authorize(Roles = "Moderator")]
[Route("Moderator")]
public class ModeratorVerificationController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModeratorVerificationController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
    }

    // GET /Moderator/ManageNannyVerification
    [HttpGet("ManageNannyVerification")]
    public async Task<IActionResult> ManageNannyVerification(string? search = null, int? status = null, int? requestType = null, int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.RequestType = requestType;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (requestType.HasValue) qs.Add($"requestType={requestType.Value}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");

        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/Moderator/moderator-view-verification-list?{string.Join("&", qs)}");

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<VerificationRequestListResponse>>(json, JsonOpts);

            return View(
                "~/Views/Moderator/NannyVerification/ManageNannyVerification.cshtml",
                result?.Data ?? new VerificationRequestListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách xác minh.";
            return View(
                "~/Views/Moderator/NannyVerification/ManageNannyVerification.cshtml",
                new VerificationRequestListResponse());
        }
    }

    // GET /Moderator/ViewNannyVerificationDetail/{id}
    [HttpGet("ViewNannyVerificationDetail/{id:guid}")]
    public async Task<IActionResult> ViewNannyVerificationDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Moderator/moderator-view-verification-detail/{id}");

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<VerificationRequestDetailDto>>(json, JsonOpts);

            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu xác minh.";
                return RedirectToAction(nameof(ManageNannyVerification));
            }

            return View("~/Views/Moderator/NannyVerification/ViewNannyVerificationDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
    }

    // POST /Moderator/ReviewVerification/{id}
    [HttpPost("ReviewVerification/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewVerification(
        Guid id,
        int action,
        string? rejectionReason,
        Guid? nannyUserId = null)
    {
        var body = JsonSerializer.Serialize(new
        {
            action,
            rejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason.Trim()
        });

        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/review-verification/{id}")
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
            {
                if (nannyUserId.HasValue && nannyUserId.Value != Guid.Empty)
                {
                    await _notificationHub.Clients.Group($"user:{nannyUserId.Value}").SendAsync("notification:new", new
                    {
                        type = action == 2 ? "verification-approved" : "verification-rejected",
                        title = action == 2 ? "Yêu cầu xác minh đã được chấp thuận" : "Yêu cầu xác minh đã bị từ chối",
                        message = action == 2
                            ? "Yêu cầu xác minh của bạn đã được chấp thuận."
                            : "Yêu cầu xác minh của bạn đã bị từ chối.",
                        toastType = action == 2 ? "success" : "warning"
                    });
                }

                var listUrl = Url.Action(nameof(ManageNannyVerification), "ModeratorVerification")
                              ?? "/Moderator/ManageNannyVerification";
                var toastMessage = Uri.EscapeDataString("Bạn đã xử lý yêu cầu xác minh thành công");
                return Redirect($"{listUrl}?toastType=success&toastMessage={toastMessage}");
            }

            TempData["Error"] = result?.Message ?? "Xử lý thất bại.";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
    }
}
