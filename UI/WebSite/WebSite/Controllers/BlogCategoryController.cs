using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models;
using WebSite.Models.BlogCategory;

namespace WebSite.Controllers;

[Authorize(Roles = "Moderator")]
[Route("Moderator")]
public class BlogCategoryController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public BlogCategoryController(IHttpClientFactory httpFactory)
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
    public IActionResult CreateBlogCategory() => View("~/Views/Moderator/BlogCategory/CreateBlogCategory.cshtml", new CreateBlogCategoryRequest());

    [HttpPost("CreateBlogCategory")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBlogCategory(CreateBlogCategoryRequest model)
    {
        ValidateCategoryForm(model.Name, model.Slug);
        if (!ModelState.IsValid)
            return View("~/Views/Moderator/BlogCategory/CreateBlogCategory.cshtml", model);

        var body = JsonSerializer.Serialize(new { name = model.Name.Trim(), slug = model.Slug.Trim() });
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
                    toastMessage = "Bạn đã tạo thể loại blog thành công"
                });
            }
            ModelState.AddModelError(nameof(model.Name), result?.Message ?? "Tao danh muc that bai.");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(nameof(model.Name), $"Loi ket noi: {ex.Message}");
        }

        return View("~/Views/Moderator/BlogCategory/CreateBlogCategory.cshtml", model);
    }

    [HttpGet("ViewBlogCategoryDetail/{id:guid}")]
    public async Task<IActionResult> ViewBlogCategoryDetail(Guid id)
    {
        var data = await FetchCategoryByIdAsync(id);
        if (data != null)
            return View("~/Views/Moderator/BlogCategory/ViewBlogCategoryDetail.cshtml", data);

        TempData["Error"] = "Không tìm thấy danh mục.";
        return RedirectToAction(nameof(ManageBlogCategory));
    }

    // POST /Moderator/ViewBlogCategoryDetail/{id}
    [HttpPost("ViewBlogCategoryDetail/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewBlogCategoryDetail(Guid id, UpdateBlogCategoryRequest model)
    {
        ValidateCategoryForm(model.Name, model.Slug);
        if (!ModelState.IsValid)
        {
            var invalidVm = await BuildCategoryDetailViewModelForInvalidPost(id, model);
            return View("~/Views/Moderator/BlogCategory/ViewBlogCategoryDetail.cshtml", invalidVm);
        }

        var body = JsonSerializer.Serialize(new { name = model.Name.Trim(), slug = model.Slug.Trim() });
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
                    toastMessage = "Bạn đã chỉnh sửa thể loại blog thành công"
                });
            }
            ModelState.AddModelError(nameof(model.Name), result?.Message ?? "Cap nhat danh muc that bai.");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(nameof(model.Name), $"Loi ket noi: {ex.Message}");
        }

        var failedVm = await BuildCategoryDetailViewModelForInvalidPost(id, model);
        return View("~/Views/Moderator/BlogCategory/ViewBlogCategoryDetail.cshtml", failedVm);
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

            TempData["Error"] = result?.Message ?? "Thao tac that bai.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Loi ket noi: {ex.Message}";
        }

        return RedirectToAction(nameof(ManageBlogCategory));
    }

    private void ValidateCategoryForm(string? name, string? slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(CreateBlogCategoryRequest.Name), "Name is required.");

        if (string.IsNullOrWhiteSpace(slug))
            ModelState.AddModelError(nameof(CreateBlogCategoryRequest.Slug), "Slug is required.");
    }

    private async Task<BlogCategoryDto?> FetchCategoryByIdAsync(Guid id)
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
            return result?.Success == true ? result.Data : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<BlogCategoryDto> BuildCategoryDetailViewModelForInvalidPost(Guid id, UpdateBlogCategoryRequest model)
    {
        var current = await FetchCategoryByIdAsync(id);
        if (current == null)
        {
            return new BlogCategoryDto
            {
                Id = id,
                Name = model.Name ?? string.Empty,
                Slug = model.Slug ?? string.Empty,
                SortOrder = 0,
                BlogCount = 0,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };
        }

        current.Name = model.Name ?? string.Empty;
        current.Slug = model.Slug ?? string.Empty;
        return current;
    }
}
