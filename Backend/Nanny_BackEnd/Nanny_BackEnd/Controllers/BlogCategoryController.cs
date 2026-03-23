using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.BlogCategory;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Services;
using System.Security.Claims;


namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize(Roles = "Moderator")]
public class BlogCategoryController : ControllerBase
{
    private readonly BlogCategoryService _svc;
    public BlogCategoryController(BlogCategoryService svc) => _svc = svc;

    // GET /api/BlogCategory?search=...&page=1&pageSize=3&isDeleted=false
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search    = null,
        [FromQuery] int     page      = 1,
        [FromQuery] int     pageSize  = 3,
        [FromQuery] bool?   isDeleted = null)
    {
        var result = await _svc.GetCategoriesAsync(search, page, pageSize, isDeleted);
        return Ok(new { success = true, data = result });
    }

    // GET /api/BlogCategory/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var (ok, code, msg, data) = await _svc.GetCategoryAsync(id);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, data });
    }

    // POST /api/BlogCategory
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBlogCategoryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Name))
            return BadRequest(new { success = false, message = "Name không được để trống." });
        if (string.IsNullOrWhiteSpace(req?.Slug))
            return BadRequest(new { success = false, message = "Slug không được để trống." });

        var userId = GetUserId();
        var (ok, code, msg, data) = await _svc.CreateCategoryAsync(req, userId);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return StatusCode(201, new { success = true, message = msg, data });
    }

    // PUT /api/BlogCategory/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBlogCategoryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Name))
            return BadRequest(new { success = false, message = "Name không được để trống." });
        if (string.IsNullOrWhiteSpace(req?.Slug))
            return BadRequest(new { success = false, message = "Slug không được để trống." });

        var userId = GetUserId();
        var (ok, code, msg) = await _svc.UpdateCategoryAsync(id, req, userId);
        if (!ok) return StatusCode(code, new { success = false, message = msg });
        return Ok(new { success = true, message = msg });
    }

    // PUT /api/BlogCategory/{id}/toggle-status
    [HttpPut("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(Guid id, [FromQuery] bool activate)
    {
        var (success, code, msg) = await _svc.ToggleCategoryStatusAsync(id, activate);
        if (!success) return StatusCode(code, new { success = false, message = msg });

        return Ok(new { success = true, message = msg });
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
