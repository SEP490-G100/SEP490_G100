using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/Admin")]
[Authorize(Roles = "Admin")]
public class AdminSubcriptionPlanController : ControllerBase
{
    private readonly IAdminSubcriptionPlanService _adminSubcriptionPlanService;

    public AdminSubcriptionPlanController(IAdminSubcriptionPlanService adminSubcriptionPlanService)
    {
        _adminSubcriptionPlanService = adminSubcriptionPlanService;
    }

    [HttpGet("admin-view-subscription-plan-list")]
    public async Task<IActionResult> AdminViewSubscriptionPlanList(
        [FromQuery] string? search = null,
        [FromQuery] string? targetRole = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 3)
    {
        var result = await _adminSubcriptionPlanService.AdminViewSubscriptionPlanListAsync(
            search,
            targetRole,
            isActive,
            page,
            pageSize);

        return Ok(new { success = true, data = result });
    }

    [HttpGet("admin-view-subscription-plan-detail/{id:guid}")]
    public async Task<IActionResult> AdminViewSubscriptionPlanDetail(Guid id)
    {
        var plan = await _adminSubcriptionPlanService.AdminViewSubscriptionPlanDetailAsync(id);
        return plan == null
            ? NotFound(new { success = false, message = "Khong tim thay goi subscription." })
            : Ok(new { success = true, data = plan });
    }

    [HttpPost("admin-create-subscription-plan")]
    public async Task<IActionResult> AdminCreateSubscriptionPlan([FromBody] AdminSubscriptionPlanUpsertRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var plan = await _adminSubcriptionPlanService.AdminCreateSubscriptionPlanAsync(adminUserId.Value, request);
            return Ok(new { success = true, message = "Tao goi subscription thanh cong.", data = plan });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("admin-update-subscription-plan/{id:guid}")]
    public async Task<IActionResult> AdminUpdateSubscriptionPlan(Guid id, [FromBody] AdminSubscriptionPlanUpsertRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        try
        {
            var plan = await _adminSubcriptionPlanService.AdminUpdateSubscriptionPlanAsync(id, adminUserId.Value, request);
            return Ok(new { success = true, message = "Cap nhat goi subscription thanh cong.", data = plan });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("admin-update-subscription-plan-status/{id:guid}")]
    public async Task<IActionResult> AdminUpdateSubscriptionPlanStatus(
        Guid id,
        [FromBody] AdminSubscriptionPlanStatusRequest? request,
        [FromQuery] bool? isActive)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized(new { success = false, message = "Khong xac dinh duoc admin hien tai." });

        var targetIsActive = isActive ?? request?.IsActive;
        if (!targetIsActive.HasValue)
            return BadRequest(new { success = false, message = "Thieu trang thai kich hoat cua goi subscription." });

        try
        {
            await _adminSubcriptionPlanService.AdminUpdateSubscriptionPlanStatusAsync(
                id,
                adminUserId.Value,
                targetIsActive.Value);

            return Ok(new
            {
                success = true,
                message = targetIsActive.Value
                    ? "Da kich hoat goi subscription."
                    : "Da vo hieu hoa goi subscription."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
