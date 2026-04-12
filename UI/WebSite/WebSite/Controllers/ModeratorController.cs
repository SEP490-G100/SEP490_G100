using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Enums;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.FAQ;
using WebSite.Models.BlogCategory;
using WebSite.Models.Blog;
using WebSite.Models.Moderator;
using WebSite.Models.Profile;
using WebSite.Models.Search;
using WebSite.Services;
using System.Text.Json.Serialization;


namespace WebSite.Controllers;

[Authorize(Roles = "Moderator")]
public class ModeratorController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IAzureBlobStorageService _blobStorageService;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModeratorController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub,
        IAzureBlobStorageService blobStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
        _blobStorageService = blobStorageService;
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ManageComplaint
    // ──────────────────────────────────────────────
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
            TempData["Error"] = "Cannot load complaint list.";
            return View("~/Views/Moderator/Complaint/ManageComplaint.cshtml", new ModeratorComplaintListResponse());
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ViewComplaintDetail/{id}
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ViewComplaintDetail(Guid id)
    {
        var detail = await FetchComplaintDetailAsync(id);
        if (detail == null)
        {
            TempData["Error"] = "Complaint not found.";
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
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewComplaintDetail(Guid id, [Bind(Prefix = "Form")] ModeratorResolveComplaintRequest form)
    {
        var detail = await FetchComplaintDetailAsync(id);
        if (detail == null)
        {
            TempData["Error"] = "Complaint not found.";
            return RedirectToAction(nameof(ManageComplaint));
        }

        if (detail.Status == 1)
        {
            TempData["Error"] = "Complaint already completed. Resolution cannot be edited.";
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

                return RedirectToAction(nameof(ManageComplaint), new
                {
                    toastType = "success",
                    toastMessage = "Complaint resolved successfully."
                });
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

    [HttpGet]
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
            var result = JsonSerializer.Deserialize<ApiResult<JobPostingDetailResponse>>(json, JsonOpts);

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

    [HttpPost]
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
                TempData["Success"] = "Da vo hieu hoa bai dang";

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

    [HttpGet]
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
    [HttpPost]
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
            return Json(new
            {
                success = result?.Success ?? false,
                message = result?.Message ?? "Operation failed."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Connection error: {ex.Message}" });
        }
    }


    // ──────────────────────────────────────────────
    // GET /Moderator/ManageFAQ
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ManageFAQ(
        string? search = null,
        bool? isActive = null,
        string? category = null,
        int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.IsActive = isActive?.ToString() ?? "";
        ViewBag.Category = category ?? "";

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (isActive.HasValue) qs.Add($"isActive={isActive.Value.ToString().ToLower()}");
        if (!string.IsNullOrWhiteSpace(category)) qs.Add($"category={Uri.EscapeDataString(category)}");

        var token = HttpContext.Session.GetString("AccessToken");

        // Fetch FAQ list
        var listReq = new HttpRequestMessage(HttpMethod.Get, $"/api/Faq?{string.Join("&", qs)}");
        if (!string.IsNullOrEmpty(token))
            listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Fetch categories for filter dropdown
        var catReq = new HttpRequestMessage(HttpMethod.Get, "/api/Faq/categories");
        if (!string.IsNullOrEmpty(token))
            catReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var listResp = await _http.SendAsync(listReq);
            var listJson = await listResp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<FaqListResponse>>(listJson, JsonOpts);

            try
            {
                var catResp = await _http.SendAsync(catReq);
                var catJson = await catResp.Content.ReadAsStringAsync();
                var catResult = JsonSerializer.Deserialize<ApiResult<List<string>>>(catJson, JsonOpts);
                ViewBag.Categories = catResult?.Data ?? new List<string>();
            }
            catch { ViewBag.Categories = new List<string>(); }

            return View("~/Views/Moderator/FAQ/ManageFAQ.cshtml", result?.Data ?? new FaqListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách FAQ.";
            ViewBag.Categories = new List<string>();
            return View("~/Views/Moderator/FAQ/ManageFAQ.cshtml", new FaqListResponse());
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/CreateFAQ
    // ──────────────────────────────────────────────
    public IActionResult CreateFAQ() => View("~/Views/Moderator/FAQ/CreateFAQ.cshtml", new CreateFaqRequest());

    // ──────────────────────────────────────────────
    // POST /Moderator/CreateFAQ
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFAQ(CreateFaqRequest model)
    {
        var body = JsonSerializer.Serialize(new
        {
            question = model.Question,
            answer = model.Answer,
            category = model.Category,
            isActive = model.IsActive
            // SortOrder auto-assigned by backend
        });
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Faq")
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
                return RedirectToAction(nameof(ManageFAQ), new
                {
                    toastType = "success",
                    toastMessage = "Đã tạo FAQ thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Tạo FAQ thất bại.";
            return View("~/Views/Moderator/FAQ/CreateFAQ.cshtml", model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return View("~/Views/Moderator/FAQ/CreateFAQ.cshtml", model);
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ViewFAQDetail/{id}
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ViewFAQDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Faq/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<FaqDto>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy FAQ.";
                return RedirectToAction(nameof(ManageFAQ));
            }
            return View("~/Views/Moderator/FAQ/ViewFAQDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageFAQ));
        }
    }

    // ──────────────────────────────────────────────
    // POST /Moderator/ViewFAQDetail/{id}  (update)
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewFAQDetail(Guid id, UpdateFaqRequest model)
    {
        var body = JsonSerializer.Serialize(new
        {
            question = model.Question,
            answer = model.Answer,
            isActive = model.IsActive
        });
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/Faq/{id}")
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
                return RedirectToAction(nameof(ManageFAQ), new
                {
                    toastType = "success",
                    toastMessage = "Đã chỉnh sửa FAQ thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Cập nhật FAQ thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ViewFAQDetail), new { id });
    }



    // ──────────────────────────────────────────────
    // POST /Moderator/ToggleFaqStatus  — AJAX toggle
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFaqStatus(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Faq/{id}/toggle-status");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            // Return the raw JSON from the backend directly to the AJAX caller
            return Content(json, "application/json");
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