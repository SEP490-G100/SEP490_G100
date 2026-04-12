using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Enums;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Blog;
using WebSite.Models.Moderator;
using WebSite.Models.Search;
using WebSite.Services;
using System.Text.Json.Serialization;

namespace WebSite.Controllers
{
    [Authorize(Roles = "Moderator")]
    [Route("Moderator")]

    public class ModeratorJobController : Controller
    {
        private readonly HttpClient _http;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        public ModeratorJobController(
            IHttpClientFactory httpFactory,
            IHubContext<NotificationHub> notificationHub,
            IAzureBlobStorageService blobStorageService)
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
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var url = $"api/Moderator/moderator-view-job-list?page={page}&pageSize=10";
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

            TempData["Error"] = "Could not fetch job postings.";
            return View("~/Views/Moderator/JobPosting/ManageJobPosting.cshtml", new JobPostingListResponse());
        }

        [HttpGet("ViewJobPostingDetail/{id:guid}")]
        public async Task<IActionResult> ViewJobPostingDetail(Guid id)
        {
            var token = HttpContext.Session.GetString("AccessToken");
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var response = await _http.GetAsync($"/api/Moderator/moderator-view-job-detail/{id}");
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResult<JobPostingDetailResponse>>(json, JsonOpts);

                if (result?.Success != true || result.Data == null)
                {
                    TempData["Error"] = result?.Message ?? "Could not find the job posting.";
                    return RedirectToAction(nameof(ManageJobPosting));
                }

                return View("~/Views/Moderator/JobPosting/ViewJobPostingDetail.cshtml", result.Data);
            }
            catch
            {
                TempData["Error"] = "Could not load the job posting detail.";
                return RedirectToAction(nameof(ManageJobPosting));
            }
        }

        [HttpPost("ReviewJobPosting")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewJobPosting(Guid id, int action, string? note, Guid? parentUserId = null, bool returnToDetail = false)
        {
            var token = HttpContext.Session.GetString("AccessToken");
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var body = new { action, note };
            var response = await _http.PatchAsJsonAsync($"api/Moderator/moderator-review-job/{id}", body);

            if (response.IsSuccessStatusCode)
            {
                if (parentUserId.HasValue && parentUserId.Value != Guid.Empty)
                {
                    await _notificationHub.Clients.Group($"user:{parentUserId.Value}").SendAsync("notification:new", new
                    {
                        type = action == 2 ? "job-posting-approved" : "job-posting-rejected",
                        title = action == 2 ? "Bai dang da duoc duyet" : "Bai dang da bi tu choi",
                        message = action == 2
                            ? "Bai dang cua ban da duoc moderator duyet."
                            : "Bai dang cua ban da bi moderator tu choi.",
                        toastType = action == 2 ? "success" : "warning"
                    });
                }

                return RedirectToAction(nameof(ManageJobPosting), new
                {
                    toastType = action == 2 ? "success" : "warning",
                    toastMessage = action == 2
                        ? "Job posting approved successfully."
                        : "Job posting rejected successfully."
                });
            }
            else
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                TempData["Error"] = "Review failed: " + errorJson;
            }

            return RedirectToAction(nameof(ManageJobPosting));
        }
    }
 


}
