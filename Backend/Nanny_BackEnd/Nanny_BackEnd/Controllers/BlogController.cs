using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Blog;
using Nanny_BackEnd.Services;
using System.Security.Claims;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Moderator")]
public class BlogController : ControllerBase
{
    private readonly BlogService _blogService;
    public BlogController(BlogService blogService)
    {
        _blogService = blogService;
    }
    // GET /api/Blog?search=...&page=1&pageSize=5&status=1&isDeleted=false
    [HttpGet]
    public async Task<IActionResult> ModeratorViewBlogList(
        [FromQuery] string? search     = null,
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 5,
        [FromQuery] int?    status     = null,
        [FromQuery] bool?   isDeleted  = null,
        [FromQuery] Guid?   categoryId = null,
        [FromQuery] string? sort       = null)
    {
        var result = await _blogService.ModeratorViewBlogListAsync(search, page, pageSize, status, isDeleted, categoryId, sort);
        return Ok(new { success = true, data = result });
    }

    // GET /api/Blog/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ModeratorViewBlogDetail(Guid id)
    {
        var (ok, code, msg, data) = await _blogService.ModeratorViewBlogDetailAsync(id);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, data });
    }

    // GET /api/Blog/by-slug/{slug} — public detail page
    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetBlogBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new { success = false, message = "Slug không hợp lệ." });

        var (ok, code, msg, data) = await _blogService.GetBlogBySlugAsync(slug);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, data });
    }

    // GET /api/Blog/categories — for dropdowns
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var repo = HttpContext.RequestServices.GetRequiredService<Nanny_BackEnd.Repositories.BlogRepository>();
        var cats = await repo.GetActiveCategoriesAsync();
        var result = cats.Select(c => new { c.Id, c.Name, c.Slug }).ToList();
        return Ok(new { success = true, data = result });
    }

    // POST /api/Blog
    [HttpPost]
    public async Task<IActionResult> ModeratorCreateBlog([FromBody] CreateBlogRequest req)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Bạn cần đăng nhập." });

        var (ok, code, msg, data) = await _blogService.ModeratorCreateBlogAsync(req, userId.Value);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return StatusCode(201, new { success = true, message = msg, data });
    }

    // PUT /api/Blog/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> ModeratorUpdateBlog(Guid id, [FromBody] UpdateBlogRequest req)
    {
        var userId = GetUserId();
        var (ok, code, msg) = await _blogService.ModeratorUpdateBlogAsync(id, req, userId);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, message = msg });
    }

    // PUT /api/Blog/{id}/toggle-status?activate=true/false
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<IActionResult> ModeratorToggleBlogStatus(Guid id, [FromQuery] bool activate)
    {
        var (ok, code, msg) = await _blogService.ModeratorToggleBlogStatusAsync(id, activate);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, message = msg });
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
