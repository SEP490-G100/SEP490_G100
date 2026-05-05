using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Authorize]
[Route("api/hiring")]
public class HiringController : ControllerBase
{
    private readonly IHiringService _service;

    public HiringController(IHiringService service) => _service = service;

    [HttpGet("records")]
    public async Task<IActionResult> GetMyHiringRecords()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.GetMyHiringRecordsAsync(userId.Value);
            return Ok(OkResult(result));
        }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpGet("{jobPostingId:guid}/applicants")]
    public async Task<IActionResult> GetApplicants(Guid jobPostingId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.GetApplicantsAsync(jobPostingId, userId.Value);
            return Ok(OkResult(result));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy bài đăng hoặc danh sách ứng viên.")); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpPost("{jobPostingId:guid}/applicants/{jobAppId:guid}/approve")]
    public async Task<IActionResult> ApproveApplicant(Guid jobPostingId, Guid jobAppId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            await _service.ApproveApplicantAsync(jobPostingId, jobAppId, userId.Value);
            return Ok(OkResult("Đã đồng ý ứng viên. Vui lòng vào hồ sơ bảo mẫu để chọn thuê."));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy đơn ứng tuyển.")); }
        catch (InvalidOperationException) { return BadRequest(Fail("Ứng viên này đã được xử lý trước đó.")); }
        catch (ArgumentException) { return BadRequest(Fail("Dữ liệu không hợp lệ.")); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpGet("{jobPostingId:guid}/applicants/{jobAppId:guid}/nanny-context")]
    public async Task<IActionResult> GetNannyContext(Guid jobPostingId, Guid jobAppId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.GetNannyHireContextAsync(jobPostingId, jobAppId, userId.Value);
            return Ok(OkResult(result));
        }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy đơn ứng tuyển.")); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpPost("{jobPostingId:guid}/applicants/{jobAppId:guid}/hire")]
    public async Task<IActionResult> HireApplicant(Guid jobPostingId, Guid jobAppId, [FromBody] ConfirmHiringDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.ConfirmHiringAsync(jobPostingId, jobAppId, userId.Value, dto);
            return Ok(OkResult(result));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy dữ liệu thuê.")); }
        catch (InvalidOperationException) { return BadRequest(Fail("Không thể tạo đề nghị thuê cho ứng viên này.")); }
        catch (ArgumentException) { return BadRequest(Fail("Ngày bắt đầu/kết thúc không hợp lệ.")); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpPost("contact-requests/{contactRequestId:guid}/hire")]
    public async Task<IActionResult> HireByContactRequest(Guid contactRequestId, [FromBody] ConfirmHiringDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.ConfirmHiringByContactRequestAsync(contactRequestId, userId.Value, dto);
            return Ok(OkResult(result));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy yêu cầu liên hệ để tạo thuê.")); }
        catch (InvalidOperationException) { return BadRequest(Fail("Không thể tạo đề nghị thuê từ yêu cầu này.")); }
        catch (ArgumentException) { return BadRequest(Fail("Ngày bắt đầu/kết thúc không hợp lệ.")); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpGet("records/{hiringRecordId:guid}")]
    public async Task<IActionResult> GetHiringOfferDetail(Guid hiringRecordId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.GetHiringOfferDetailAsync(hiringRecordId, userId.Value);
            return Ok(OkResult(result));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy đề nghị việc làm.")); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpPost("records/{hiringRecordId:guid}/cancel")]
    public async Task<IActionResult> CancelHiringRequest(Guid hiringRecordId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            await _service.CancelHiringRequestAsync(hiringRecordId, userId.Value);
            return Ok(OkResult("Bạn đã hủy yêu cầu thuê thành công."));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy yêu cầu thuê.")); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpPost("records/{hiringRecordId:guid}/accept")]
    public async Task<IActionResult> AcceptHiringRequest(Guid hiringRecordId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            await _service.RespondHiringRequestAsync(hiringRecordId, userId.Value, isAccepted: true);
            return Ok(OkResult("Bạn đã chấp nhận yêu cầu thuê."));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy yêu cầu thuê.")); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpPost("records/{hiringRecordId:guid}/decline")]
    public async Task<IActionResult> DeclineHiringRequest(Guid hiringRecordId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            await _service.RespondHiringRequestAsync(hiringRecordId, userId.Value, isAccepted: false);
            return Ok(OkResult("Bạn đã từ chối yêu cầu thuê."));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy yêu cầu thuê.")); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpPost("records/{hiringRecordId:guid}/create-contract")]
    public async Task<IActionResult> CreateContract(Guid hiringRecordId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var contractId = await _service.CreateContractForHiringAsync(hiringRecordId, userId.Value);
            return Ok(OkResult(new { contractId }, "Hợp đồng đã được tạo thành công."));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy bản ghi thuê.")); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    [HttpPost("records/{hiringRecordId:guid}/complete")]
    public async Task<IActionResult> CompleteHiring(Guid hiringRecordId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            await _service.CompleteHiringAsync(hiringRecordId, userId.Value);
            return Ok(OkResult("Hợp đồng đã được đánh dấu hoàn thành."));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(Fail("Không tìm thấy hợp đồng thuê.")); }
        catch (InvalidOperationException) { return BadRequest(Fail("Chỉ được hoàn thành khi hợp đồng đã đến hạn kết thúc.")); }
        catch (Exception) { return StatusCode(500, Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại.")); }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static object OkResult(object? data, string? message = null) => new { success = true, message, data };
    private static object Fail(string message) => new { success = false, message };

}
