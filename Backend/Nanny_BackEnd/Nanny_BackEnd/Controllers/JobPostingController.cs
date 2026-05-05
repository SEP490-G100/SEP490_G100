using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Report;
using Nanny_BackEnd.Exceptions;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/job-postings")]
public class JobPostingController : ControllerBase
{
    private readonly IJobService _jobSvc;
    private readonly IReportService _reportService;
    private readonly IProfileService _profileService;

    public JobPostingController(
        IJobService jobSvc,
        IReportService reportService,
        IProfileService profileService)
    {
        _jobSvc = jobSvc;
        _reportService = reportService;
        _profileService = profileService;
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyJobs()
    {
        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ huynh."));

        var result = await _jobSvc.getMyJobs(parent.Id);
        return Ok(Success(result, result.Count));
    }

    [Authorize]
    [HttpGet("prefill")]
    public async Task<IActionResult> GetPrefill()
    {
        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ huynh."));

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
    [HttpPost("{id:guid}/report")]
    public async Task<IActionResult> ReportJobPosting(Guid id, [FromBody] CreateReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(Fail("Không xác định được người dùng hiện tại."));

        try
        {
            var reportId = await _reportService.ReportJobPostingAsync(id, userId.Value, request);
            return Ok(new
            {
                success = true,
                message = "Báo cáo bài đăng đã được gửi thành công.",
                data = new { reportId, jobPostingId = id }
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (RateLimitExceededException ex)
        {
            Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString();
            return StatusCode(429, new
            {
                success = false,
                code = ex.Code,
                message = ex.Message,
                retryAfterSeconds = ex.RetryAfterSeconds,
                cooldownUntilUtc = ex.CooldownUntilUtc
            });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> createJobPosting([FromBody] CreateJobPostingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ huynh."));

        try
        {
            var jobId = await _jobSvc.createJob(parent.Id, request);
            return Ok(new
            {
                success = true,
                message = "Tạo tin đăng thành công. Bài đăng đang ở trạng thái chờ duyệt.",
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
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ huynh."));

        try
        {
            await _jobSvc.updateJob(id, parent.Id, request);
            return Ok(new
            {
                success = true,
                message = "Cập nhật tin đăng thành công. Bài đăng đã được đưa về trạng thái chờ duyệt."
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
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ huynh."));

        try
        {
            await _jobSvc.deletePost(id, parent.Id);
            return Ok(new { success = true, message = "Xóa tin đăng thành công." });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize]
    [HttpPost("{id:guid}/toggle-visibility")]
    public async Task<IActionResult> ToggleVisibility(Guid id)
    {
        var parent = await getParent();
        if (parent is null) return BadRequest(Fail("Tài khoản hiện tại không phải Phụ huynh."));

        try
        {
            await _jobSvc.ToggleJobVisibilityAsync(id, parent.Id);
            return Ok(new { success = true, message = "Đã cập nhật trạng thái hiển thị bài đăng." });
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
        if (!moderatorId.HasValue) return Unauthorized(Fail("Không xác định được điều hành viên hiện tại."));

        try
        {
            await _jobSvc.moderateJob(id, moderatorId.Value, true, request.Note);
            return Ok(new { success = true, message = "Đã duyệt bài đăng thành công." });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize(Roles = "Moderator,Admin")]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ModerateJobPostingRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue) return Unauthorized(Fail("Không xác định được điều hành viên hiện tại."));

        try
        {
            await _jobSvc.moderateJob(id, moderatorId.Value, false, request.Note);
            return Ok(new { success = true, message = "Đã từ chối bài đăng." });
        }
        catch (KeyNotFoundException ex) { return NotFound(Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize(Roles = "Moderator,Admin")]
    [HttpPost("admin/backfill-coordinates")]
    public async Task<IActionResult> BackfillCoordinates([FromBody] BackfillJobCoordinatesRequest? request)
    {
        if (!ModelState.IsValid)
            return BadRequest(FailValidation(ModelState));

        var actorUserId = getCurrentUserId();
        var input = request ?? new BackfillJobCoordinatesRequest();

        var result = await _jobSvc.BackfillJobCoordinatesAsync(
            input,
            actorUserId,
            HttpContext.RequestAborted);

        var message = result.DryRun
            ? "Đã chạy dry-run backfill tọa độ cho job posting."
            : "Đã backfill tọa độ cho job posting.";

        return Ok(new
        {
            success = true,
            message,
            data = result
        });
    }

    // GET /api/Moderator/moderator-view-job-list?status=1&moderationStatus=0&search=lan&page=1&pageSize=10
    [Authorize(Roles = "Moderator")]
    [HttpGet("moderator-view-job-list")]
    public async Task<IActionResult> ModeratorViewJobList(
        [FromQuery] int? status = null,
        [FromQuery] int? moderationStatus = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, totalCount) = await _jobSvc.ModeratorViewJobListAsync(
            status,
            moderationStatus,
            search,
            page,
            pageSize);

        return Ok(new { success = true, data = new { items, totalCount, page, pageSize } });
    }

    // GET /api/Moderator/moderator-view-job-detail/{id}
    [Authorize(Roles = "Moderator")]
    [HttpGet("moderator-view-job-detail/{id:guid}")]
    public async Task<IActionResult> ModeratorViewJobDetail(Guid id)
    {
        try
        {
            var detail = await _jobSvc.ModeratorViewJobDetailAsync(id);
            return Ok(new { success = true, data = detail });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    // PATCH /api/Moderator/moderator-review-job/{id}
    [Authorize(Roles = "Moderator")]
    [HttpPatch("moderator-review-job/{id:guid}")]
    public async Task<IActionResult> ModeratorReviewJob(Guid id, [FromBody] ModerateJobPostingRequest request)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Không xác định được điều hành viên." });

        try
        {
            await _jobSvc.ModeratorReviewJobAsync(id, moderatorId.Value, request);
            return Ok(new { success = true, message = "Xử lý tin đăng thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // PATCH /api/Moderator/moderator-deactivate-job-posting/{id}
    [Authorize(Roles = "Moderator")]
    [HttpPatch("moderator-deactivate-job-posting/{id:guid}")]
    public async Task<IActionResult> ModeratorDeactivateJobPosting(Guid id)
    {
        var moderatorId = getCurrentUserId();
        if (!moderatorId.HasValue)
            return Unauthorized(new { success = false, message = "Không xác định được điều hành viên." });

        try
        {
            await _jobSvc.ModeratorDeactivateJobAsync(id, moderatorId.Value);
            return Ok(new { success = true, message = "Ẩn bài đăng thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private async Task<Models.ParentProfile?> getParent()
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue)
            return null;

        return await _profileService.GetParentProfileByUserIdAsync(userId.Value);
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
            message = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.",
            errors = modelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
        };
}
