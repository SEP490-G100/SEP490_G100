using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Communication;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Authorize]
[Route("api/communication")]
public class CommunicationController : ControllerBase
{
    private readonly CommunicationService _service;

    public CommunicationController(CommunicationService service) => _service = service;

    // GET /api/communication/conversations
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(fail("Khong xac dinh duoc nguoi dung."));

        var result = await _service.GetConversationsAsync(userId.Value);
        return Ok(new { success = true, data = result });
    }

    // GET /api/communication/conversations/{id}/messages
    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.GetMessageHistoryAsync(id, userId.Value, page, pageSize);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return Forbid(); }
    }

    // POST /api/communication/conversations  — Tạo hoặc mở lại conversation 1-1
    [HttpPost("conversations")]
    public async Task<IActionResult> GetOrCreateConversation([FromBody] GetOrCreateConversationDto dto)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.GetOrCreateConversationAsync(userId.Value, dto.OtherUserId);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(fail(ex.Message)); }
    }

    // POST /api/communication/conversations/{id}/messages
    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendMessageDto dto)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(fail("Khong xac dinh duoc nguoi dung."));

        dto.ConversationId = id;

        try
        {
            var result = await _service.SendMessageAsync(dto, userId.Value);
            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(fail(ex.Message)); }
    }

    // DELETE /api/communication/messages/{id}
    [HttpDelete("messages/{id:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            await _service.DeleteMessageAsync(id, userId.Value);
            return Ok(new { success = true, message = "Tin nhan da duoc xoa." });
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // POST /api/communication/messages/{id}/report
    [HttpPost("messages/{id:guid}/report")]
    public async Task<IActionResult> ReportMessage(Guid id, [FromBody] ReportMessageDto dto)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            await _service.ReportMessageAsync(id, userId.Value, dto);
            return Ok(new { success = true, message = "Bao cao da duoc gui. Chung toi se kiem tra trong thoi gian som nhat." });
        }
        catch (KeyNotFoundException ex) { return NotFound(fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(fail(ex.Message)); }
    }

    // PATCH /api/communication/conversations/{id}/status
    [HttpPatch("conversations/{id:guid}/status")]
    public async Task<IActionResult> UpdateConversationStatus(
        Guid id, [FromBody] UpdateConversationStatusDto dto)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return Unauthorized(fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            await _service.UpdateConversationStatusAsync(id, userId.Value, dto);
            return Ok(new { success = true, message = $"Da thuc hien hanh dong '{dto.Action}' thanh cong." });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(fail(ex.Message)); }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static object fail(string message) => new { success = false, message };
}
