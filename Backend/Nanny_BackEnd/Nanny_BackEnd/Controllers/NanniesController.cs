using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NanniesController : ControllerBase
{
    private readonly NannyService _nannyService;
    private readonly ParentRepository _parentRepository;

    public NanniesController(NannyService nannyService, ParentRepository parentRepository)
    {
        _nannyService = nannyService;
        _parentRepository = parentRepository;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetNannies([FromQuery] NannyListRequest request)
    {
        try
        {
            var currentParentProfileId = await TryGetCurrentParentProfileId();
            var result = await _nannyService.GetListAsync(request, currentParentProfileId);
            return Ok(new
            {
                success = true,
                data = result.Items,
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("{nannyProfileId:guid}")]
    public async Task<IActionResult> GetNannyDetail(Guid nannyProfileId)
    {
        try
        {
            var currentParentProfileId = await TryGetCurrentParentProfileId();
            var detail = await _nannyService.GetDetailAsync(nannyProfileId, currentParentProfileId);
            return Ok(new { success = true, data = detail });
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

    [Authorize]
    [HttpGet("favorites/me")]
    public async Task<IActionResult> GetMyFavoriteNannies([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chi parent moi co quyen xem danh sach nanny yeu thich."));

            var userId = GetCurrentUserId();
            var parentProfile = await _parentRepository.FindByUserIdAsync(userId);
            if (parentProfile == null)
                return BadRequest(Fail("Tai khoan khong phai parent."));

            var result = await _nannyService.GetFavoritesAsync(parentProfile.Id, page, pageSize);
            return Ok(new
            {
                success = true,
                data = result.Items,
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("{nannyProfileId:guid}/favorite/toggle")]
    public async Task<IActionResult> ToggleFavorite(Guid nannyProfileId)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chi parent moi co quyen yeu thich nanny."));

            var userId = GetCurrentUserId();
            var parentProfile = await _parentRepository.FindByUserIdAsync(userId);
            if (parentProfile == null)
                return BadRequest(Fail("Tai khoan khong phai parent."));

            var favoriteResult = await _nannyService.ToggleFavoriteAsync(parentProfile.Id, nannyProfileId, userId);
            return Ok(new
            {
                success = true,
                isFavorite = favoriteResult.IsFavorite,
                nannyUserId = favoriteResult.NannyUserId,
                message = favoriteResult.IsFavorite ? "Da yeu thich nanny." : "Da bo yeu thich nanny."
            });
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

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    private async Task<Guid?> TryGetCurrentParentProfileId()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return null;
        if (!User.IsInRole("Parent"))
            return null;

        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return null;

        var parentProfile = await _parentRepository.FindByUserIdAsync(userId);
        return parentProfile?.Id;
    }

    private static object Fail(string message) => new { success = false, message };
}
