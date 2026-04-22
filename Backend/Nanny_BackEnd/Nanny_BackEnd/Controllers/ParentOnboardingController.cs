using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/onboarding/parent")]
[Authorize]
public class ParentOnboardingController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ParentOnboardingController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateParentProfile([FromBody] UpdateParentProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _profileService.UpdateParentOnboardingProfileAsync(userId, request);
            return Ok(new { success = true, message = "Cập nhật hồ sơ parent thành công." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("child")]
    public async Task<IActionResult> CreateOnboardingChild([FromBody] ParentOnboardingChildRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var createRequest = new CreateChildProfileRequest
            {
                Characteristic = request.Characteristic,
                ChildAgeGroup = (ChildAgeGroup?)request.ChildAgeGroup,
                SpecialNeeds = request.SpecialNeeds,
                Notes = request.Notes
            };

            var child = await _profileService.CreateChildProfileAsync(userId, createRequest);
            return Ok(new { success = true, message = "Tạo hồ sơ con thành công.", data = child });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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
