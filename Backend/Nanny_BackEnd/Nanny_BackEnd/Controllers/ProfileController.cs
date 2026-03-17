using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;

    public ProfileController(ProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>
    /// Get personal profile information
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPersonalProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var profile = await _profileService.GetPersonalProfileAsync(userId);
            return Ok(new { success = true, data = profile });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update personal information
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdatePersonalInfo([FromBody] UpdatePersonalInfoRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var profile = await _profileService.UpdatePersonalInfoAsync(userId, request);
            return Ok(new { success = true, message = "Cập nhật thông tin cá nhân thành công.", data = profile });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    /// <summary>
    /// Upload user avatar
    /// </summary>
    [HttpPost("upload-avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(Fail("Vui lòng chọn file ảnh."));

        try
        {
            var userId = GetCurrentUserId();
            var avatarUrl = await _profileService.UploadAvatarAsync(userId, file);
            return Ok(new { success = true, message = "Cập nhật ảnh đại diện thành công.", data = avatarUrl });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get all child profiles (Parent only)
    /// </summary>
    [HttpGet("children")]
    public async Task<IActionResult> GetChildProfiles()
    {
        try
        {
            var userId = GetCurrentUserId();
            var children = await _profileService.GetChildProfilesAsync(userId);
            return Ok(new { success = true, data = children });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    /// <summary>
    /// Create a new child profile (Parent only)
    /// </summary>
    [HttpPost("children")]
    public async Task<IActionResult> CreateChildProfile([FromBody] CreateChildProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var child = await _profileService.CreateChildProfileAsync(userId, request);
            return CreatedAtAction(nameof(GetChildProfiles), new { }, new { success = true, message = "Thêm con em thành công.", data = child });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update child profile information (Parent only)
    /// </summary>
    [HttpPut("children/{childId}")]
    public async Task<IActionResult> UpdateChildProfile(Guid childId, [FromBody] UpdateChildProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var child = await _profileService.UpdateChildProfileAsync(userId, childId, request);
            return Ok(new { success = true, message = "Cập nhật thông tin con em thành công.", data = child });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    /// <summary>
    /// Delete child profile (Parent only)
    /// </summary>
    [HttpDelete("children/{childId}")]
    public async Task<IActionResult> DeleteChildProfile(Guid childId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _profileService.DeleteChildProfileAsync(userId, childId);
            return Ok(new { success = true, message = "Xóa con em thành công." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    private static object Fail(string message) => new { success = false, message };
}
