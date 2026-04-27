using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.BlogCategory;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Services.Interfaces;
using System.Security.Claims;


namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Moderator")]
public class BlogCategoryController : ControllerBase
{
    private readonly IBlogCategoryService _blogCategoryService;
    public BlogCategoryController(IBlogCategoryService blogCategoryService) {
        _blogCategoryService = blogCategoryService;
    }  
    // GET /api/BlogCategory?search=...&page=1&pageSize=3&isDeleted=false
    [HttpGet("moderator-view-blog-category-list")]
    public async Task<IActionResult> ModeratorViewBlogCategoryList(
        [FromQuery] string? search    = null,
        [FromQuery] int     page      = 1,
        [FromQuery] int     pageSize  = 3,
        [FromQuery] bool?   isDeleted = null)
    {
        var result = await _blogCategoryService.ModeratorViewBlogCategoryListAsync(search, page, pageSize, isDeleted);
        return Ok(new { success = true, data = result });
    }

    // GET /api/BlogCategory/{id}
    [HttpGet("moderator-view-blog-category-detail/{id:guid}")]
    public async Task<IActionResult> ModeratorViewBlogCategoryDetail(Guid id)
    {
        var (ok, code, msg, data) = await _blogCategoryService.ModeratorViewBlogCategoryDetailAsync(id);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, data });
    }

    // POST /api/BlogCategory
    [HttpPost("moderator-create-blog-category")]
    public async Task<IActionResult> ModeratorCreateBlogCategory([FromBody] CreateBlogCategoryRequest req)
    {
        var userId = GetUserId();
        var (ok, code, msg, data) = await _blogCategoryService.ModeratorCreateBlogCategoryAsync(req, userId);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return StatusCode(201, new { success = true, message = msg, data });
    }

    // PUT /api/BlogCategory/{id}
    [HttpPut("moderator-update-blog-category/{id:guid}")]
    public async Task<IActionResult> ModeratorUpdateBlogCategory(Guid id, [FromBody] UpdateBlogCategoryRequest req)
    {
        var userId = GetUserId();
        var (ok, code, msg) = await _blogCategoryService.ModeratorUpdateBlogCategoryAsync(id, req, userId);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, message = msg });
    }

    // PUT /api/BlogCategory/{id}/toggle-status
    [HttpPut("moderator-toggle-status/{id:guid}")]
    public async Task<IActionResult> ModeratorToggleCategoryStatus(Guid id, [FromQuery] bool activate)
    {
        var (success, code, msg) = await _blogCategoryService.ModeratorToggleCategoryStatusAsync(id, activate);
        if (!success) return StatusCode(code, new { success = false, message = msg });

        return Ok(new { success = true, message = msg });
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
