using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Blog;
using Nanny_BackEnd.Services.Interfaces;
using System.Security.Claims;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlogController : ControllerBase
{
    private readonly IBlogService _blogService;
    public BlogController(IBlogService blogService)
    {
        _blogService = blogService;
    }

    // GET /api/Blog?search=...&page=1&pageSize=9&categoryId=...&sort=newest
    // Public endpoint — chỉ trả về bài đã Published và chưa xoá
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicBlogList(
        [FromQuery] string? search     = null,
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 9,
        [FromQuery] Guid?   categoryId = null,
        [FromQuery] string? sort       = null)
    {
        // Bắt buộc status=1 (Published) và isDeleted=false cho endpoint công khai
        var result = await _blogService.ModeratorViewBlogListAsync(
            search, page, pageSize,
            status: 1, isDeleted: false,
            categoryId, sort);
        return Ok(new { success = true, data = result });
    }

    // GET /api/Blog/moderator-view-blog-list
    [HttpGet("moderator-view-blog-list")]
    [Authorize(Roles = "Moderator")]
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

    // GET /api/Blog/moderator-view-blog-detail/{id}
    [HttpGet("moderator-view-blog-detail/{id:guid}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> ModeratorViewBlogDetail(Guid id)
    {
        var (ok, code, msg, data) = await _blogService.ModeratorViewBlogDetailAsync(id);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, data });
    }

    // GET /api/Blog/by-slug/{slug} — trang chi tiết công khai
    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBlogBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new { success = false, message = "Slug không hợp lệ." });

        var (ok, code, msg, data) = await _blogService.GetBlogBySlugAsync(slug);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, data });
    }

    // GET /api/Blog/categories — cho dropdown lọc danh mục
    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _blogService.GetActiveCategoriesAsync();
        return Ok(new { success = true, data = result });
    }

    // POST /api/Blog/moderator-create-blog
    [HttpPost("moderator-create-blog")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> ModeratorCreateBlog([FromBody] CreateBlogRequest req)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Bạn cần đăng nhập." });

        var (ok, code, msg, data) = await _blogService.ModeratorCreateBlogAsync(req, userId.Value);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return StatusCode(201, new { success = true, message = msg, data });
    }

    // PUT /api/Blog/moderator-update-blog/{id}
    [HttpPut("moderator-update-blog/{id:guid}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> ModeratorUpdateBlog(Guid id, [FromBody] UpdateBlogRequest req)
    {
        var userId = GetUserId();
        var (ok, code, msg) = await _blogService.ModeratorUpdateBlogAsync(id, req, userId);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, message = msg });
    }

    // PUT /api/Blog/moderator-toggle-blog-status/{id}
    [HttpPut("moderator-toggle-blog-status/{id:guid}")]
    [Authorize(Roles = "Moderator")]
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
