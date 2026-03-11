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
    public async Task<IActionResult> searchJob([FromQuery] SearchJobRequest request)
    {
        try
        {
            var result = await _jobSvc.findJobs(request);
            return Ok(new { success = true, data = result, total = result.Count });
        }
        catch (Exception ex) { return StatusCode(500, Fail(ex.Message)); }
    }

    //need to login with nanny account to save favorite job
    [Authorize]
    [HttpPost("jobs/favorite/{jobPostingId:guid}")]
    public async Task<IActionResult> saveFavoriteJob(Guid jobPostingId)
    {
        try
        {
            var userId = GetCurrentUserId();

            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

            if (nannyProfile == null)
                return BadRequest(Fail("Tài khoản không phải nanny."));

            await _jobSvc.addFavoriteJob(nannyProfile.Id, jobPostingId);
            return Ok(new { success = true, message = "Đã lưu yêu thích." });
        }
        catch (InvalidOperationException ex) { return Conflict(Fail(ex.Message)); }
        catch (Exception ex)                 { return StatusCode(500, Fail(ex.Message)); }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    private static object Fail(string message) => new { success = false, message };
}
