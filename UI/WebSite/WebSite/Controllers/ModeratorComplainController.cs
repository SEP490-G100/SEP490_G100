using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Moderator;
using WebSite.Models.Profile;
using System.Text.Json.Serialization;


namespace WebSite.Controllers;

[Authorize(Roles = "Moderator")]
[Route("Moderator")]
public class ModeratorComplainController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModeratorComplainController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ManageComplaint
    // ──────────────────────────────────────────────
    [HttpGet("ManageComplaint")]
    public async Task<IActionResult> ManageComplaint(
        string? search = null,
        int? status = null,
        string? entityType = null,
        int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status?.ToString() ?? "";
        ViewBag.EntityType = entityType ?? "";

        var qs = new List<string> { $"page={page}", "pageSize=10" };
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(entityType)) qs.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");

        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Moderator/reports?{string.Join("&", qs)}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<ModeratorComplaintListResponse>>(json, JsonOpts);
            return View("~/Views/Moderator/Complaint/ManageComplaint.cshtml", result?.Data ?? new ModeratorComplaintListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách khiếu nại.";
            return View("~/Views/Moderator/Complaint/ManageComplaint.cshtml", new ModeratorComplaintListResponse());
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ViewComplaintDetail/{id}
    // ──────────────────────────────────────────────
    [HttpGet("ViewComplaintDetail/{id:guid}")]
    public async Task<IActionResult> ViewComplaintDetail(Guid id)
    {
        var detail = await FetchComplaintDetailAsync(id);
        if (detail == null)
        {
            TempData["Error"] = "Không tìm thấy khiếu nại.";
            return RedirectToAction(nameof(ManageComplaint));
        }

        var pageModel = new ModeratorComplaintDetailPageModel
        {
            Detail = detail,
            Form = new ModeratorResolveComplaintRequest
            {
                Resolution = detail.Resolution ?? string.Empty,
                ActionTaken = detail.ActionTaken ?? string.Empty,
                OffenderNotificationMessage = string.Empty
            }
        };
        return View("~/Views/Moderator/Complaint/ViewComplaintDetail.cshtml", pageModel);
    }

    // ──────────────────────────────────────────────
    // POST /Moderator/ViewComplaintDetail/{id}
    // ──────────────────────────────────────────────
    [HttpPost("ViewComplaintDetail/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewComplaintDetail(Guid id, [Bind(Prefix = "Form")] ModeratorResolveComplaintRequest form)
    {
        var detail = await FetchComplaintDetailAsync(id);
        if (detail == null)
        {
            TempData["Error"] = "Không tìm thấy khiếu nại.";
            return RedirectToAction(nameof(ManageComplaint));
        }

        if (detail.Status == 1)
        {
            TempData["Error"] = "Khiếu nại đã giải quyết. Biện pháp không thể chỉnh sửa.";
            var lockedModel = new ModeratorComplaintDetailPageModel
            {
                Detail = detail,
                Form = new ModeratorResolveComplaintRequest
                {
                    Resolution = detail.Resolution ?? string.Empty,
                    ActionTaken = detail.ActionTaken ?? string.Empty,
                    OffenderNotificationMessage = string.Empty
                }
            };
            return View("~/Views/Moderator/Complaint/ViewComplaintDetail.cshtml", lockedModel);
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = new ModeratorComplaintDetailPageModel
            {
                Detail = detail,
                Form = form
            };
            return View("~/Views/Moderator/Complaint/ViewComplaintDetail.cshtml", invalidModel);
        }

        var token = HttpContext.Session.GetString("AccessToken");
        var body = JsonSerializer.Serialize(new
        {
            resolution = form.Resolution?.Trim(),
            actionTaken = form.ActionTaken?.Trim(),
            offenderNotificationMessage = string.IsNullOrWhiteSpace(form.OffenderNotificationMessage)
                ? null
                : form.OffenderNotificationMessage.Trim()
        });

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/reports/{id}/resolve")
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
                await _notificationHub.Clients.Group($"user:{detail.ReporterUserId}").SendAsync("notification:new", new
                {
                    type = "complaint-resolved",
                    title = "Thông báo từ điều hành viên",
                    message = "Chúng tôi đã xử lý yêu cầu khiếu nại của bạn",
                    toastType = "success"
                });

                if (!string.IsNullOrWhiteSpace(form.OffenderNotificationMessage)
                    && detail.OffenderUserId.HasValue
                    && detail.OffenderUserId.Value != Guid.Empty)
                {
                    await _notificationHub.Clients.Group($"user:{detail.OffenderUserId.Value}").SendAsync("notification:new", new
                    {
                        type = "complaint-reviewed",
                        title = "Thông báo từ điều hành viên",
                        message = form.OffenderNotificationMessage.Trim(),
                        toastType = "info"
                    });
                }

                var listUrl = Url.Action(nameof(ManageComplaint), "ModeratorComplain")
                              ?? "/Moderator/ManageComplaint";
                var toastMessage = Uri.EscapeDataString("Bạn đã xử lý yêu cầu khiếu nại thành công");
                return Redirect($"{listUrl}?toastType=success&toastMessage={toastMessage}");
            }

            TempData["Error"] = result?.Message ?? "Không thể giải quyết khiếu nại.";
            var failedModel = new ModeratorComplaintDetailPageModel
            {
                Detail = detail,
                Form = form
            };
            return View("~/Views/Moderator/Complaint/ViewComplaintDetail.cshtml", failedModel);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            var failedModel = new ModeratorComplaintDetailPageModel
            {
                Detail = detail,
                Form = form
            };
            return View("~/Views/Moderator/Complaint/ViewComplaintDetail.cshtml", failedModel);
        }
    }

    [HttpGet("ViewComplainedJobPostingDetail")]
    public async Task<IActionResult> ViewComplainedJobPostingDetail(Guid jobPostingId, Guid? complaintId = null)
    {
        if (jobPostingId == Guid.Empty)
            return RedirectToAction(nameof(ManageComplaint), new { toastType = "error", toastMessage = "Không tìm thấy bài đăng." });

        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.GetAsync($"/api/Moderator/moderator-view-job-detail/{jobPostingId}");
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<WebSite.Models.Search.JobPostingDetailResponse>>(json, JsonOpts);

            if (result?.Success != true || result.Data == null)
            {
                return RedirectToAction(nameof(ManageComplaint), new
                {
                    toastType = "error",
                    toastMessage = result?.Message ?? "Không thể tải chi tiết bài đăng bị phàn nàn."
                });
            }

            var model = new ModeratorComplainedJobPostingDetailPageModel
            {
                ComplaintId = complaintId,
                JobPosting = result.Data
            };

            return View("~/Views/Moderator/Complaint/ViewComplainedJobPostingDetail.cshtml", model);
        }
        catch
        {
            return RedirectToAction(nameof(ManageComplaint), new
            {
                toastType = "error",
                toastMessage = "Không thể tải chi tiết bài đăng bị phàn nàn."
            });
        }
    }

    [HttpPost("DeactivateComplainedJobPosting")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateComplainedJobPosting(Guid jobPostingId, Guid? complaintId = null)
    {
        if (jobPostingId == Guid.Empty)
            return RedirectToAction(nameof(ManageComplaint), new { toastType = "error", toastMessage = "Không tìm thấy bài đăng." });

        var token = HttpContext.Session.GetString("AccessToken");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.PatchAsJsonAsync($"/api/Moderator/moderator-deactivate-job-posting/{jobPostingId}", new { });
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                if (complaintId.HasValue && complaintId.Value != Guid.Empty)
                {
                    return RedirectToAction(nameof(ViewComplaintDetail), new
                    {
                        id = complaintId.Value,
                        toastType = "success",
                        toastMessage = "Đã vô hiệu hóa bài đăng"
                    });
                }

                return RedirectToAction(nameof(ManageComplaint), new
                {
                    toastType = "success",
                    toastMessage = "Đã vô hiệu hóa bài đăng"
                });
            }

            return RedirectToAction(nameof(ViewComplainedJobPostingDetail), new
            {
                jobPostingId,
                complaintId,
                toastType = "error",
                toastMessage = result?.Message ?? "Không thể vô hiệu hóa bài đăng."
            });
        }
        catch
        {
            return RedirectToAction(nameof(ViewComplainedJobPostingDetail), new
            {
                jobPostingId,
                complaintId,
                toastType = "error",
                toastMessage = "Không thể vô hiệu hóa bài đăng."
            });
        }
    }

    [HttpGet("ViewComplainedProfileDetail")]
    public async Task<IActionResult> ViewComplainedProfileDetail(Guid userId, Guid? complaintId = null)
    {
        if (userId == Guid.Empty)
            return RedirectToAction(nameof(ManageComplaint), new { toastType = "error", toastMessage = "Không tìm thấy hồ sơ." });

        try
        {
            var token = HttpContext.Session.GetString("AccessToken");
            if (string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction("đăng nhập", "Auth", new
                {
                    toastType = "warning",
                    toastMessage = "Phiên đăng nhập đã hết hạn."
                });
            }

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.GetAsync($"/api/profile/public/{userId}");

            if (!response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(ManageComplaint), new
                {
                    toastType = "error",
                    toastMessage = "Không thể tải hồ sơ bị phàn nàn."
                });
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<PersonalProfileViewModel>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                return RedirectToAction(nameof(ManageComplaint), new
                {
                    toastType = "error",
                    toastMessage = result?.Message ?? "Không thể tải hồ sơ bị phàn nàn."
                });
            }

            result.Data.IsReadOnlyView = true;

            var model = new ModeratorComplainedProfileDetailPageModel
            {
                ComplaintId = complaintId,
                Profile = result.Data
            };

            return View("~/Views/Moderator/Complaint/ViewComplainedProfileDetail.cshtml", model);
        }
        catch
        {
            return RedirectToAction(nameof(ManageComplaint), new
            {
                toastType = "error",
                toastMessage = "Không thể tải hồ sơ bị phàn nàn."
            });
        }
    }

    // ──────────────────────────────────────────────
    // POST /Moderator/ToggleComplaintStatus
    // ──────────────────────────────────────────────
    [HttpPost("ToggleComplaintStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleComplaintStatus(Guid id, bool isActive)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var body = JsonSerializer.Serialize(new { isActive });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/reports/{id}/status")
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
            var isSuccess = result?.Success ?? false;

            return Json(new
            {
                success = isSuccess,
                message = isSuccess
                    ? (isActive
                        ? "Bạn đã kích hoạt khiếu nại thành công"
                        : "Bạn đã vô hiệu hóa khiếu nại thành công")
                    : (result?.Message ?? "Thao tác thất bại.")
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Lỗi kết nối: {ex.Message}" });
        }
    }


    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private async Task<ModeratorComplaintDetailDto?> FetchComplaintDetailAsync(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Moderator/reports/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<ModeratorComplaintDetailDto>>(json, JsonOpts);
            return result?.Success == true ? result.Data : null;
        }
        catch
        {
            return null;
        }
    }
    

}
