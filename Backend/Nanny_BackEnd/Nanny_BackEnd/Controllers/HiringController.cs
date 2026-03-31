using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Authorize]
[Route("api/hiring")]
public class HiringController : ControllerBase
{
    private readonly HiringService _service;

    public HiringController(HiringService service) => _service = service;

    [HttpGet("{jobPostingId:guid}/applicants")]
    public async Task<IActionResult> GetApplicants(Guid jobPostingId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.GetApplicantsAsync(jobPostingId, userId.Value);
            return Ok(OkResult(result));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (Exception ex) { return StatusCode(500, Fail(ex.Message)); }
    }

    [HttpPost("{jobPostingId:guid}/applicants/{jobAppId:guid}/approve")]
    public async Task<IActionResult> ApproveApplicant(Guid jobPostingId, Guid jobAppId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            await _service.ApproveApplicantAsync(jobPostingId, jobAppId, userId.Value);
            return Ok(OkResult("Da dong y ung vien. Vui long vao ho so nanny de chon thue."));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception ex) { return StatusCode(500, Fail(ex.Message)); }
    }

    [HttpGet("{jobPostingId:guid}/applicants/{jobAppId:guid}/nanny-context")]
    public async Task<IActionResult> GetNannyContext(Guid jobPostingId, Guid jobAppId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.GetNannyHireContextAsync(jobPostingId, jobAppId, userId.Value);
            return Ok(OkResult(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (Exception ex) { return StatusCode(500, Fail(ex.Message)); }
    }

    [HttpPost("{jobPostingId:guid}/applicants/{jobAppId:guid}/hire")]
    public async Task<IActionResult> HireApplicant(Guid jobPostingId, Guid jobAppId, [FromBody] ConfirmHiringDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.ConfirmHiringAsync(jobPostingId, jobAppId, userId.Value, dto);
            return Ok(OkResult(result));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception ex) { return StatusCode(500, Fail(ex.Message)); }
    }

    [HttpGet("records/{hiringRecordId:guid}")]
    public async Task<IActionResult> GetHiringOfferDetail(Guid hiringRecordId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.GetHiringOfferDetailAsync(hiringRecordId, userId.Value);
            return Ok(OkResult(result));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (Exception ex) { return StatusCode(500, Fail(ex.Message)); }
    }

    [HttpPost("records/{hiringRecordId:guid}/respond")]
    public async Task<IActionResult> RespondToOffer(Guid hiringRecordId, [FromBody] RespondToOfferDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            await _service.RespondToOfferAsync(hiringRecordId, userId.Value, dto);
            return Ok(OkResult("Phan hoi thanh cong."));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception ex) { return StatusCode(500, Fail(ex.Message)); }
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        try
        {
            var result = await _service.GetActiveTemplatesAsync();
            return Ok(OkResult(result));
        }
        catch (Exception ex) { return StatusCode(500, Fail(ex.Message)); }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static object OkResult(object? data, string? message = null) => new { success = true, message, data };
    private static object Fail(string message) => new { success = false, message };
}
