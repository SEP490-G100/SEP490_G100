using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.FAQ;
using Nanny_BackEnd.Services;
using System.Security.Claims;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Moderator")]
public class FaqController : ControllerBase
{
    private readonly FaqService _faqService;

    public FaqController(FaqService faqService)
    {
        _faqService = faqService;
    }

    // ─────────────────────────────────────────────────────
    // GET /api/Faq?search=...&isActive=true&category=Payment&page=1&pageSize=10
    // ─────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetFaqs(
        [FromQuery] string? search   = null,
        [FromQuery] bool?   isActive = null,
        [FromQuery] string? category = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 10)
    {
        var result = await _faqService.GetFaqsAsync(search, isActive, category, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    // ─────────────────────────────────────────────────────
    // GET /api/Faq/categories
    // ─────────────────────────────────────────────────────
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var cats = await _faqService.GetCategoriesAsync();
        return Ok(new { success = true, data = cats });
    }

    // ─────────────────────────────────────────────────────
    // GET /api/Faq/{id}
    // ─────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFaq(Guid id)
    {
        var result = await _faqService.GetFaqAsync(id);
        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    // ─────────────────────────────────────────────────────
    // POST /api/Faq
    // ─────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateFaq([FromBody] CreateFaqRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Question))
            return BadRequest(new { success = false, message = "Question không được để trống." });
        if (string.IsNullOrWhiteSpace(request?.Answer))
            return BadRequest(new { success = false, message = "Answer không được để trống." });
        if (string.IsNullOrWhiteSpace(request?.Category))
            return BadRequest(new { success = false, message = "Category không được để trống." });

        var userId = GetCurrentUserId();
        var result = await _faqService.CreateFaqAsync(request, userId);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return StatusCode(201, new { success = true, message = result.Message, data = result.Data });
    }

    // ─────────────────────────────────────────────────────
    // PUT /api/Faq/{id}
    // ─────────────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateFaq(Guid id, [FromBody] UpdateFaqRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Question))
            return BadRequest(new { success = false, message = "Question không được để trống." });
        if (string.IsNullOrWhiteSpace(request?.Answer))
            return BadRequest(new { success = false, message = "Answer không được để trống." });

        var userId = GetCurrentUserId();
        var result = await _faqService.UpdateFaqAsync(id, request, userId);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    // ─────────────────────────────────────────────────────
    // DELETE /api/Faq/{id}   — soft delete (IsDeleted = true)
    // ─────────────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFaq(Guid id)
    {
        var result = await _faqService.DeleteFaqAsync(id);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }


    // ─────────────────────────────────────────────────────
    // PATCH /api/Faq/{id}/toggle-status  — flip IsActive
    // ─────────────────────────────────────────────────────
    [HttpPatch("{id:guid}/toggle-status")]
    public async Task<IActionResult> ToggleFaqStatus(Guid id)
    {
        var userId = GetCurrentUserId();
        var result = await _faqService.ToggleFaqStatusAsync(id, userId);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = new { isActive = result.IsActive } });
    }

    // ─────────────────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────────────────
    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
