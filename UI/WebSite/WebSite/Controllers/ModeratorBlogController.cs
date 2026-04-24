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
[Route("Moderator")]
public class ModeratorBlogController : Controller
{
    private readonly HttpClient _http;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly IAzureBlobStorageService _blobStorageService;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModeratorBlogController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub,
        IAzureBlobStorageService blobStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _notificationHub = notificationHub;
        _blobStorageService = blobStorageService;
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
            return Json(new { success = false, message = "Loại phương tiện không hợp lệ. Chỉ hỗ trợ ảnh/video." });
        }

        var type = normalized == "video" ? BlobMediaType.Video : BlobMediaType.Image;
        return await UploadBlogContentMediaCore(files, type, cancellationToken);
    }

    private async Task<IActionResult> UploadBlogContentMediaCore(List<IFormFile>? files, BlobMediaType mediaType, CancellationToken cancellationToken)
    {
        var mediaLabel = mediaType == BlobMediaType.Video ? "video" : "ảnh";
        if (files == null || files.Count == 0)
        {
            return Json(new { success = false, message = $"Vui lòng chọn ít nhất một {mediaLabel}." });
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
                return Json(new { success = false, message = $"Không có {mediaLabel} hợp lệ để tải lên." });
            }

            return Json(new
            {
                success = true,
                message = uploadedUrls.Count == 1 ? $"Upload {mediaLabel} thành công." : $"Upload cac {mediaLabel} thành công.",
                data = new
                {
                    urls = uploadedUrls
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Không thể tải lên {mediaLabel} blog: {ex.Message}" });
        }
    }

    [HttpGet("ManageBlog")]
    public async Task<IActionResult> ManageBlog(
        string? search = null, int page = 1, int? status = null, bool? isDeleted = null, Guid? categoryId = null)
    {
        const int pageSize = 3;
        var token = HttpContext.Session.GetString("AccessToken");

        var qs = $"?search={Uri.EscapeDataString(search ?? "")}&page={page}&pageSize={pageSize}";
        if (status.HasValue) qs += $"&status={status.Value}";
        if (isDeleted.HasValue) qs += $"&isDeleted={isDeleted.Value.ToString().ToLower()}";
        if (categoryId.HasValue) qs += $"&categoryId={categoryId.Value}";

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/Blog/moderator-view-blog-list{qs}");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<BlogListResponse>>(json, JsonOpts);

            ViewBag.Search = search ?? "";
            ViewBag.Status = status.HasValue ? status.Value.ToString() : "";
            ViewBag.IsDeleted = isDeleted.HasValue ? isDeleted.Value.ToString().ToLower() : "";
            ViewBag.CategoryId = categoryId.HasValue ? categoryId.Value.ToString() : "";
            ViewBag.Categories = await FetchBlogCategoriesAsync();
            return View("~/Views/Moderator/Blog/ManageBlog.cshtml", result?.Data ?? new BlogListResponse { Page = page, PageSize = pageSize });
        }
        catch
        {
            ViewBag.Search = search ?? "";
            ViewBag.Status = "";
            ViewBag.IsDeleted = "";
            ViewBag.CategoryId = "";
            ViewBag.Categories = new List<BlogCategoryOption>();
            TempData["Error"] = "Không thể tải danh sách bài viết.";
            return View("~/Views/Moderator/Blog/ManageBlog.cshtml", new BlogListResponse { Page = page, PageSize = pageSize });
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
    [HttpGet("CreateBlog")]
    public async Task<IActionResult> CreateBlog()
    {
        ViewBag.Categories = await FetchBlogCategoriesAsync();
        return View("~/Views/Moderator/Blog/CreateBlog.cshtml");
    }

    // POST /Moderator/CreateBlog
    [HttpPost("CreateBlog")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBlog(CreateBlogRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Slug)
            || string.IsNullOrWhiteSpace(model.Content))
        {
            TempData["Error"] = "Title, Slug và Content không được để trống.";
            ViewBag.Categories = await FetchBlogCategoriesAsync();
            return View("~/Views/Moderator/Blog/CreateBlog.cshtml", model);
        }

        var token = HttpContext.Session.GetString("AccessToken");
        var payload = JsonSerializer.Serialize(new
        {
            title = model.Title.Trim(),
            slug = model.Slug.Trim().ToLower(),
            content = model.Content.Trim(),
            summary = model.Summary?.Trim(),
            thumbnailUrl = model.ThumbnailUrl?.Trim(),
            categoryId = model.CategoryId,
            status = model.Status
        });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/Blog/moderator-create-blog")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlog), new
                {
                    toastType = "success",
                    toastMessage = "Bạn đã tạo bài blog thành công"
                });
            }
            TempData["Error"] = result?.Message ?? "Tạo bài viết thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        ViewBag.Categories = await FetchBlogCategoriesAsync();
        return View("~/Views/Moderator/Blog/CreateBlog.cshtml", model);
    }

    [HttpGet("ViewBlogDetail/{id:guid}")]
    public async Task<IActionResult> ViewBlogDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/Blog/moderator-view-blog-detail/{id}");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<BlogDto>>(json, JsonOpts);

            if (result?.Success != true || result.Data == null)
            {
                TempData["Error"] = "Không tìm thấy bài viết.";
                return RedirectToAction(nameof(ManageBlog));
            }

            ViewBag.Categories = await FetchBlogCategoriesAsync();
            return View("~/Views/Moderator/Blog/ViewBlogDetail.cshtml", result.Data);
        }
        catch
        {
            TempData["Error"] = "Không thể tải bài viết.";
            return RedirectToAction(nameof(ManageBlog));
        }
    }

    [HttpPost("ViewBlogDetail/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewBlogDetail(Guid id, UpdateBlogRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Slug)
            || string.IsNullOrWhiteSpace(model.Content))
        {
            TempData["Error"] = "Title, Slug và Content không được để trống.";
            ViewBag.Categories = await FetchBlogCategoriesAsync();
            var blogReq = new HttpRequestMessage(HttpMethod.Get, $"/api/Blog/moderator-view-blog-detail/{id}");
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
            return View("~/Views/Moderator/Blog/ViewBlogDetail.cshtml", blogData);
        }

        var token = HttpContext.Session.GetString("AccessToken");
        var payload = JsonSerializer.Serialize(new
        {
            title = model.Title.Trim(),
            slug = model.Slug.Trim().ToLower(),
            content = model.Content.Trim(),
            summary = model.Summary?.Trim(),
            thumbnailUrl = model.ThumbnailUrl?.Trim(),
            categoryId = model.CategoryId,
            status = model.Status
        });
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/Blog/moderator-update-blog/{id}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);

            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlog), new
                {
                    toastType = "success",
                    toastMessage = "Bạn đã chỉnh sửa bài blog thành công"
                });
            }

            TempData["Error"] = result?.Message ?? "Cập nhật thất bại.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        ViewBag.Categories = await FetchBlogCategoriesAsync();
        var detailVm = await FetchBlogByIdAsync(id);
        if (detailVm == null)
        {
            return RedirectToAction(nameof(ManageBlog), new
            {
                toastType = "error",
                toastMessage = "Không thể tải lại bài blog để chỉnh sửa"
            });
        }

        detailVm.Title = model.Title ?? detailVm.Title;
        detailVm.Slug = model.Slug ?? detailVm.Slug;
        detailVm.Content = model.Content ?? detailVm.Content;
        detailVm.Summary = model.Summary;
        detailVm.ThumbnailUrl = model.ThumbnailUrl;
        detailVm.CategoryId = model.CategoryId;
        detailVm.Status = model.Status;
        return View("~/Views/Moderator/Blog/ViewBlogDetail.cshtml", detailVm);
    }

    [HttpPost("ToggleBlogStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlogStatus(Guid id, bool activate)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/Blog/moderator-toggle-blog-status/{id}?activate={(activate ? "true" : "false")}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
            if (result?.Success == true)
            {
                return RedirectToAction(nameof(ManageBlog), new
                {
                    toastType = "success",
                    toastMessage = activate
                        ? "Đã kích hoạt blog thành công."
                        : "Đã vô hiệu hóa blog thành công."
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

    private async Task<BlogDto?> FetchBlogByIdAsync(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/Blog/moderator-view-blog-detail/{id}");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<BlogDto>>(json, JsonOpts);
            return result?.Success == true ? result.Data : null;
        }
        catch
        {
            return null;
        }
    }


}
