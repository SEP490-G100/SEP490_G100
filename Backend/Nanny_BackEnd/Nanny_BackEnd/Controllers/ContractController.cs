using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Authorize]
[Route("api/contracts")]
public class ContractController : ControllerBase
{
    private readonly ContractService _service;

    public ContractController(ContractService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetMyContracts()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

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

    [HttpGet("{contractId:guid}")]
    public async Task<IActionResult> GetContractDetail(Guid contractId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.GetContractDetailAsync(contractId, userId.Value);
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
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [HttpPatch("{contractId:guid}/draft-parent")]
    public async Task<IActionResult> SaveDraftByParent(Guid contractId, [FromBody] ContractUpsertRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.SaveDraftByParentAsync(contractId, userId.Value, request);
            return Ok(OkResult(result, "Luu ban nhap hop dong thanh cong."));
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

    [HttpPatch("{contractId:guid}/fill-nanny")]
    public async Task<IActionResult> FillByNanny(Guid contractId, [FromBody] ContractUpsertRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.FillByNannyAsync(contractId, userId.Value, request);
            return Ok(OkResult(result, "Nanny submit thong tin hop dong thanh cong."));
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

    [HttpPost("{contractId:guid}/final-confirm-parent")]
    public async Task<IActionResult> FinalConfirmByParent(Guid contractId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Khong xac dinh duoc nguoi dung."));

        try
        {
            var result = await _service.FinalConfirmByParentAsync(contractId, userId.Value);
            return Ok(OkResult(result, "Parent xac nhan hop dong thanh cong."));
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
