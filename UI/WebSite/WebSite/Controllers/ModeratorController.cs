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

    public IActionResult ManageBlogs()         => View();
    public IActionResult ModerateJobPostings() => View();

    // ──────────────────────────────────────────────
    // GET /Moderator/ManageFAQ
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ManageFAQ(
        string? search   = null,
        bool?   isActive = null,
        string? category = null,
        int     page     = 1)
    {
        ViewBag.Search   = search;
        ViewBag.IsActive = isActive?.ToString() ?? "";
        ViewBag.Category = category ?? "";

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (!string.IsNullOrWhiteSpace(search))   qs.Add($"search={Uri.EscapeDataString(search)}");
        if (isActive.HasValue)                     qs.Add($"isActive={isActive.Value.ToString().ToLower()}");
        if (!string.IsNullOrWhiteSpace(category))  qs.Add($"category={Uri.EscapeDataString(category)}");

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
            var result   = JsonSerializer.Deserialize<ApiResult<FaqListResponse>>(listJson, JsonOpts);

            try
            {
                var catResp  = await _http.SendAsync(catReq);
                var catJson  = await catResp.Content.ReadAsStringAsync();
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
            answer   = model.Answer,
            category = model.Category,
            isActive = model.IsActive
            // SortOrder auto-assigned by backend
        });
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Faq")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
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
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Faq/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult<FaqDto>>(json, JsonOpts);
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
            question  = model.Question,
            answer    = model.Answer,
            isActive  = model.IsActive
        });
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/Faq/{id}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
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
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Faq/{id}/toggle-status");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            // Return the raw JSON from the backend directly to the AJAX caller
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Lỗi kết nối: {ex.Message}" });
        }
    }

    // ══════════════════════════════════════════════════
    // BLOG CATEGORIES
    // ══════════════════════════════════════════════════

    // GET /Moderator/ManageBlogCategory
    public async Task<IActionResult> ManageBlogCategory(string? search = null, int page = 1, bool? isDeleted = null)
    {
        const int pageSize = 3;
        var token = HttpContext.Session.GetString("AccessToken");

        var qs = $"?search={Uri.EscapeDataString(search ?? "")}&page={page}&pageSize={pageSize}";
        if (isDeleted.HasValue) qs += $"&isDeleted={isDeleted.Value.ToString().ToLower()}";
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/BlogCategory{qs}");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult<BlogCategoryListResponse>>(json, JsonOpts);

            ViewBag.Search    = search ?? "";
            ViewBag.IsDeleted = isDeleted.HasValue ? isDeleted.Value.ToString().ToLower() : "";
            return View("BlogCategory/ManageBlogCategory", result?.Data ?? new BlogCategoryListResponse { Page = page, PageSize = pageSize });
        }
        catch
        {
            ViewBag.Search    = search ?? "";
            ViewBag.IsDeleted = "";
            TempData["Error"] = "Không thể tải danh sách danh mục.";
            return View("BlogCategory/ManageBlogCategory", new BlogCategoryListResponse { Page = page, PageSize = pageSize });
        }
    }

    // GET /Moderator/CreateBlogCategory
    public IActionResult CreateBlogCategory() => View("BlogCategory/CreateBlogCategory");

    // POST /Moderator/CreateBlogCategory
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBlogCategory(CreateBlogCategoryRequest model)
    {
        var body    = JsonSerializer.Serialize(new { name = model.Name, slug = model.Slug });
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/BlogCategory")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlogCategory), new
                {
                    toastType = "success",
                    toastMessage = "Tạo mới Blog category thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Tạo danh mục thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return View("BlogCategory/CreateBlogCategory", model);
    }

    // GET /Moderator/ViewBlogCategoryDetail/{id}
    public async Task<IActionResult> ViewBlogCategoryDetail(Guid id)
    {
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/BlogCategory/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult<BlogCategoryDto>>(json, JsonOpts);

            if (result?.Success == true && result.Data != null)
                return View("BlogCategory/ViewBlogCategoryDetail", result.Data);

            TempData["Error"] = result?.Message ?? "Không tìm thấy danh mục.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageBlogCategory));
    }

    // POST /Moderator/ViewBlogCategoryDetail/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewBlogCategoryDetail(Guid id, UpdateBlogCategoryRequest model)
    {
        var body    = JsonSerializer.Serialize(new { name = model.Name, slug = model.Slug });
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/BlogCategory/{id}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlogCategory), new
                {
                    toastType = "success",
                    toastMessage = "Cập nhật Blog ccategory thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Cập nhật danh mục thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ViewBlogCategoryDetail), new { id });
    }

    // POST /Moderator/ToggleBlogCategoryStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlogCategoryStatus(Guid id, bool activate)
    {
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/BlogCategory/{id}/toggle-status?activate={(activate ? "true" : "false")}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlogCategory), new
                {
                    toastType = activate ? "success" : "warning",
                    toastMessage = activate
                        ? "Activate blog category thành công"
                        : "Deactivate blog category thành công"
                });
            }

            TempData["Error"] = result?.Message ?? "Thao tác thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageBlogCategory));
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
    // ════════════════════════════════════════════════
    // BLOG MANAGEMENT
    // ════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBlogContentImages(List<IFormFile>? files, CancellationToken cancellationToken)
        => await UploadBlogContentMediaCore(files, BlobMediaType.Image, cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBlogContentVideos(List<IFormFile>? files, CancellationToken cancellationToken)
        => await UploadBlogContentMediaCore(files, BlobMediaType.Video, cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBlogContentMedia(List<IFormFile>? files, [FromQuery] string? mediaType, CancellationToken cancellationToken)
    {
        var normalized = mediaType?.Trim().ToLowerInvariant();
        if (normalized is not ("image" or "video"))
        {
            return Json(new { success = false, message = "Loai media khong hop le. Chi ho tro image/video." });
        }

        var type = normalized == "video" ? BlobMediaType.Video : BlobMediaType.Image;
        return await UploadBlogContentMediaCore(files, type, cancellationToken);
    }

    private async Task<IActionResult> UploadBlogContentMediaCore(List<IFormFile>? files, BlobMediaType mediaType, CancellationToken cancellationToken)
    {
        var mediaLabel = mediaType == BlobMediaType.Video ? "video" : "anh";
        if (files == null || files.Count == 0)
        {
            return Json(new { success = false, message = $"Vui long chon it nhat mot {mediaLabel}." });
        }

        try
        {
            var uploadedUrls = await _blobStorageService.UploadMediaAsync(
                files,
                BlobStorageContainerKind.BlogMedia,
                mediaType,
                cancellationToken);
            if (uploadedUrls.Count == 0)
            {
                return Json(new { success = false, message = $"Khong co {mediaLabel} hop le de upload." });
            }

            return Json(new
            {
                success = true,
                message = uploadedUrls.Count == 1 ? $"Upload {mediaLabel} thanh cong." : $"Upload cac {mediaLabel} thanh cong.",
                data = new
                {
                    urls = uploadedUrls
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Khong the upload {mediaLabel} blog: {ex.Message}" });
        }
    }

    // GET /Moderator/ManageBlog
    public async Task<IActionResult> ManageBlog(
        string? search = null, int page = 1, int? status = null, bool? isDeleted = null, Guid? categoryId = null)
    {
        const int pageSize = 3;
        var token = HttpContext.Session.GetString("AccessToken");

        var qs = $"?search={Uri.EscapeDataString(search ?? "")}&page={page}&pageSize={pageSize}";
        if (status.HasValue)    qs += $"&status={status.Value}";
        if (isDeleted.HasValue) qs += $"&isDeleted={isDeleted.Value.ToString().ToLower()}";
        if (categoryId.HasValue) qs += $"&categoryId={categoryId.Value}";

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/Blog{qs}");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult<BlogListResponse>>(json, JsonOpts);

            ViewBag.Search    = search ?? "";
            ViewBag.Status    = status.HasValue ? status.Value.ToString() : "";
            ViewBag.IsDeleted = isDeleted.HasValue ? isDeleted.Value.ToString().ToLower() : "";
            ViewBag.CategoryId = categoryId.HasValue ? categoryId.Value.ToString() : "";
            ViewBag.Categories = await FetchBlogCategoriesAsync();
            return View("Blog/ManageBlog", result?.Data ?? new BlogListResponse { Page = page, PageSize = pageSize });
        }
        catch
        {
            ViewBag.Search    = search ?? "";
            ViewBag.Status    = "";
            ViewBag.IsDeleted = "";
            ViewBag.CategoryId = "";
            ViewBag.Categories = new List<BlogCategoryOption>();
            TempData["Error"] = "Không thể tải danh sách bài viết.";
            return View("Blog/ManageBlog", new BlogListResponse { Page = page, PageSize = pageSize });
        }
    }

    private async Task<List<BlogCategoryOption>> FetchBlogCategoriesAsync()
    {
        var token = HttpContext.Session.GetString("AccessToken");
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/Blog/categories");
            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            var r = JsonSerializer.Deserialize<ApiResult<List<BlogCategoryOption>>>(json, JsonOpts);
            return r?.Data ?? new List<BlogCategoryOption>();
        }
        catch { return new List<BlogCategoryOption>(); }
    }

    // GET /Moderator/CreateBlog
    public async Task<IActionResult> CreateBlog()
    {
        ViewBag.Categories = await FetchBlogCategoriesAsync();
        return View("Blog/CreateBlog");
    }

    // POST /Moderator/CreateBlog
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBlog(CreateBlogRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Slug)
            || string.IsNullOrWhiteSpace(model.Content))
        {
            TempData["Error"] = "Title, Slug và Content không được để trống.";
            ViewBag.Categories = await FetchBlogCategoriesAsync();
            return View("Blog/CreateBlog", model);
        }

        var token = HttpContext.Session.GetString("AccessToken");
        var payload = JsonSerializer.Serialize(new
        {
            title        = model.Title.Trim(),
            slug         = model.Slug.Trim().ToLower(),
            content      = model.Content.Trim(),
            summary      = model.Summary?.Trim(),
            thumbnailUrl = model.ThumbnailUrl?.Trim(),
            categoryId   = model.CategoryId,
            status       = model.Status
        });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/Blog")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlog), new
                {
                    toastType = "success",
                    toastMessage = "Đã tạo blog thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Tạo bài viết thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        ViewBag.Categories = await FetchBlogCategoriesAsync();
        return View("Blog/CreateBlog", model);
    }

    // GET /Moderator/ViewBlogDetail/{id}
    public async Task<IActionResult> ViewBlogDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/Blog/{id}");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult<BlogDto>>(json, JsonOpts);

            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy bài viết.";
                return RedirectToAction(nameof(ManageBlog));
            }

            ViewBag.Categories = await FetchBlogCategoriesAsync();
            return View("Blog/ViewBlogDetail", result.Data);
        }
        catch
        {
            TempData["Error"] = "Không thể tải bài viết.";
            return RedirectToAction(nameof(ManageBlog));
        }
    }

    // POST /Moderator/ViewBlogDetail/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewBlogDetail(Guid id, UpdateBlogRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Slug)
            || string.IsNullOrWhiteSpace(model.Content))
        {
            TempData["Error"] = "Title, Slug và Content không được để trống.";
            ViewBag.Categories = await FetchBlogCategoriesAsync();
            var blogReq = new HttpRequestMessage(HttpMethod.Get, $"/api/Blog/{id}");
            var token2 = HttpContext.Session.GetString("AccessToken");
            if (!string.IsNullOrEmpty(token2))
                blogReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);
            var bResp = await _http.SendAsync(blogReq);
            var bJson = await bResp.Content.ReadAsStringAsync();
            var bResult = JsonSerializer.Deserialize<ApiResult<BlogDto>>(bJson, JsonOpts);
            var blogData = bResult?.Data ?? new BlogDto { Id = id };
            blogData.Title = model.Title; blogData.Slug = model.Slug; blogData.Content = model.Content;
            blogData.Summary = model.Summary; blogData.ThumbnailUrl = model.ThumbnailUrl;
            blogData.CategoryId = model.CategoryId; blogData.Status = model.Status;
            return View("Blog/ViewBlogDetail", blogData);
        }

        var token = HttpContext.Session.GetString("AccessToken");
        var payload = JsonSerializer.Serialize(new
        {
            title        = model.Title.Trim(),
            slug         = model.Slug.Trim().ToLower(),
            content      = model.Content.Trim(),
            summary      = model.Summary?.Trim(),
            thumbnailUrl = model.ThumbnailUrl?.Trim(),
            categoryId   = model.CategoryId,
            status       = model.Status
        });
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/Blog/{id}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlog), new
                {
                    toastType = "success",
                    toastMessage = "Đã chỉnh sửa blog thành công"
                });
            }

            TempData["Error"] = result?.Message ?? "Cập nhật thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        ViewBag.Categories = await FetchBlogCategoriesAsync();
        return RedirectToAction("ViewBlogDetail", new { id });
    }

    // POST /Moderator/ToggleBlogStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlogStatus(Guid id, bool activate)
    {
        var token   = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/Blog/{id}/toggle-status?activate={(activate ? "true" : "false")}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();
            var result   = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlog), new
                {
                    toastType = activate ? "success" : "warning",
                    toastMessage = activate
                        ? "Blog activated successfully."
                        : "Blog deactivated successfully."
                });
            }

            TempData["Error"] = result?.Message ?? "Thao tác thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageBlog));
    }

    // ─────────────────────────────────────────────────────
    // JOB POSTING MODERATION
    // ─────────────────────────────────────────────────────

    [HttpGet]
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

    [HttpGet]
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

    [HttpPost]
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

public class JobPostingListResponse
{
    [JsonPropertyName("items")]
    public List<SearchJobResponse> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
