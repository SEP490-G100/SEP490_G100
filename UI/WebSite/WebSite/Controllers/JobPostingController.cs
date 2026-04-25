using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Search;

namespace WebSite.Controllers;

[Authorize(Roles = "Moderator")]
[Route("Moderator")]
public class JobPostingController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public JobPostingController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
    }

    [HttpGet("ManageJobPosting")]
    public async Task<IActionResult> ManageJobPosting(
        int? status = null,
        int? moderationStatus = null,
        string? search = null,
        int page = 1)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"api/job-postings/moderator-view-job-list?page={page}&pageSize=10";
        if (status.HasValue) url += $"&status={status}";
        if (moderationStatus.HasValue) url += $"&moderationStatus={moderationStatus}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";

        var response = await _http.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<JobPostingListResponse>>(json, JsonOpts);
            ViewBag.Search = search;
            ViewBag.Status = status?.ToString();
            ViewBag.ModerationStatus = moderationStatus?.ToString();

            return View("~/Views/Moderator/JobPosting/ManageJobPosting.cshtml", result?.Data);
        }

        TempData["Error"] = "Khong the tai danh sach tin dang.";
        return View("~/Views/Moderator/JobPosting/ManageJobPosting.cshtml", new JobPostingListResponse());
    }

    [HttpGet("ViewJobPostingDetail/{id:guid}")]
    public async Task<IActionResult> ViewJobPostingDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.GetAsync($"/api/job-postings/moderator-view-job-detail/{id}");
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<JobPostingDetailResponse>>(json, JsonOpts);

            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = result?.Message ?? "Khong tim thay tin dang.";
                return RedirectToAction(nameof(ManageJobPosting));
            }

            return View("~/Views/Moderator/JobPosting/ViewJobPostingDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Khong the tai chi tiet tin dang.";
            return RedirectToAction(nameof(ManageJobPosting));
        }
    }

    [HttpPost("ReviewJobPosting")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewJobPosting(Guid id, int action, string? note, Guid? parentUserId = null, bool returnToDetail = false)
    {
        if (action is not 1 and not 2)
        {
            TempData["Error"] = "Hành động kiểm duyệt không hợp lệ.";
            return RedirectToAction(nameof(ViewJobPostingDetail), new { id });
        }

        if (action == 1 && string.IsNullOrWhiteSpace(note))
        {
            TempData["Error"] = "Bạn phải nhập lý do từ chối trước khi từ chối bài đăng.";
            return RedirectToAction(nameof(ViewJobPostingDetail), new { id });
        }

        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new { action, note = normalizedNote };
        var response = await _http.PatchAsJsonAsync($"api/job-postings/moderator-review-job/{id}", body);

        if (response.IsSuccessStatusCode)
        {
            if (parentUserId.HasValue && parentUserId.Value != Guid.Empty)
            {
                await _notificationHub.Clients.Group($"user:{parentUserId.Value}").SendAsync("notification:new", new
                {
                    type = action == 2 ? "job-posting-approved" : "job-posting-rejected",
                    title = action == 2 ? "Bài đăng đã được duyệt" : "Bài đăng đã bị từ chối",
                    message = action == 2
                        ? "Bài đăng của bạn đã được điều hành viên duyệt."
                        : "Bài đăng của bạn đã bị điều hành viên từ chối.",
                    toastType = action == 2 ? "success" : "warning"
                });
            }

            var listUrl = Url.Action(nameof(ManageJobPosting), "JobPosting")
                          ?? "/Moderator/ManageJobPosting";
            var toastMessage = Uri.EscapeDataString("Bạn đã xử lý yêu cầu kiểm duyệt bài đăng thành công.");
            return Redirect($"{listUrl}?toastType=success&toastMessage={toastMessage}");
        }

        var errorJson = await response.Content.ReadAsStringAsync();
        TempData["Error"] = "Kiểm duyệt thất bại: " + errorJson;
        return RedirectToAction(nameof(ViewJobPostingDetail), new { id });
    }
}
