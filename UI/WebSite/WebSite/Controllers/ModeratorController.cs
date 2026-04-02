using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebSite.Enums;
using WebSite.Hubs;
using WebSite.Models;
using WebSite.Models.Account;
using WebSite.Models.FAQ;
using WebSite.Models.BlogCategory;
using WebSite.Models.Blog;
using WebSite.Models.Moderator;
using WebSite.Models.Search;
using WebSite.Services;
using System.Text.Json.Serialization;


namespace WebSite.Controllers;

[Authorize(Roles = "Moderator")]
public class ModeratorController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IBlogImageStorageService _blogImageStorageService;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModeratorController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub,
        IBlogImageStorageService blogImageStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
        _blogImageStorageService = blogImageStorageService;
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/Dashboard
    // ──────────────────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        var model = new ModeratorDashboardDto();
        var token = HttpContext.Session.GetString("AccessToken");

        var dashboardRequest = new HttpRequestMessage(HttpMethod.Get, "/api/Moderator/dashboard");
        if (!string.IsNullOrEmpty(token))
            dashboardRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var dashboardResponse = await _http.SendAsync(dashboardRequest);
            var dashboardJson = await dashboardResponse.Content.ReadAsStringAsync();
            var dashboardResult = JsonSerializer.Deserialize<ApiResult<ApiModeratorDashboardStatsDto>>(dashboardJson, JsonOpts);
            model = dashboardResult?.Data?.ToViewModel() ?? new ModeratorDashboardDto();
        }
        catch
        {
            TempData["Error"] = "Khong the tai du lieu dashboard moderator.";
        }

        var recent = await FetchAccountsAsync(page: 1, pageSize: 5);
        model.RecentAccounts = recent?.Items ?? new List<AccountDto>();

        return View(model);
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ManageAccount
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ManageAccount(
        string? search = null,
        string? role = null,
        int? status = null,
        int page = 1)
    {
        // Preserve query params for UI
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
            TempData["Error"] = "Không thể tải danh sách tài khoản. Vui lòng thử lại.";
            result = new AccountListResponse();
        }

        return View("~/Views/Moderator/Account/ManageAccount.cshtml", result);
    }

    // ──────────────────────────────────────────────
    // POST /Moderator/ToggleStatus
    // Called via AJAX from ManageAccount page
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id, int newStatus)
    {
        var body = JsonSerializer.Serialize(new { status = newStatus });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/accounts/{id}/status")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        // Gắn token nếu có
        var token = HttpContext.Session.GetString("AccessToken");
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
                message = result?.Message ?? "Có lỗi xảy ra."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Lỗi kết nối: {ex.Message}" });
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ViewAccountDetail/{id}
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ViewAccountDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Moderator/accounts/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<AccountDto>>(json, JsonOpts);
            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản.";
                return RedirectToAction(nameof(ManageAccount));
            }
            return View("~/Views/Moderator/Account/ViewAccountDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Lỗi kết nối đến API.";
            return RedirectToAction(nameof(ManageAccount));
        }
    }

    // ──────────────────────────────────────────────
    // POST /Moderator/ViewAccountDetail/{id}
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewAccountDetail(Guid id, ViewAccountDetailRequest model)
    {
        var body = JsonSerializer.Serialize(new { status = model.Status });
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/accounts/{id}/status")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var token = HttpContext.Session.GetString("AccessToken");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageAccount), new
                {
                    toastType = model.Status == 1 ? "success" : "error",
                    toastMessage = model.Status == 1
                        ? "Đã activate account thành công"
                        : "Đã deactivate account thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Cập nhật thất bại.";
            return RedirectToAction(nameof(ManageAccount));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
            return RedirectToAction(nameof(ManageAccount));
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ManageNannyVerification
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ManageNannyVerification(string? search = null, int? status = null, int page = 1)
    {
        ViewBag.Search = search;
        ViewBag.Status = status;

        var qs = new List<string> { $"page={page}", "pageSize=3" };
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");


        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/Moderator/verifications?{string.Join("&", qs)}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<WebSite.Models.Verification.VerificationRequestListResponse>>(json, JsonOpts);
            return View("~/Views/Moderator/NannyVerification/ManageNannyVerification.cshtml", result?.Data ?? new WebSite.Models.Verification.VerificationRequestListResponse());
        }
        catch
        {
            TempData["Error"] = "Không thể tải danh sách xác minh.";
            return View("~/Views/Moderator/NannyVerification/ManageNannyVerification.cshtml", new WebSite.Models.Verification.VerificationRequestListResponse());
        }
    }

    // ──────────────────────────────────────────────
    // GET /Moderator/ViewNannyVerificationDetail/{id}
    // ──────────────────────────────────────────────
    public async Task<IActionResult> ViewNannyVerificationDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Moderator/verifications/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<WebSite.Models.Verification.VerificationRequestDetailDto>>(json, JsonOpts);
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

    // ──────────────────────────────────────────────
    // POST /Moderator/ReviewVerification/{id}
    // ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewVerification(Guid id, int action, string? rejectionReason, Guid? nannyUserId = null)
    {
        var body = JsonSerializer.Serialize(new
        {
            action,
            rejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason.Trim()
        });
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/Moderator/verifications/{id}/review")
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

    public IActionResult ViewReports()         => View();
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
                TempData["Success"] = "Tạo FAQ thành công!";
                return RedirectToAction(nameof(ManageFAQ));
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
                    toastMessage = "Cập nhật FAQ thành công."
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
                    toastMessage = "Tạo danh mục thành công."
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
                    toastMessage = "Cập nhật danh mục thành công."
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
                        ? "Blog category activated successfully."
                        : "Blog category deactivated successfully."
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
    private async Task<AccountListResponse?> FetchAccountsAsync(
        string? role = null,
        int? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 3)
    {
        var token = HttpContext.Session.GetString("AccessToken");

        var qs = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrWhiteSpace(role)) qs.Add($"role={Uri.EscapeDataString(role)}");
        if (status.HasValue) qs.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");

        var url = $"/api/Moderator/accounts?{string.Join("&", qs)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
    // ════════════════════════════════════════════════
    // BLOG MANAGEMENT
    // ════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBlogContentImages(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
        {
            return Json(new { success = false, message = "Vui long chon it nhat mot anh." });
        }

        try
        {
            var uploadedUrls = await _blogImageStorageService.UploadAsync(files, cancellationToken);
            if (uploadedUrls.Count == 0)
            {
                return Json(new { success = false, message = "Khong co anh hop le de upload." });
            }

            return Json(new
            {
                success = true,
                message = uploadedUrls.Count == 1 ? "Upload anh thanh cong." : "Upload cac anh thanh cong.",
                data = new
                {
                    urls = uploadedUrls
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Khong the upload anh blog: {ex.Message}" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBlogContentVideos(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
        {
            return Json(new { success = false, message = "Vui long chon it nhat mot video." });
        }

        try
        {
            var uploadedUrls = await _blogImageStorageService.UploadVideosAsync(files, cancellationToken);
            if (uploadedUrls.Count == 0)
            {
                return Json(new { success = false, message = "Khong co video hop le de upload." });
            }

            return Json(new
            {
                success = true,
                message = uploadedUrls.Count == 1 ? "Upload video thanh cong." : "Upload cac video thanh cong.",
                data = new
                {
                    urls = uploadedUrls
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Khong the upload video blog: {ex.Message}" });
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
                TempData["Success"] = "Tạo bài viết thành công.";
                return RedirectToAction(nameof(ManageBlog));
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
                    toastMessage = "Cập nhật bài viết thành công."
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

        var url = $"api/Moderator/job-postings?page={page}&pageSize=10";
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
            var response = await _http.GetAsync($"/api/Moderator/job-postings/{id}");
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
        var response = await _http.PatchAsJsonAsync($"api/Moderator/job-postings/{id}/review", body);

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
