using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboarding;

    public OnboardingController(IOnboardingService onboarding)
    {
        _onboarding = onboarding;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var userId = GetCurrentUserId();
        var status = await _onboarding.GetStatusAsync(userId);
        return Ok(new { success = true, data = status });
    }

    [HttpGet("skills")]
    public async Task<IActionResult> GetSkills()
    {
        var skills = await _onboarding.GetAllSkillsAsync();
        return Ok(new { success = true, data = skills });
    }

    [HttpPut("nanny/profile")]
    public async Task<IActionResult> UpdateNannyProfile([FromBody] UpdateNannyProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var profile = await _onboarding.UpdateNannyProfileAsync(userId, request);
            return Ok(new { success = true, message = "Cập nhật hồ sơ nanny thành công.", data = profile });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("nanny/skills")]
    public async Task<IActionResult> UpdateNannySkills([FromBody] UpdateNannySkillsRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _onboarding.UpdateNannySkillsAsync(userId, request);
            return Ok(new { success = true, message = "Cập nhật kỹ năng nanny thành công." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("nanny/availability")]
    public async Task<IActionResult> UpdateNannyAvailability([FromBody] UpdateNannyAvailabilityRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _onboarding.UpdateNannyAvailabilityAsync(userId, request);
            return Ok(new { success = true, message = "Cập nhật thời gian làm việc thành công." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }
}

