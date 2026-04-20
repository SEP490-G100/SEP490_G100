using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/onboarding/parent")]
[Authorize]
public class ParentOnboardingController : ControllerBase
{
    private readonly ProfileService _profileService;
    private readonly ParentRepository _parentRepo;
    private readonly ChildRepository _childRepo;

    public ParentOnboardingController(
        ProfileService profileService,
        ParentRepository parentRepo,
        ChildRepository childRepo)
    {
        _profileService = profileService;
        _parentRepo = parentRepo;
        _childRepo = childRepo;
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateParentProfile([FromBody] UpdateParentProfileRequest request)
    {
        try
        {
            if (request.NumberOfChildren.HasValue && request.NumberOfChildren.Value < 1)
                throw new InvalidOperationException("So luong tre phai lon hon hoac bang 1.");

            var userId = GetCurrentUserId();
            var parentProfile = await _parentRepo.FindByUserIdAsync(userId);
            if (parentProfile == null)
            {
                parentProfile = new Models.ParentProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };
                _parentRepo.Add(parentProfile);
            }

            var createdChildrenCount = (await _childRepo.GetByParentProfileIdAsync(parentProfile.Id)).Count;
            if (request.NumberOfChildren.HasValue && request.NumberOfChildren.Value < createdChildrenCount)
            {
                throw new InvalidOperationException(
                    $"Khong the giam tong so tre xuong {request.NumberOfChildren.Value} vi ban da tao {createdChildrenCount} ho so tre.");
            }

            parentProfile.FamilyDescription = request.FamilyDescription;
            if (request.NumberOfChildren.HasValue)
            {
                parentProfile.NumberOfChildren = request.NumberOfChildren.Value;
            }
            else if (!parentProfile.NumberOfChildren.HasValue && createdChildrenCount > 0)
            {
                parentProfile.NumberOfChildren = createdChildrenCount;
            }
            parentProfile.UpdatedAt = DateTime.UtcNow;
            parentProfile.UpdatedBy = userId;

            await _parentRepo.SaveChangesAsync();

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
                ChildAgeGroup = (Enums.ChildAgeGroup?)request.ChildAgeGroup,
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
