using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly JobService _jobSvc;
    private readonly Sep490NannyDbContext _db;

    public SearchController(JobService jobSvc, Sep490NannyDbContext db)
    {
        _jobSvc = jobSvc;
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("jobs")]
    public async Task<IActionResult> SearchJob([FromQuery] SearchJobRequest request)
    {
        try
        {
            var currentUserId = TryGetCurrentUserId();
            var canSeeNannyOnlyJobs = User.IsInRole("Nanny");
            Guid? currentNannyProfileId = null;

            if (currentUserId.HasValue && canSeeNannyOnlyJobs)
                currentNannyProfileId = await GetCurrentNannyProfileId(currentUserId.Value);

            var result = await _jobSvc.findJobs(
                request,
                request.NannyLat,
                request.NannyLng,
                currentUserId,
                canSeeNannyOnlyJobs,
                currentNannyProfileId);

            return Ok(new { success = true, data = result, total = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("jobs/favorite/{jobPostingId:guid}")]
    public async Task<IActionResult> SaveFavoriteJob(Guid jobPostingId)
    {
        try
        {
            if (!User.IsInRole("Nanny"))
                return StatusCode(403, Fail("Chi nanny moi co quyen luu bai dang."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

            if (nannyProfile == null)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

            await _jobSvc.addFavoriteJob(nannyProfile.Id, jobPostingId);
            return Ok(new { success = true, message = "Da luu bai dang." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("jobs/favorite/me")]
    public async Task<IActionResult> GetMyFavoriteJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            if (!User.IsInRole("Nanny"))
                return StatusCode(403, Fail("Chi nanny moi co quyen xem bai dang da luu."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

            if (nannyProfile == null)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

            var result = await _jobSvc.getFavoriteJobs(nannyProfile.Id, page, pageSize, userId);
            return Ok(new
            {
                success = true,
                total = result.TotalCount,
                page,
                pageSize,
                data = result.Items
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("jobs/favorite/{jobPostingId:guid}/toggle")]
    public async Task<IActionResult> ToggleFavoriteJob(Guid jobPostingId)
    {
        try
        {
            if (!User.IsInRole("Nanny"))
                return StatusCode(403, Fail("Chi nanny moi co quyen luu bai dang."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

            if (nannyProfile == null)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

            var isFavorite = await _jobSvc.toggleFavoriteJob(nannyProfile.Id, jobPostingId, userId);
            return Ok(new
            {
                success = true,
                isFavorite,
                message = isFavorite ? "Da luu bai dang." : "Da bo luu bai dang."
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

    private Guid? TryGetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    private async Task<Guid?> GetCurrentNannyProfileId(Guid userId)
    {
        return await _db.NannyProfiles
            .Where(n => n.UserId == userId && !n.IsDeleted)
            .Select(n => (Guid?)n.Id)
            .FirstOrDefaultAsync();
    }

    private static object Fail(string message) => new { success = false, message };
}
