using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Search;
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
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ Huynh."));

        var result = await _jobSvc.getMyJobs(parent.Id);
        return Ok(Success(result, result.Count));
    }


    // GET /api/job-postings?title=c%E1%BA%A7n+b%E1%BA%A3o+m%E1%BA%ABu
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> SearchByTitle([FromQuery] string? title)
    {
        var result = await _jobSvc.searchByTitle(title);
        return Ok(Success(result, result.Count));
    }


    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobPostingRequest request)
    {

        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ Huynh."));

        try
        {
            var jobId = await _jobSvc.createJob(parent.Id, request);
            return Ok(new { success = true, message = "Tạo tin đăng thành công.", data = new { id = jobId } });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateJobPostingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ Huynh."));

        try
        {
            await _jobSvc.updateJob(id, parent.Id, request);
            return Ok(new { success = true, message = "Cập nhật tin đăng thành công." });
        }
        catch (KeyNotFoundException ex)         { return NotFound(Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex)  { return StatusCode(403, Fail(ex.Message)); }
        catch (InvalidOperationException ex)    { return BadRequest(Fail(ex.Message)); }
    }



    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ Huynh."));

        try
        {
            await _jobSvc.deletePost(id, parent.Id);
            return Ok(new { success = true, message = "Xoá tin đăng thành công." });
        }
        catch (KeyNotFoundException ex)        { return NotFound(Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, Fail(ex.Message)); }
        catch (InvalidOperationException ex)   { return BadRequest(Fail(ex.Message)); }
    }


    private async Task<Models.ParentProfile?> getParent()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return null;

        return await _db.ParentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
    }

    private static object Success(object data, int? total = null) =>
        total.HasValue
            ? new { success = true, total, data }
            : new { success = true, data };

    private static object Fail(string message) =>
        new { success = false, message };


    private static object FailValidation(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState) =>
        new
        {
            success = false,
            message = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.",
            errors = modelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                )
        };
}
