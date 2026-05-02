using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Enums;
using WebSite.Models;
using WebSite.Models.Blog;
using WebSite.Models.BlogCategory;
using WebSite.Services;

namespace WebSite.Controllers;

public class BlogController : Controller
{
    private readonly HttpClient _http;
    private readonly IAzureBlobStorageService _blobStorageService;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public BlogController(IHttpClientFactory httpFactory, IAzureBlobStorageService blobStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
        _blobStorageService = blobStorageService;
    }

    // GET /Blog
    public async Task<IActionResult> Index(string? search, Guid? categoryId, int page = 1, string? sort = null)
    {
        const int pageSize = 9;
        var url = $"/api/Blog?status=1&isDeleted=false&page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (categoryId.HasValue) url += $"&categoryId={categoryId.Value}";
        if (!string.IsNullOrWhiteSpace(sort)) url += $"&sort={sort}";

        BlogListResponse blogs = new();
        List<BlogCategoryOption> categories = [];

        try
        {
            var blogsTask = _http.GetAsync(url);
            var catsTask = _http.GetAsync("/api/Blog/categories");
            await Task.WhenAll(blogsTask, catsTask);

            var blogsResp = await blogsTask;
            if (blogsResp.IsSuccessStatusCode)
            {
                var json = await blogsResp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Envelope<BlogListResponse>>(json, JsonOpts);
                if (result?.Data != null) blogs = result.Data;
            }

            var catsResp = await catsTask;
            if (catsResp.IsSuccessStatusCode)
            {
                var json = await catsResp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Envelope<List<BlogCategoryOption>>>(json, JsonOpts);
                categories = result?.Data ?? [];
            }
        }
        catch { }

        ViewBag.Categories = categories;
        ViewBag.Search = search ?? "";
        ViewBag.CategoryId = categoryId;
        ViewBag.Sort = sort ?? "newest";
        return View(blogs);
    }

    // GET /Blog/{slug}
    [Route("Blog/{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        BlogDto? blog = null;
        List<BlogDto> related = [];

        try
        {
            var resp = await _http.GetAsync($"/api/Blog/by-slug/{Uri.EscapeDataString(slug)}");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Envelope<BlogDto>>(json, JsonOpts);
                blog = result?.Data;
            }

            if (blog != null)
            {
                var relUrl = $"/api/Blog?status=1&isDeleted=false&page=1&pageSize=4";
                if (blog.CategoryId.HasValue) relUrl += $"&categoryId={blog.CategoryId.Value}";
                var relResp = await _http.GetAsync(relUrl);
                if (relResp.IsSuccessStatusCode)
                {
                    var json = await relResp.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Envelope<BlogListResponse>>(json, JsonOpts);
                    related = result?.Data?.Items
                        .Where(b => b.Id != blog.Id)
                        .Take(3)
                        .ToList() ?? [];
                }
            }
        }
        catch { }

        if (blog == null) return NotFound();

        ViewBag.Related = related;
        return View(blog);
    }

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/UploadBlogContentImages")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBlogContentImages(List<IFormFile>? files, CancellationToken cancellationToken)
        => await UploadBlogContentMediaCore(files, BlobMediaType.Image, cancellationToken);

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/UploadBlogContentVideos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBlogContentVideos(List<IFormFile>? files, CancellationToken cancellationToken)
        => await UploadBlogContentMediaCore(files, BlobMediaType.Video, cancellationToken);

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/UploadBlogContentMedia")]
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

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ManageBlog")]
    public async Task<IActionResult> ManageBlog(
        string? search = null, int page = 1, int? status = null, bool? isDeleted = null, Guid? categoryId = null)
    {
        const int pageSize = 10;
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

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/CreateBlog")]
    public async Task<IActionResult> CreateBlog()
    {
        ViewBag.Categories = await FetchBlogCategoriesAsync();
        return View("~/Views/Moderator/Blog/CreateBlog.cshtml");
    }

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/CreateBlog")]
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

    [Authorize(Roles = "Moderator")]
    [HttpGet("/Moderator/ViewBlogDetail/{id:guid}")]
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

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/ViewBlogDetail/{id:guid}")]
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
            blogData.Title = model.Title;
            blogData.Slug = model.Slug;
            blogData.Content = model.Content;
            blogData.Summary = model.Summary;
            blogData.ThumbnailUrl = model.ThumbnailUrl;
            blogData.CategoryId = model.CategoryId;
            blogData.Status = model.Status;
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

    [Authorize(Roles = "Moderator")]
    [HttpPost("/Moderator/ToggleBlogStatus")]
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
                    toastType = activate ? "success" : "warning",
                    toastMessage = activate
                        ? "Kích hoạt bài viết thành công."
                        : "Ẩn bài viết thành công."
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
        catch
        {
            return new List<BlogCategoryOption>();
        }
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

    private async Task<IActionResult> UploadBlogContentMediaCore(List<IFormFile>? files, BlobMediaType mediaType, CancellationToken cancellationToken)
    {
        var mediaLabel = mediaType == BlobMediaType.Video ? "video" : "anh";
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
                    message = uploadedUrls.Count == 1 ? $"Upload {mediaLabel} thành công." : $"Upload các {mediaLabel} thành công.",
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

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
    }
}
