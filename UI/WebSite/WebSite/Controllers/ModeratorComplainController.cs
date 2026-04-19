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
                    title = "Thong bao tu Moderator",
                    message = "Chúng tôi đã xử lí yêu cầu phàn nàn của bạn",
                    toastType = "success"
                });

                if (!string.IsNullOrWhiteSpace(form.OffenderNotificationMessage)
                    && detail.OffenderUserId.HasValue
                    && detail.OffenderUserId.Value != Guid.Empty)
                {
                    await _notificationHub.Clients.Group($"user:{detail.OffenderUserId.Value}").SendAsync("notification:new", new
                    {
                        type = "complaint-reviewed",
                        title = "Thong bao tu Moderator",
                        message = form.OffenderNotificationMessage.Trim(),
                        toastType = "info"
                    });
                }

                var listUrl = Url.Action(nameof(ManageComplaint), "ModeratorComplain")
                              ?? "/Moderator/ManageComplaint";
                var toastMessage = Uri.EscapeDataString("Bạn đã xử lí yêu cầu phàn nàn thành công");
                return Redirect($"{listUrl}?toastType=success&toastMessage={toastMessage}");
            }

            TempData["Error"] = result?.Message ?? "Failed to resolve complaint.";
            var failedModel = new ModeratorComplaintDetailPageModel
            {
                Detail = detail,
                Form = form
            };
            return View("~/Views/Moderator/Complaint/ViewComplaintDetail.cshtml", failedModel);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Connection error: {ex.Message}";
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
            return RedirectToAction(nameof(ManageComplaint), new { toastType = "error", toastMessage = "Khong tim thay bai dang." });

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
                    toastMessage = result?.Message ?? "Khong the tai chi tiet bai dang bi phan nan."
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
                toastMessage = "Khong the tai chi tiet bai dang bi phan nan."
            });
        }
    }

    [HttpPost("DeactivateComplainedJobPosting")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateComplainedJobPosting(Guid jobPostingId, Guid? complaintId = null)
    {
        if (jobPostingId == Guid.Empty)
            return RedirectToAction(nameof(ManageComplaint), new { toastType = "error", toastMessage = "Khong tim thay bai dang." });

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
                        toastMessage = "Da vo hieu hoa bai dang"
                    });
                }

                return RedirectToAction(nameof(ManageComplaint), new
                {
                    toastType = "success",
                    toastMessage = "Da vo hieu hoa bai dang"
                });
            }

            return RedirectToAction(nameof(ViewComplainedJobPostingDetail), new
            {
                jobPostingId,
                complaintId,
                toastType = "error",
                toastMessage = result?.Message ?? "Khong the vo hieu hoa bai dang."
            });
        }
        catch
        {
            return RedirectToAction(nameof(ViewComplainedJobPostingDetail), new
            {
                jobPostingId,
                complaintId,
                toastType = "error",
                toastMessage = "Khong the vo hieu hoa bai dang."
            });
        }
    }

    [HttpGet("ViewComplainedProfileDetail")]
    public async Task<IActionResult> ViewComplainedProfileDetail(Guid userId, Guid? complaintId = null)
    {
        if (userId == Guid.Empty)
            return RedirectToAction(nameof(ManageComplaint), new { toastType = "error", toastMessage = "Khong tim thay profile." });

        try
        {
            var token = HttpContext.Session.GetString("AccessToken");
            if (string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction("Login", "Auth", new
                {
                    toastType = "warning",
                    toastMessage = "Phien dang nhap da het han."
                });
            }

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.GetAsync($"/api/profile/public/{userId}");

            if (!response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(ManageComplaint), new
                {
                    toastType = "error",
                    toastMessage = "Khong the tai profile bi phan nan."
                });
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<PersonalProfileViewModel>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                return RedirectToAction(nameof(ManageComplaint), new
                {
                    toastType = "error",
                    toastMessage = result?.Message ?? "Khong the tai profile bi phan nan."
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
                toastMessage = "Khong the tai profile bi phan nan."
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
                        ? "Bạn đã kích hoạt phàn nàn thành công"
                        : "Bạn đã vô hiệu hóa phàn nàn thành công")
                    : (result?.Message ?? "Operation failed.")
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Connection error: {ex.Message}" });
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
