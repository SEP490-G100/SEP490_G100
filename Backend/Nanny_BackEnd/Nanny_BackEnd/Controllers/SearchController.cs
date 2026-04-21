using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly JobService _jobSvc;
    private readonly Sep490NannyDbContext _db;
    private readonly NotificationService _notificationService;
    private readonly SubscriptionService? _subscriptionService;

    public SearchController(
        JobService jobSvc,
        Sep490NannyDbContext db,
        NotificationService notificationService,
        SubscriptionService? subscriptionService = null)
    {
        _jobSvc = jobSvc;
        _db = db;
        _notificationService = notificationService;
        _subscriptionService = subscriptionService;
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
                return StatusCode(403, Fail("Chỉ bảo mẫu mới có quyền lưu bài đăng."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

            if (nannyProfile == null)
                return BadRequest(Fail("Tài khoản không phải bảo mẫu."));

            await _jobSvc.addFavoriteJob(nannyProfile.Id, jobPostingId);
            return Ok(new { success = true, message = "Đã lưu bài đăng." });
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
                return StatusCode(403, Fail("Chỉ bảo mẫu mới có quyền xem bài đăng đã lưu."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

            if (nannyProfile == null)
                return BadRequest(Fail("Tài khoản không phải bảo mẫu."));

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
                return StatusCode(403, Fail("Chỉ bảo mẫu mới có quyền lưu bài đăng."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

            if (nannyProfile == null)
                return BadRequest(Fail("Tài khoản không phải bảo mẫu."));

            var isFavorite = await _jobSvc.toggleFavoriteJob(nannyProfile.Id, jobPostingId, userId);
            return Ok(new
            {
                success = true,
                isFavorite,
                message = isFavorite ? "Đã lưu bài đăng." : "Đã bỏ lưu bài đăng."
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
                return StatusCode(403, Fail("Chỉ bảo mẫu mới có quyền ứng tuyển bài đăng."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .Include(n => n.User)
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

            if (nannyProfile == null)
                return BadRequest(Fail("Tài khoản không phải bảo mẫu."));

            var job = await _db.JobPostings
                .Include(j => j.ParentProfile)
                .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(j => j.Id == jobPostingId && !j.IsDeleted);

            if (job == null)
                return NotFound(Fail("Không tìm thấy bài đăng."));

            if (job.Status != (int)JobPostingStatus.Public ||
                job.ModerationStatus != (int)JobPostingModerationStatus.Approved)
                return BadRequest(Fail("Bài đăng hiện không sẵn sàng để ứng tuyển."));

            if (job.ParentProfile?.UserId == userId)
                return BadRequest(Fail("Bạn không thể ứng tuyển bài đăng của chính mình."));

            var nowUtc = DateTime.UtcNow;
            var existingApplication = await _db.JobApplications
                .FirstOrDefaultAsync(a =>
                    a.JobPostingId == jobPostingId &&
                    a.NannyProfileId == nannyProfile.Id &&
                    !a.IsDeleted);

            var monthlyApplicationLimit = await getMonthlyApplicationLimit(nannyProfile.Id);
            if (monthlyApplicationLimit > 0)
            {
                var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var appliedThisMonth = await _db.JobApplications.CountAsync(a =>
                    a.NannyProfileId == nannyProfile.Id &&
                    !a.IsDeleted &&
                    a.CreatedAt >= startOfMonth);

                var willConsumeMonthlyQuota = existingApplication == null || existingApplication.CreatedAt < startOfMonth;
                if (willConsumeMonthlyQuota && appliedThisMonth >= monthlyApplicationLimit)
                {
                    return BadRequest(Fail(
                        $"Bạn đã đạt giới hạn {monthlyApplicationLimit} lượt ứng tuyển trong tháng này. Vui lòng nâng cấp gói để ứng tuyển thêm."));
                }
            }

            JobApplication application;
            var isReapplied = false;
            if (existingApplication != null)
            {
                if (existingApplication.Status != 3)
                    return Conflict(Fail("Bạn đã gửi đơn ứng tuyển cho bài đăng này."));

                existingApplication.Status = 0;
                existingApplication.WithdrawnAt = null;
                existingApplication.ReviewedAt = null;
                existingApplication.RejectionReason = null;
                // Re-apply in a new month should consume the current month quota.
                if (existingApplication.CreatedAt.Year != nowUtc.Year || existingApplication.CreatedAt.Month != nowUtc.Month)
                    existingApplication.CreatedAt = nowUtc;
                existingApplication.UpdatedAt = nowUtc;
                existingApplication.UpdatedBy = userId;
                application = existingApplication;
                isReapplied = true;
            }
            else
            {
                application = new JobApplication
                {
                    Id = Guid.NewGuid(),
                    JobPostingId = jobPostingId,
                    NannyProfileId = nannyProfile.Id,
                    Status = 0,
                    RejectionReason = null,
                    ReviewedAt = null,
                    WithdrawnAt = null,
                    CreatedAt = nowUtc,
                    CreatedBy = userId,
                    UpdatedAt = null,
                    UpdatedBy = null,
                    IsDeleted = false
                };
                _db.JobApplications.Add(application);
            }

            await _db.SaveChangesAsync();

            var nannyName = $"{nannyProfile.User?.FirstName} {nannyProfile.User?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(nannyName)) nannyName = "Một bảo mẫu";

            var parentUserId = job.ParentProfile?.UserId ?? Guid.Empty;
            if (parentUserId != Guid.Empty)
            {
                await _notificationService.createNotification(
                    parentUserId,
                    "Có bảo mẫu vừa ứng tuyển bài đăng của bạn",
                    $"{nannyName} vừa gửi đơn ứng tuyển cho bài đăng \"{job.Title}\".",
                    NotificationTypes.JobApplicationReceived,
                    job.Id,
                    "JobPosting",
                    userId);
            }

            await _notificationService.createNotification(
                userId,
                "Bạn đã gửi đơn ứng tuyển",
                $"Đơn ứng tuyển của bạn cho bài đăng \"{job.Title}\" đã được gửi. Vui lòng chờ phụ huynh phản hồi.",
                NotificationTypes.JobApplicationSubmitted,
                application.Id,
                "JobApplication",
                userId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    applicationId = application.Id,
                    jobPostingId = jobPostingId,
                    parentUserId = parentUserId,
                    nannyUserId = userId
                },
                message = isReapplied
                    ? "Bạn đã gửi lại đơn ứng tuyển. Vui lòng chờ phụ huynh phản hồi."
                    : "Bạn đã ứng tuyển thành công. Vui lòng chờ phụ huynh phản hồi."
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
                return StatusCode(403, Fail("Chỉ bảo mẫu mới có quyền xem lịch sử ứng tuyển."));

            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 50);

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);
            if (nannyProfile == null)
                return BadRequest(Fail("Tài khoản không phải bảo mẫu."));

            var baseQuery = _db.JobApplications
                .Where(a => a.NannyProfileId == nannyProfile.Id && !a.IsDeleted)
                .Include(a => a.JobPosting)
                    .ThenInclude(j => j.ParentProfile)
                        .ThenInclude(p => p.User)
                .AsQueryable();

            var totalCount = await baseQuery.CountAsync();
            var items = await baseQuery
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = items.Select(a => new
            {
                id = a.Id,
                jobPostingId = a.JobPostingId,
                jobTitle = a.JobPosting?.Title ?? "Tin đăng",
                parentName = $"{a.JobPosting?.ParentProfile?.User?.FirstName} {a.JobPosting?.ParentProfile?.User?.LastName}".Trim(),
                city = a.JobPosting?.City,
                district = a.JobPosting?.District,
                location = a.JobPosting?.Location,
                status = a.Status,
                statusLabel = getApplicationStatusLabel(a.Status),
                canWithdraw = a.Status == 0,
                appliedAt = a.CreatedAt,
                reviewedAt = a.ReviewedAt,
                withdrawnAt = a.WithdrawnAt
            }).ToList();

            return Ok(new
            {
                success = true,
                total = totalCount,
                page,
                pageSize,
                data
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
                return StatusCode(403, Fail("Chỉ bảo mẫu mới có quyền hủy đơn ứng tuyển."));

            var userId = GetCurrentUserId();
            var nannyProfile = await _db.NannyProfiles
                .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);
            if (nannyProfile == null)
                return BadRequest(Fail("Tài khoản không phải bảo mẫu."));

            var application = await _db.JobApplications
                .FirstOrDefaultAsync(a =>
                    a.Id == applicationId &&
                    a.NannyProfileId == nannyProfile.Id &&
                    !a.IsDeleted);

            if (application == null)
                return NotFound(Fail("Không tìm thấy đơn ứng tuyển."));

            if (application.Status != 0)
                return BadRequest(Fail("Chỉ đơn đang chờ duyệt mới có thể hủy."));

            var nowUtc = DateTime.UtcNow;
            application.Status = 3; // Withdrawn
            application.WithdrawnAt = nowUtc;
            application.UpdatedAt = nowUtc;
            application.UpdatedBy = userId;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Bạn đã hủy đơn ứng tuyển."
            });
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
                return StatusCode(403, Fail("Chỉ phụ huynh mới có quyền xem đơn ứng tuyển."));

            if (status.HasValue && (status.Value < 0 || status.Value > 3))
                return BadRequest(Fail("Trạng thái đơn ứng tuyển không hợp lệ."));

            var userId = GetCurrentUserId();
            var parentProfileId = await _db.ParentProfiles
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();

            if (!parentProfileId.HasValue)
                return BadRequest(Fail("Tài khoản không phải phụ huynh."));

            var job = await _db.JobPostings
                .AsNoTracking()
                .FirstOrDefaultAsync(j =>
                    j.Id == jobPostingId &&
                    j.ParentProfileId == parentProfileId.Value &&
                    !j.IsDeleted);

            if (job == null)
                return NotFound(Fail("Không tìm thấy bài đăng hoặc bạn không có quyền truy cập."));

            IQueryable<JobApplication> query = _db.JobApplications
                .Where(a => a.JobPostingId == jobPostingId && !a.IsDeleted)
                .Include(a => a.NannyProfile)
                    .ThenInclude(n => n.User)
                .AsNoTracking();

            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);

            var applications = await query
                .OrderBy(a => a.Status == 0 ? 0 : 1)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();

            var data = applications.Select(a =>
            {
                var nannyUser = a.NannyProfile?.User;
                var nannyName = $"{nannyUser?.FirstName} {nannyUser?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(nannyName))
                    nannyName = "Nanny";

                return new
                {
                    id = a.Id,
                    status = a.Status,
                    statusLabel = getApplicationStatusLabel(a.Status),
                    appliedAt = a.CreatedAt,
                    reviewedAt = a.ReviewedAt,
                    withdrawnAt = a.WithdrawnAt,
                    rejectionReason = a.RejectionReason,
                    canReview = a.Status == 0,
                    nanny = new
                    {
                        profileId = a.NannyProfileId,
                        userId = nannyUser?.Id,
                        fullName = nannyName,
                        avatarUrl = nannyUser?.AvatarUrl,
                        phoneNumber = nannyUser?.PhoneNumber,
                        city = nannyUser?.City,
                        district = nannyUser?.District,
                        yearsOfExperience = a.NannyProfile?.YearsOfExperience,
                        expectedSalaryMin = a.NannyProfile?.ExpectedSalaryMin,
                        expectedSalaryMax = a.NannyProfile?.ExpectedSalaryMax
                    }
                };
            }).ToList();

            return Ok(new
            {
                success = true,
                data = new
                {
                    job = new
                    {
                        id = job.Id,
                        title = job.Title,
                        status = job.Status,
                        moderationStatus = job.ModerationStatus,
                        city = job.City,
                        district = job.District,
                        location = job.Location,
                        createdAt = job.CreatedAt,
                        publishedAt = job.PublishedAt,
                        expiresAt = job.ExpiresAt
                    },
                    totalApplications = data.Count,
                    pendingApplications = data.Count(a => a.status == 0),
                    applications = data
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
    public async Task<IActionResult> ReviewJobApplication(Guid applicationId, [FromBody] ReviewJobApplicationRequest? request)
    {
        try
        {
            if (!User.IsInRole("Parent"))
                return StatusCode(403, Fail("Chỉ phụ huynh mới có quyền duyệt đơn ứng tuyển."));

            if (request == null)
                return BadRequest(Fail("Dữ liệu duyệt đơn không hợp lệ."));

            if (request.Action is not 1 and not 2)
                return BadRequest(Fail("Thao tác không hợp lệ. Dùng 1 (chấp nhận) hoặc 2 (từ chối)."));

            if (request.Action == 2 && string.IsNullOrWhiteSpace(request.RejectionReason))
                return BadRequest(Fail("Vui lòng nhập lý do khi từ chối đơn."));

            var userId = GetCurrentUserId();
            var parentProfileId = await _db.ParentProfiles
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();

            if (!parentProfileId.HasValue)
                return BadRequest(Fail("Tài khoản không phải phụ huynh."));

            var application = await _db.JobApplications
                .Include(a => a.JobPosting)
                .Include(a => a.NannyProfile)
                    .ThenInclude(n => n.User)
                .FirstOrDefaultAsync(a => a.Id == applicationId && !a.IsDeleted);

            if (application == null)
                return NotFound(Fail("Không tìm thấy đơn ứng tuyển."));

            if (application.JobPosting == null || application.JobPosting.IsDeleted ||
                application.JobPosting.ParentProfileId != parentProfileId.Value)
                return NotFound(Fail("Không tìm thấy đơn ứng tuyển hoặc bạn không có quyền xử lý."));

            if (application.Status == 3)
                return BadRequest(Fail("Đơn ứng tuyển này đã được bảo mẫu hủy."));

            if (application.Status is 1 or 2)
                return BadRequest(Fail("Đơn ứng tuyển này đã được xử lý trước đó."));

            if (application.Status != 0)
                return BadRequest(Fail("Chỉ đơn đang chờ duyệt mới có thể xử lý."));

            var nowUtc = DateTime.UtcNow;
            var isApproved = request.Action == 1;

            application.Status = isApproved ? 1 : 2;
            application.ReviewedAt = nowUtc;
            application.WithdrawnAt = null;
            application.RejectionReason = isApproved ? null : request.RejectionReason?.Trim();
            application.UpdatedAt = nowUtc;
            application.UpdatedBy = userId;

            await _db.SaveChangesAsync();

            var nannyUserId = application.NannyProfile?.UserId ?? Guid.Empty;
            if (nannyUserId != Guid.Empty && !isApproved)
            {
                var title = "Đơn ứng tuyển bị từ chối";
                var content = $"Phụ huynh đã từ chối đơn ứng tuyển của bạn cho bài đăng \"{application.JobPosting.Title}\". Lý do: {application.RejectionReason}";

                await _notificationService.createNotification(
                    nannyUserId,
                    title,
                    content,
                    NotificationTypes.JobApplicationRejected,
                    application.Id,
                    "JobApplication",
                    userId);
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    applicationId = application.Id,
                    jobPostingId = application.JobPostingId,
                    status = application.Status,
                    statusLabel = getApplicationStatusLabel(application.Status),
                    reviewedAt = application.ReviewedAt,
                    rejectionReason = application.RejectionReason,
                    parentUserId = userId,
                    nannyUserId = nannyUserId
                },
                message = isApproved
                    ? "Bạn đã chấp nhận đơn ứng tuyển."
                    : "Bạn đã từ chối đơn ứng tuyển."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, Fail(ex.Message));
        }
    }

    public sealed class ReviewJobApplicationRequest
    {
        public int Action { get; set; } // 1 = Accept, 2 = Reject
        public string? RejectionReason { get; set; }
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

    private static string getApplicationStatusLabel(int status)
    {
        return status switch
        {
            0 => "Đang chờ duyệt",
            1 => "Đã được chấp nhận",
            2 => "Đã bị từ chối",
            3 => "Đã hủy",
            _ => "Đang cập nhật"
        };
    }

    private async Task<int> getMonthlyApplicationLimit(Guid nannyProfileId)
    {
        if (_subscriptionService == null)
            return SubscriptionBenefitResponse.FreeNanny.MonthlyApplicationLimit;

        var benefits = await _subscriptionService.getBenefitsForNannyProfile(nannyProfileId);
        return benefits.MonthlyApplicationLimit;
    }

    private static object Fail(string message) => new { success = false, message };
}
