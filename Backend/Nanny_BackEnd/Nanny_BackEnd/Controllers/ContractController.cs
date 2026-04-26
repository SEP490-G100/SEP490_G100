using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Authorize]
[Route("api/contracts")]
public class ContractController : ControllerBase
{
    private readonly IContractService _service;

    public ContractController(IContractService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetMyContracts()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.GetMyContractsAsync(userId.Value);
            return Ok(OkResult(result));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetActiveContractTemplates()
    {
        try
        {
            var result = await _service.GetActiveContractTemplatesAsync();
            return Ok(OkResult(result));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpGet("templates/{templateId:guid}")]
    public async Task<IActionResult> GetTemplatePreview(Guid templateId)
    {
        try
        {
            var result = await _service.GetContractTemplatePreviewAsync(templateId);
            return Ok(OkResult(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpGet("detail")]
    public async Task<IActionResult> GetContractDetail([FromQuery] Guid? contractId, [FromQuery] Guid? hiringRecordId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.GetContractDetailAsync(userId.Value, contractId, hiringRecordId);
            return Ok(OkResult(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpPatch("{contractId:guid}/parent-confirm-info")]
    public async Task<IActionResult> ParentConfirmInfo(Guid contractId, [FromBody] ContractParentFillRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.ParentConfirmInfoAsync(contractId, userId.Value, request);
            return Ok(OkResult(result, "Parent da xac nhan thong tin hop dong."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpPatch("{contractId:guid}/nanny-confirm-info")]
    public async Task<IActionResult> NannyConfirmInfo(Guid contractId, [FromBody] ContractNannyFillRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.NannyConfirmInfoAsync(contractId, userId.Value, request);
            return Ok(OkResult(result, "Nanny da xac nhan thong tin hop dong."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpPost("{contractId:guid}/parent-final-confirm")]
    public async Task<IActionResult> ParentFinalConfirm(Guid contractId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.ParentFinalConfirmAsync(contractId, userId.Value);
            return Ok(OkResult(result, "Parent da xac nhan hoan tat hop dong."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpGet("{contractId:guid}/download")]
    public async Task<IActionResult> DownloadContractPdf(Guid contractId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var (content, fileName) = await _service.DownloadContractPdfAsync(contractId, userId.Value);
            return File(content, "application/pdf", fileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpPatch("{contractId:guid}/storage-file")]
    public async Task<IActionResult> SaveStorageFile(Guid contractId, [FromBody] SaveContractStoragePdfRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng."));

        try
        {
            var result = await _service.SaveContractStoragePdfAsync(contractId, userId.Value, request);
            return Ok(OkResult(result, "Lưu file hợp đồng thành công."));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static object OkResult(object? data, string? message = null) => new { success = true, message, data };
    private static object Fail(string message) => new { success = false, message };
}
