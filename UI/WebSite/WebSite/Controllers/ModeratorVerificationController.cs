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
    public async Task<IActionResult> ManageNannyVerification(string? search = null, int? status = null, int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (status.HasValue) qs.Add($"status={status.Value}");
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
            TempData["Error"] = "Khong the tai danh sach xac minh.";
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
                TempData["Error"] = "Khong tim thay yeu cau xac minh.";
                return RedirectToAction(nameof(ManageNannyVerification));
            }

            return View("~/Views/Moderator/NannyVerification/ViewNannyVerificationDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Loi ket noi den API.";
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
                        title = action == 2 ? "Yeu cau xac minh da duoc chap thuan" : "Yeu cau xac minh da bi tu choi",
                        message = action == 2
                            ? "Yeu cau xac minh cua ban da duoc chap thuan."
                            : "Yeu cau xac minh cua ban da bi tu choi.",
                        toastType = action == 2 ? "success" : "warning"
                    });
                }

                return RedirectToAction(nameof(ManageNannyVerification), new
                {
                    toastType = action == 2 ? "success" : "warning",
                    toastMessage = action == 2 ? "Da duyet ho so thanh cong." : "Da tu choi ho so."
                });
            }

            TempData["Error"] = result?.Message ?? "Xu ly that bai.";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
            return RedirectToAction(nameof(ManageNannyVerification));
        }
    }
}
