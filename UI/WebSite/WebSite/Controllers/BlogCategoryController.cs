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
public class BlogCategoryController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public BlogCategoryController(
        IHttpClientFactory httpFactory,
        IHubContext<NotificationHub> notificationHub,
        IAzureBlobStorageService blobStorageService)
    {
        _http = httpFactory.CreateClient("BackendApi");
    }

    [HttpGet("ManageBlogCategory")]
    public async Task<IActionResult> ManageBlogCategory(string? search = null, int page = 1, bool? isDeleted = null)
    {
        const int pageSize = 3;
        var token = HttpContext.Session.GetString("AccessToken");

        var qs = $"?search={Uri.EscapeDataString(search ?? "")}&page={page}&pageSize={pageSize}";
        if (isDeleted.HasValue) qs += $"&isDeleted={isDeleted.Value.ToString().ToLower()}";
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/BlogCategory/moderator-view-blog-category-list{qs}");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<BlogCategoryListResponse>>(json, JsonOpts);

            ViewBag.Search = search ?? "";
            ViewBag.IsDeleted = isDeleted.HasValue ? isDeleted.Value.ToString().ToLower() : "";
            return View("~/Views/Moderator/BlogCategory/ManageBlogCategory.cshtml", result?.Data ?? new BlogCategoryListResponse { Page = page, PageSize = pageSize });
        }
        catch
        {
            ViewBag.Search = search ?? "";
            ViewBag.IsDeleted = "";
            TempData["Error"] = "Không thể tải danh sách danh mục.";
            return View("~/Views/Moderator/BlogCategory/ManageBlogCategory.cshtml", new BlogCategoryListResponse { Page = page, PageSize = pageSize });
        }
    }

    [HttpGet("CreateBlogCategory")]
    public IActionResult CreateBlogCategory() => View("~/Views/Moderator/BlogCategory/CreateBlogCategory.cshtml");

    [HttpPost("CreateBlogCategory")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBlogCategory(CreateBlogCategoryRequest model)
    {
        var body = JsonSerializer.Serialize(new { name = model.Name, slug = model.Slug });
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/BlogCategory/moderator-create-blog-category")
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

        return View("~/Views/Moderator/BlogCategory/CreateBlogCategory.cshtml", model);
    }

    [HttpGet("ViewBlogCategoryDetail/{id:guid}")]
    public async Task<IActionResult> ViewBlogCategoryDetail(Guid id)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/BlogCategory/moderator-view-blog-category-detail/{id}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult<BlogCategoryDto>>(json, JsonOpts);

            if (result?.Success == true && result.Data != null)
                return View("~/Views/Moderator/BlogCategory/ViewBlogCategoryDetail.cshtml", result.Data);

            TempData["Error"] = result?.Message ?? "Không tìm thấy danh mục.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi kết nối: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageBlogCategory));
    }

    // POST /Moderator/ViewBlogCategoryDetail/{id}
    [HttpPost("ViewBlogCategoryDetail/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewBlogCategoryDetail(Guid id, UpdateBlogCategoryRequest model)
    {
        var body = JsonSerializer.Serialize(new { name = model.Name, slug = model.Slug });
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/BlogCategory/moderator-update-blog-category/{id}")
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
    [HttpPost("ToggleBlogCategoryStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlogCategoryStatus(Guid id, bool activate)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/BlogCategory/moderator-toggle-status/{id}?activate={(activate ? "true" : "false")}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResult>(json, JsonOpts);
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


}
