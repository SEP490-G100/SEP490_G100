using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/job-postings")]
public class JobPostingController : ControllerBase
{
    private readonly JobService _jobSvc;
    private readonly Sep490NannyDbContext _db;

    public JobPostingController(JobService jobSvc, Sep490NannyDbContext db)
    {
        _jobSvc = jobSvc;
        _db = db;
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyJobs()
    {
        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tai khoan hien tai khong phai Phu Huynh."));

        var result = await _jobSvc.getMyJobs(parent.Id);
        return Ok(Success(result, result.Count));
    }

    [Authorize]
    [HttpGet("prefill")]
    public async Task<IActionResult> GetPrefill()
    {
        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tai khoan hien tai khong phai Phu Huynh."));

        var result = await _jobSvc.getCreatePrefill(parent.Id);
        return Ok(Success(result));
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> SearchByTitle([FromQuery] string? title)
    {
        var result = await _jobSvc.searchByTitle(title);
        return Ok(Success(result, result.Count));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> getJobpostingById(Guid id)
    {
        try
        {
            var result = await _jobSvc.getDetail(id);
            return Ok(Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> createJobPosting([FromBody] CreateJobPostingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tai khoan hien tai khong phai Phu Huynh."));

        try
        {
            var jobId = await _jobSvc.createJob(parent.Id, request);
            return Ok(new
            {
                success = true,
                message = "Tao tin dang thanh cong. Bai dang dang o trang thai cho duyet.",
                data = new { id = jobId }
            });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> updateJobposting(Guid id, [FromBody] UpdateJobPostingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tai khoan hien tai khong phai Phu Huynh."));

        try
        {
            await _jobSvc.updateJob(id, parent.Id, request);
            return Ok(new
            {
                success = true,
                message = "Cap nhat tin dang thanh cong. Bai dang da duoc dua ve trang thai cho duyet."
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> deleteJobPosting(Guid id)
    {
        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tai khoan hien tai khong phai Phu Huynh."));

        try
        {
            await _jobSvc.deletePost(id, parent.Id);
            return Ok(new { success = true, message = "Xoa tin dang thanh cong." });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize(Roles = "Moderator,Admin")]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ModerateJobPostingRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue) return Unauthorized(Fail("Khong xac dinh duoc moderator hien tai."));

        try
        {
            await _jobSvc.moderateJob(id, moderatorId.Value, true, request.Note);
            return Ok(new { success = true, message = "Da duyet bai dang thanh cong." });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
    }

    [Authorize(Roles = "Moderator,Admin")]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ModerateJobPostingRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue) return Unauthorized(Fail("Khong xac dinh duoc moderator hien tai."));

        try
        {
            await _jobSvc.moderateJob(id, moderatorId.Value, false, request.Note);
            return Ok(new { success = true, message = "Da tu choi bai dang." });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
    }

    private async Task<Models.ParentProfile?> getParent()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return null;

        return await _db.ParentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId.Value && !p.IsDeleted);
    }

    private Guid? getCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    private static object Success(object data, int? total = null) =>
        total.HasValue
            ? new { success = true, total, data }
            : new { success = true, data };

    private static object Fail(string message) => new { success = false, message };

    private static object FailValidation(ModelStateDictionary modelState) =>
        new
        {
            success = false,
            message = "Du lieu khong hop le. Vui long kiem tra lai.",
            errors = modelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
        };
}
