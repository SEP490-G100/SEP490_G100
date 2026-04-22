using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IJobService _jobSvc;
    private readonly ISearchService _searchSvc;

    public SearchController(IJobService jobSvc, ISearchService searchSvc)
    {
        _jobSvc    = jobSvc;
        _searchSvc = searchSvc;
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
                currentNannyProfileId = await _searchSvc.GetNannyProfileIdByUserIdAsync(currentUserId.Value);

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
            var nannyProfileId = await _searchSvc.GetNannyProfileIdByUserIdAsync(userId);
            if (nannyProfileId == null)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

            await _jobSvc.addFavoriteJob(nannyProfileId.Value, jobPostingId);
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
            var nannyProfileId = await _searchSvc.GetNannyProfileIdByUserIdAsync(userId);
            if (nannyProfileId == null)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

            var result = await _jobSvc.getFavoriteJobs(nannyProfileId.Value, page, pageSize, userId);
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
            var nannyProfileId = await _searchSvc.GetNannyProfileIdByUserIdAsync(userId);
            if (nannyProfileId == null)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

            var isFavorite = await _jobSvc.toggleFavoriteJob(nannyProfileId.Value, jobPostingId, userId);
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

    [Authorize]
    [HttpPost("jobs/{jobPostingId:guid}/apply")]
    public async Task<IActionResult> ApplyJob(Guid jobPostingId)
    {
        try
        {
            if (!User.IsInRole("Nanny"))
                return StatusCode(403, Fail("Chi nanny moi co quyen ung tuyen bai dang."));

            var userId = GetCurrentUserId();
            var result = await _searchSvc.ApplyToJobAsync(userId, jobPostingId);
            if (!result.IsSuccess)
            {
                return result.Failure switch
                {
                    ApplyToJobFailure.NotNanny
                        or ApplyToJobFailure.JobNotOpen
                        or ApplyToJobFailure.OwnJob
                        => BadRequest(Fail(result.Failure == ApplyToJobFailure.NotNanny
                            ? "Tai khoan khong phai nanny."
                            : (result.Failure == ApplyToJobFailure.JobNotOpen
                                ? "Bai dang hien khong san sang de ung tuyen."
                                : "Ban khong the ung tuyen bai dang cua chinh minh."))),
                    ApplyToJobFailure.NotFound => NotFound(Fail("Khong tim thay bai dang.")),
                    ApplyToJobFailure.AlreadyApplied
                        => Conflict(Fail("Ban da gui don ung tuyen cho bai dang nay.")),
                    ApplyToJobFailure.MonthlyLimit
                        => BadRequest(Fail(result.ErrorDetailMessage!)),
                    _ => BadRequest(Fail("Khong the ung tuyen."))
                };
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    applicationId  = result.ApplicationId,
                    jobPostingId   = result.JobPostingId,
                    parentUserId   = result.ParentUserId,
                    nannyUserId    = result.NannyUserId
                },
                message = result.SuccessMessage
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("jobs/applications/me")]
    public async Task<IActionResult> MyApplications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            if (!User.IsInRole("Nanny"))
                return StatusCode(403, Fail("Chi nanny moi co quyen xem lich su ung tuyen."));

            var userId = GetCurrentUserId();
            var list   = await _searchSvc.GetMyApplicationsAsync(userId, page, pageSize);
            if (list == null)
                return BadRequest(Fail("Tai khoan khong phai nanny."));

            return Ok(new
            {
                success = true,
                total   = list.Total,
                page    = list.Page,
                pageSize = list.PageSize,
                data = list.Items
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("jobs/applications/{applicationId:guid}/withdraw")]
    public async Task<IActionResult> WithdrawApplication(Guid applicationId)
    {
        try
        {
            if (!User.IsInRole("Nanny"))
                return StatusCode(403, Fail("Chi nanny moi co quyen huy don ung tuyen."));

            var userId = GetCurrentUserId();
            var r      = await _searchSvc.WithdrawApplicationAsync(userId, applicationId);
            if (!r.IsSuccess)
            {
                return r.Failure switch
                {
                    WithdrawForNannyFailure.NotNanny => BadRequest(Fail("Tai khoan khong phai nanny.")),
                    WithdrawForNannyFailure.NotFound
                        => NotFound(Fail("Khong tim thay don ung tuyen.")),
                    WithdrawForNannyFailure.NotPending
                        => BadRequest(Fail("Chi don dang cho duyet moi co the huy.")),
                    _ => BadRequest(Fail("Khong the huy don."))
                };
            }

            return Ok(new { success = true, message = "Ban da huy don ung tuyen." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("jobs/{jobPostingId:guid}/applications")]
    public async Task<IActionResult> GetJobApplicationsForParent(Guid jobPostingId, [FromQuery] int? status = null)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chi parent moi co quyen xem request ung tuyen."));

            var userId = GetCurrentUserId();
            var (err, errMsg, data) = await _searchSvc.GetJobApplicationsForParentAsync(userId, jobPostingId, status);
            if (err == GetParentJobApplicationsFailure.InvalidStatusFilter)
                return BadRequest(Fail(errMsg!));
            if (err == GetParentJobApplicationsFailure.NotParent)
                return BadRequest(Fail("Tai khoan khong phai parent."));
            if (err == GetParentJobApplicationsFailure.JobNotFound)
                return NotFound(Fail(errMsg!));

            return Ok(new
            {
                success = true,
                data = new
                {
                    job = new
                    {
                        id = data!.Job.Id,
                        title = data.Job.Title,
                        status = data.Job.Status,
                        moderationStatus = data.Job.ModerationStatus,
                        city = data.Job.City,
                        district = data.Job.District,
                        location = data.Job.Location,
                        createdAt = data.Job.CreatedAt,
                        publishedAt = data.Job.PublishedAt,
                        expiresAt = data.Job.ExpiresAt
                    },
                    totalApplications = data.TotalApplications,
                    pendingApplications = data.PendingApplications,
                    applications = data.Applications.Select(a => new
                    {
                        a.Id,
                        a.Status,
                        a.StatusLabel,
                        appliedAt = a.AppliedAt,
                        a.ReviewedAt,
                        a.WithdrawnAt,
                        a.RejectionReason,
                        a.CanReview,
                        nanny = new
                        {
                            profileId = a.Nanny.ProfileId,
                            userId    = a.Nanny.UserId,
                            fullName  = a.Nanny.FullName,
                            avatarUrl = a.Nanny.AvatarUrl,
                            phoneNumber = a.Nanny.PhoneNumber,
                            city = a.Nanny.City,
                            district = a.Nanny.District,
                            yearsOfExperience = a.Nanny.YearsOfExperience,
                            expectedSalaryMin = a.Nanny.ExpectedSalaryMin,
                            expectedSalaryMax = a.Nanny.ExpectedSalaryMax
                        }
                    })
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("jobs/applications/{applicationId:guid}/review")]
    public async Task<IActionResult> ReviewJobApplication(
        Guid applicationId,
        [FromBody] ReviewJobApplicationRequestDto? request)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chi parent moi co quyen duyet request ung tuyen."));

            var userId = GetCurrentUserId();
            var r      = await _searchSvc.ReviewJobApplicationAsync(userId, applicationId, request);
            if (!r.IsSuccess)
            {
                return r.Failure switch
                {
                    ReviewJobParentFailure.BadInput => BadRequest(Fail(r.ErrorMessage!)),
                    ReviewJobParentFailure.NotParent
                        => BadRequest(Fail("Tai khoan khong phai parent.")),
                    ReviewJobParentFailure.ApplicationNotFound
                        => NotFound(Fail(r.ErrorMessage!)),
                    ReviewJobParentFailure.Forbidden
                        => NotFound(Fail(r.ErrorMessage!)),
                    ReviewJobParentFailure.NannyWithdrawn
                        or ReviewJobParentFailure.AlreadyProcessed
                        or ReviewJobParentFailure.NotPending
                        => BadRequest(Fail(r.ErrorMessage!)),
                    _ => BadRequest(Fail("Khong the xu ly request."))
                };
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    applicationId  = r.ApplicationId,
                    jobPostingId   = r.JobPostingId,
                    status         = r.Status,
                    statusLabel    = r.StatusLabel,
                    reviewedAt     = r.ReviewedAt,
                    rejectionReason = r.RejectionReason,
                    parentUserId   = r.ParentUserId,
                    nannyUserId    = r.NannyUserId
                },
                message = r.SuccessMessage
            });
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

    private static object Fail(string message) => new { success = false, message };
}
