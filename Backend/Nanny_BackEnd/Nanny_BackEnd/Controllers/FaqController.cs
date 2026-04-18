using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.FAQ;
using Nanny_BackEnd.Services;
using System.Security.Claims;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Faq")]
[Authorize(Roles = "Moderator")]
public class FaqController : ControllerBase
{
    private readonly FaqService _faqService;

    public FaqController(FaqService faqService)
    {
        _faqService = faqService;
    }

    // GET /api/Faq?search=...&isActive=true&category=Payment&page=1&pageSize=10
    [HttpGet]
    public async Task<IActionResult> ModeratorViewFaqList(
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _faqService.ModeratorViewFaqListAsync(search, isActive, category, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    // GET /api/Faq/categories
    [HttpGet("categories")]
    public async Task<IActionResult> ModeratorViewFaqCategories()
    {
        var categories = await _faqService.ModeratorViewFaqCategoriesAsync();
        return Ok(new { success = true, data = categories });
    }

    // GET /api/Faq/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ModeratorViewFaqDetail(Guid id)
    {
        var result = await _faqService.ModeratorViewFaqDetailAsync(id);
        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    // POST /api/Faq
    [HttpPost]
    public async Task<IActionResult> ModeratorCreateFaq([FromBody] CreateFaqRequest request)
    {
        var userId = getCurrentUserId();
        var result = await _faqService.ModeratorCreateFaqAsync(request, userId);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return StatusCode(201, new { success = true, message = result.Message, data = result.Data });
    }

    // PUT /api/Faq/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> ModeratorUpdateFaq(Guid id, [FromBody] UpdateFaqRequest request)
    {
        var userId = getCurrentUserId();
        var result = await _faqService.ModeratorUpdateFaqAsync(id, request, userId);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    // DELETE /api/Faq/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> ModeratorDeleteFaq(Guid id)
    {
        var result = await _faqService.ModeratorDeleteFaqAsync(id);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    // PATCH /api/Faq/{id}/toggle-status
    [HttpPatch("{id:guid}/toggle-status")]
    public async Task<IActionResult> ModeratorToggleFaqStatus(Guid id)
    {
        var userId = getCurrentUserId();
        var result = await _faqService.ModeratorToggleFaqStatusAsync(id, userId);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = new { isActive = result.IsActive } });
    }

    private Guid? getCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
