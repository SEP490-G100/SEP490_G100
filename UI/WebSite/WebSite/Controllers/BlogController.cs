using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Blog;

namespace WebSite.Controllers;

public class BlogController : Controller
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public BlogController(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient("BackendApi");
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
            var catsTask  = _http.GetAsync("/api/Blog/categories");
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
        catch { /* log nếu cần */ }

        ViewBag.Categories = categories;
        ViewBag.Search     = search ?? "";
        ViewBag.CategoryId = categoryId;
        ViewBag.Sort       = sort ?? "newest";
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
        catch { /* log nếu cần */ }

        if (blog == null) return NotFound();

        ViewBag.Related = related;
        return View(blog);
    }

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
    }
}
