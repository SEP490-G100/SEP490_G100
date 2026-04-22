using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPersonalProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var profile = await _profileService.GetPersonalProfileAsync(userId);
            return Ok(new { success = true, data = profile });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(Fail("Phien dang nhap khong hop le."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    [HttpGet("public/{userId:guid}")]
    public async Task<IActionResult> GetPublicProfile(Guid userId)
    {
        try
        {
            var requesterUserId = GetCurrentUserId();
            var profile = await _profileService.GetPublicProfileAsync(requesterUserId, userId);
            return Ok(new { success = true, data = profile });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(Fail("Phien dang nhap khong hop le."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdatePersonalInfo([FromBody] UpdatePersonalInfoRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var profile = await _profileService.UpdatePersonalInfoAsync(userId, request);
            return Ok(new { success = true, message = "Cap nhat thong tin ca nhan thanh cong.", data = profile });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(Fail("Phien dang nhap khong hop le."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    [HttpPost("upload-avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(Fail("Vui long chon file anh."));

        try
        {
            var userId = GetCurrentUserId();
            var avatarUrl = await _profileService.UploadAvatarAsync(userId, file);
            return Ok(new { success = true, message = "Cap nhat anh dai dien thanh cong.", data = avatarUrl });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(Fail("Phien dang nhap khong hop le."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    [NonAction]
    public async Task<IActionResult> AddCertificate([FromBody] CreateNannyCertificateRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _profileService.AddNannyCertificateAsync(userId, request);
            return Ok(new { success = true, message = "Them chung chi thanh cong." });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(Fail("Phien dang nhap khong hop le."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    [HttpGet("children")]
    public async Task<IActionResult> GetChildProfiles()
    {
        try
        {
            var userId = GetCurrentUserId();
            var children = await _profileService.GetChildProfilesAsync(userId);
            return Ok(new { success = true, data = children });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    [HttpPost("children")]
    public async Task<IActionResult> CreateChildProfile([FromBody] CreateChildProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var child = await _profileService.CreateChildProfileAsync(userId, request);
            return CreatedAtAction(nameof(GetChildProfiles), new { }, new { success = true, message = "Them con em thanh cong.", data = child });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    [HttpPut("children/{childId}")]
    public async Task<IActionResult> UpdateChildProfile(Guid childId, [FromBody] UpdateChildProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var child = await _profileService.UpdateChildProfileAsync(userId, childId, request);
            return Ok(new { success = true, message = "Cap nhat thong tin con em thanh cong.", data = child });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Fail(ex.Message));
        }
    }

    [HttpDelete("children/{childId}")]
    public async Task<IActionResult> DeleteChildProfile(Guid childId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _profileService.DeleteChildProfileAsync(userId, childId);
            return Ok(new { success = true, message = "Xoa con em thanh cong." });
        }
        catch (UnauthorizedAccessException)
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

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Token khong hop le.");

        return userId;
    }

    private static object Fail(string message) => new { success = false, message };
}
