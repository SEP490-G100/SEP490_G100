using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class JobService : IJobService
{
    private readonly IJobRepository _jobRepo;
    private readonly IFavoriteRepository _favoriteRepo;
    private readonly IGeocodingService _geo;
    private readonly ISubscriptionService _subscriptionService;
    private readonly INotificationService _notificationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobService> _logger;

    public JobService(
        IJobRepository jobRepo,
        IFavoriteRepository favoriteRepo,
        IGeocodingService geo,
        ISubscriptionService subscriptionService,
        INotificationService notificationService,
        IServiceScopeFactory scopeFactory,
        ILogger<JobService> logger)
    {
        _jobRepo = jobRepo;
        _favoriteRepo = favoriteRepo;
        _geo = geo;
        _subscriptionService = subscriptionService;
        _notificationService = notificationService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<List<SearchJobResponse>> findJobs(
        SearchJobRequest filters,
        double? nannyLat = null,
        double? nannyLng = null,
        Guid? currentUserId = null,
        bool canSeeNannyOnlyJobs = false,
        Guid? currentNannyProfileId = null)
    {
        await EnsurePublicJobExpiryStateAsync();
        if (filters.PageSize > 50) filters.PageSize = 50;
        if (filters.Page < 1) filters.Page = 1;

        var jobs = await _jobRepo.searchJobPosting(filters, currentUserId, canSeeNannyOnlyJobs);
        var favoriteJobIds = currentNannyProfileId.HasValue
            ? await _favoriteRepo.getFavoriteJobIds(currentNannyProfileId.Value, jobs.Select(j => j.Id))
            : [];

        return jobs.Select(j => mapToListItem(j, nannyLat, nannyLng, currentUserId, favoriteJobIds)).ToList();
    }

    public async Task<List<SearchJobResponse>> getMyJobs(Guid parentProfileId)
    {
        await EnsurePublicJobExpiryStateAsync();
        var jobs = await _jobRepo.getListPosting(parentProfileId);
        return jobs.Select(j => mapToListItem(j)).ToList();
    }

    public async Task<(List<SearchJobResponse> Items, int TotalCount)> getFavoriteJobs(
        Guid nannyProfileId,
        int page = 1,
        int pageSize = 20,
        Guid? currentUserId = null)
    {
        await EnsurePublicJobExpiryStateAsync();
        var (jobs, totalCount) = await _favoriteRepo.getFavoriteJobs(nannyProfileId, page, pageSize);
        var favoriteJobIds = jobs.Select(job => job.Id).ToHashSet();
        var mapped = jobs.Select(job => mapToListItem(job, null, null, currentUserId, favoriteJobIds)).ToList();
        return (mapped, totalCount);
    }

    public async Task<List<SearchJobResponse>> searchByTitle(string? title)
    {
        await EnsurePublicJobExpiryStateAsync();
        var jobs = await _jobRepo.searchByTitle(title);
        return jobs.Select(j => mapToListItem(j)).ToList();
    }

    public async Task<JobPostingDetailResponse> getDetail(Guid jobId)
    {
        await EnsurePublicJobExpiryStateAsync();
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xóa.");
        return mapToDetail(job);
    }

    public async Task<JobPostingPrefillResponse> getCreatePrefill(Guid parentProfileId)
    {
        var parentProfile = await _jobRepo.getParentProfileSnapshot(parentProfileId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ phụ huynh.");
        var activeChildren = parentProfile.ChildProfiles
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToList();
        var totalChildren = activeChildren.Count;
        var selectedChildren = ChildProfileSnapshotHelper.ResolveChildren(
            parentProfile,
            null,
            totalChildren);
        var selectedChild = selectedChildren.FirstOrDefault();
        var childSnapshot = ChildProfileSnapshotHelper.BuildSnapshot(
            selectedChildren,
            parentProfile.FamilyDescription);

        return new JobPostingPrefillResponse
        {
            NumberOfChildren = totalChildren,
            SelectedChildProfileId = selectedChild?.Id,
            Characteristic = childSnapshot.Characteristic,
            BirthType = childSnapshot.BirthType,
            BirthTypeLabel = childSnapshot.BirthType.HasValue
                ? EnumDisplayHelper.GetDisplayName((ChildAgeGroup)childSnapshot.BirthType.Value)
                : null,
            SpecialNeeds = childSnapshot.SpecialNeeds,
            Skills = [],
            Children = activeChildren
                .Select((child, index) => new JobPostingPrefillChildResponse
                {
                    Id = child.Id,
                    Label = $"Be {index + 1}",
                    Characteristic = child.Characteristic,
                    BirthType = child.ChildAgeGroup,
                    BirthTypeLabel = child.ChildAgeGroup.HasValue
                        ? EnumDisplayHelper.GetDisplayName((ChildAgeGroup)child.ChildAgeGroup.Value)
                        : null,
                    SpecialNeeds = child.SpecialNeeds
                })
                .ToList()
        };
    }

    public async Task<Guid> createJob(Guid parentProfileId, CreateJobPostingRequest req)
    {
        var freeParentPostingLimit = Math.Max(1, SubscriptionBenefitResponse.FreeParent.MonthlyJobPostLimit);
        var freeParentListingDurationDays = Math.Max(1, SubscriptionBenefitResponse.FreeParent.ListingDurationDays);
        var benefits = await _subscriptionService.getBenefitsForParentProfile(parentProfileId);
        var parentProfile = await _jobRepo.getParentProfileSnapshot(parentProfileId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ phụ huynh.");

        validateAgeRange(req.MinNannyAge, req.MaxNannyAge);
        var salaryValidationError = SalaryValidationRules.GetFirstError(
            req.SalaryMin,
            req.SalaryMax,
            "Lương từ",
            "Đến");
        if (!string.IsNullOrWhiteSpace(salaryValidationError))
            throw new InvalidOperationException(salaryValidationError);
        var selectedChildren = resolveSelectedChildren(parentProfile, req.ChildProfileId, req.ChildProfileIds, req.NumberOfChildren);
        var primaryChild = selectedChildren.FirstOrDefault();

        var hasActiveParentSubscription = await _subscriptionService.hasActiveParentSubscription(parentProfileId);
        if (!hasActiveParentSubscription)
        {
            await EnsurePublicJobExpiryStateAsync();
            var activePostingCount = await _jobRepo.countActiveJobPostings(parentProfileId);
            if (activePostingCount >= freeParentPostingLimit)
                throw new InvalidOperationException(
                    $"Tài khoản phụ huynh miễn phí chỉ được duy trì tối đa {freeParentPostingLimit} bài đăng đang hoạt động. " +
                    $"Mỗi bài có thời hạn {freeParentListingDurationDays} ngày. Vui lòng nâng cấp gói nếu muốn đăng thêm.");
        }
        // Paid users: không giới hạn số bài đăng đồng thời ở đây
        // (plan benefits được apply khi tính ExpiresAt và FeaturedBadge)

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException("Phải nhập lương từ hoặc chọn 'Thương lượng'.");
        if (req.SalaryMin.HasValue && req.SalaryMax.HasValue && req.SalaryMin > req.SalaryMax)
            throw new InvalidOperationException("Lương từ không được lớn hơn Đến.");

        var profileSnapshot = buildProfileSnapshot(parentProfile, selectedChildren);

        decimal? lat = null;
        decimal? lng = null;
        var coords = await _geo.geocode(req.Location, req.City, req.District);
        if (coords.HasValue)
        {
            lat = coords.Value.Lat;
            lng = coords.Value.Lng;
        }

        var nowUtc = DateTime.UtcNow;
        var job = new JobPosting
        {
            Id = Guid.NewGuid(),
            ParentProfileId = parentProfileId,
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            JobType = req.JobType,
            SalaryMin = req.SalaryMin,
            SalaryMax = req.SalaryMax,
            SalaryType = 2,
            SalaryNegotiable = req.SalaryNegotiable,
            NumberOfChildren = profileSnapshot.NumberOfChildren,
            ChildProfileId = primaryChild?.Id,
            Location = req.Location?.Trim(),
            City = req.City?.Trim(),
            District = req.District?.Trim(),
            MinNannyAge = req.MinNannyAge,
            MaxNannyAge = req.MaxNannyAge,
            Latitude = lat,
            Longitude = lng,
            ExpiresAt = null,
            Status = req.Status,
            ModerationStatus = (int)JobPostingModerationStatus.Pending,
            ModerationNote = null,
            PublishedAt = null,
            CreatedAt = nowUtc,
            CreatedBy = parentProfile.UserId
        };

        await _jobRepo.createJobPosting(job);
        await syncRequirements(job, req.Skills, parentProfile.UserId);
        await syncScheduleRequirements(job, req.ScheduleSlots, parentProfile.UserId);
        await _notificationService.createNotification(
            parentProfile.UserId,
            "Bài đăng của bạn đang chờ điều hành viên duyệt",
            $"Bài đăng \"{job.Title}\" đã được gửi thành công và hiện đang chờ điều hành viên duyệt.",
            NotificationTypes.JobPostingPending,
            job.Id,
            "JobPosting",
            null);
        await _notificationService.createNotificationForModerators(
            "Có bài đăng mới cần duyệt",
            $"Phụ huynh {getDisplayName(parentProfile.User)} vừa gửi bài đăng \"{job.Title}\" để điều hành viên xem xét.",
            NotificationTypes.JobPostingReviewRequired,
            job.Id,
            "JobPosting",
            parentProfile.UserId);

        // Fire-and-forget: tạo embedding cho job mới
        _ = EmbedJobInBackgroundAsync(job.Id);

        return job.Id;
    }

    public async Task updateJob(Guid jobId, Guid parentProfileId, UpdateJobPostingRequest req)
    {
        var benefits = await _subscriptionService.getBenefitsForParentProfile(parentProfileId);
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xóa.");
        var parentProfile = await _jobRepo.getParentProfileSnapshot(parentProfileId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ phụ huynh.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa tin đăng này.");

        validateAgeRange(req.MinNannyAge, req.MaxNannyAge);
        var salaryValidationError = SalaryValidationRules.GetFirstError(
            req.SalaryMin,
            req.SalaryMax,
            "Lương từ",
            "Đến");
        if (!string.IsNullOrWhiteSpace(salaryValidationError))
            throw new InvalidOperationException(salaryValidationError);
        var selectedChildren = resolveSelectedChildren(parentProfile, req.ChildProfileId, req.ChildProfileIds, req.NumberOfChildren);
        var primaryChild = selectedChildren.FirstOrDefault();

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException("Phải nhập lương từ hoặc chọn 'Thương lượng'.");
        if (req.SalaryMin.HasValue && req.SalaryMax.HasValue && req.SalaryMin > req.SalaryMax)
            throw new InvalidOperationException("Lương từ không được lớn hơn Đến.");

        var addrChanged = req.Location != job.Location
                       || req.City != job.City
                       || req.District != job.District;
        if (addrChanged)
        {
            var coords = await _geo.geocode(req.Location, req.City, req.District);
            if (coords.HasValue)
            {
                job.Latitude = coords.Value.Lat;
                job.Longitude = coords.Value.Lng;
            }
        }

        var profileSnapshot = buildProfileSnapshot(parentProfile, selectedChildren);
        var nowUtc = DateTime.UtcNow;

        job.Title = req.Title.Trim();
        job.Description = req.Description.Trim();
        job.JobType = req.JobType;
        job.SalaryMin = req.SalaryMin;
        job.SalaryMax = req.SalaryMax;
        job.SalaryType = 2;
        job.SalaryNegotiable = req.SalaryNegotiable;
        job.NumberOfChildren = profileSnapshot.NumberOfChildren;
        job.ChildProfileId = primaryChild?.Id;
        job.Location = req.Location?.Trim();
        job.City = req.City?.Trim();
        job.District = req.District?.Trim();
        job.MinNannyAge = req.MinNannyAge;
        job.MaxNannyAge = req.MaxNannyAge;
        job.Status = req.Status;
        if (req.Status == (int)JobPostingStatus.Hidden)
        {
            job.ClosedAt = nowUtc;
        }
        else
        {
            job.PublishedAt = null;
            job.ClosedAt = null;
            job.ExpiresAt = null;
            job.ModerationStatus = (int)JobPostingModerationStatus.Pending;
            job.ModerationNote = null;
            job.ModeratedAt = null;
            job.ModeratedBy = null;
        }

        await syncRequirements(job, req.Skills, parentProfile.UserId);
        await syncScheduleRequirements(job, req.ScheduleSlots, parentProfile.UserId);
        await _jobRepo.updateJobPosting(job);

        // Fire-and-forget: cập nhật embedding sau khi sửa job
        _ = EmbedJobInBackgroundAsync(job.Id);
    }

    public async Task moderateJob(Guid jobId, Guid moderatorUserId, bool approved, string? note)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xóa.");

        ensureJobModerationIsPending(job);
        var nowUtc = DateTime.UtcNow;
        job.ModerationStatus = approved
            ? (int)JobPostingModerationStatus.Approved
            : (int)JobPostingModerationStatus.Rejected;
        job.ModerationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        job.ModeratedAt = nowUtc;
        job.ModeratedBy = moderatorUserId;
        job.UpdatedBy = moderatorUserId;
        job.PublishedAt = approved && job.Status == (int)JobPostingStatus.Public ? nowUtc : null;
        job.ClosedAt = approved
            ? (job.Status == (int)JobPostingStatus.Hidden ? nowUtc : null)
            : nowUtc;

        if (approved && job.Status == (int)JobPostingStatus.Public)
        {
            var benefits = await _subscriptionService.getBenefitsForParentProfile(job.ParentProfileId);
            var listingDurationDays = Math.Max(1, benefits.ListingDurationDays);
            job.ExpiresAt = nowUtc.AddDays(listingDurationDays);
        }
        else
        {
            job.ExpiresAt = null;
        }

        await _jobRepo.updateJobPosting(job);

        var title = approved
            ? "Bài đăng của bạn đã được duyệt"
            : "Bài đăng của bạn đã bị từ chối";
        var content = approved
            ? $"Bài đăng \"{job.Title}\" đã được điều hành viên duyệt và sẽ hiển thị trên hệ thống."
            : $"Bài đăng \"{job.Title}\" đã bị từ chối.{(string.IsNullOrWhiteSpace(job.ModerationNote) ? "" : $" Lý do: {job.ModerationNote}")}";

        await _notificationService.createNotification(
            job.ParentProfile!.UserId,
            title,
            content,
            approved ? NotificationTypes.JobPostingApproved : NotificationTypes.JobPostingRejected,
            job.Id,
            "JobPosting",
            moderatorUserId);
    }

    public async Task deletePost(Guid jobId, Guid parentProfileId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xóa.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Bạn không có quyền xóa tin đăng này.");

        var hasPending = job.JobApplications.Any(a => a.Status == 0);
        if (hasPending)
            throw new InvalidOperationException("Không thể xóa tin đăng có đơn ứng tuyển chờ xét duyệt. Vui lòng xử lý các đơn trước.");

        await _jobRepo.deleteJobPosting(job);
    }

    public async Task addFavoriteJob(Guid nannyProfileId, Guid jobPostingId)
    {
        var job = await _jobRepo.viewDetailPosting(jobPostingId)
            ?? throw new KeyNotFoundException("Tin đăng không tồn tại.");

        var alreadySaved = await _favoriteRepo.isFavoriteJob(nannyProfileId, jobPostingId);
        if (alreadySaved)
            throw new InvalidOperationException("Bạn đã lưu tin này trước đó rồi.");

        await _favoriteRepo.addFavoriteJob(nannyProfileId, jobPostingId);
    }

    public async Task<bool> toggleFavoriteJob(Guid nannyProfileId, Guid jobPostingId, Guid actorUserId)
    {
        var job = await _jobRepo.viewDetailPosting(jobPostingId)
            ?? throw new KeyNotFoundException("Tin đăng không tồn tại.");

        if (job.IsDeleted || job.ModerationStatus != (int)JobPostingModerationStatus.Approved)
            throw new InvalidOperationException("Không thể lưu tin đăng này.");

        return await _favoriteRepo.toggleFavoriteJob(nannyProfileId, jobPostingId, actorUserId);
    }

    private async Task syncRequirements(JobPosting job, IEnumerable<string> skillNames, Guid createdBy)
    {
        var normalized = skillNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        var existingRequirements = job.JobRequirements.Where(r => !r.IsDeleted).ToList();
        if (existingRequirements.Count > 0)
            _jobRepo.removeRequirements(existingRequirements);

        if (normalized.Count == 0)
        {
            await _jobRepo.saveChanges();
            return;
        }

        var existingSkills = await _jobRepo.getSkillsByNames(normalized);
        var existingMap = existingSkills.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var missingSkills = normalized
            .Where(name => !existingMap.ContainsKey(name))
            .ToList();

        if (missingSkills.Count > 0)
            throw new InvalidOperationException("Kỹ năng yêu cầu không hợp lệ. Vui lòng chọn từ danh sách kỹ năng có sẵn.");

        var requirements = normalized.Select(name => new JobRequirement
        {
            Id = Guid.NewGuid(),
            JobPostingId = job.Id,
            SkillId = existingMap[name].Id,
            IsRequired = true,
            MinProficiencyLevel = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsDeleted = false
        }).ToList();

        _jobRepo.addRequirements(requirements);
        await _jobRepo.saveChanges();
    }

    private async Task syncScheduleRequirements(JobPosting job, IEnumerable<JobScheduleSlotRequest> scheduleSlots, Guid createdBy)
    {
        var normalized = scheduleSlots
            .Where(slot => slot.DayOfWeek >= 0 && slot.DayOfWeek <= 6 && slot.TimeSlot >= 0 && slot.TimeSlot <= 3)
            .GroupBy(slot => new { slot.DayOfWeek, slot.TimeSlot })
            .Select(group => group.First())
            .ToList();

        var existing = job.JobScheduleRequirements.Where(r => !r.IsDeleted).ToList();
        if (existing.Count > 0)
            _jobRepo.removeScheduleRequirements(existing);

        if (normalized.Count == 0)
        {
            await _jobRepo.saveChanges();
            return;
        }

        var schedules = normalized.Select(slot => new JobScheduleRequirement
        {
            Id = Guid.NewGuid(),
            JobPostingId = job.Id,
            DayOfWeek = slot.DayOfWeek,
            TimeSlot = slot.TimeSlot,
            IsRequired = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsDeleted = false
        }).ToList();

        _jobRepo.addScheduleRequirements(schedules);
        await _jobRepo.saveChanges();
    }

    private static (int? NumberOfChildren, string? Characteristic, int? BirthType, string? SpecialNeeds) buildProfileSnapshot(
        ParentProfile parentProfile,
        List<ChildProfile> selectedChildren)
    {
        var childSnapshot = ChildProfileSnapshotHelper.BuildSnapshot(selectedChildren, parentProfile.FamilyDescription);
        int? numberOfChildren = selectedChildren.Count;
        if (!numberOfChildren.HasValue || numberOfChildren <= 0)
            numberOfChildren = 1;

        return (
            numberOfChildren,
            childSnapshot.Characteristic,
            childSnapshot.BirthType,
            childSnapshot.SpecialNeeds
        );
    }

    private static void validateAgeRange(int? minNannyAge, int? maxNannyAge)
    {
        if (minNannyAge.HasValue && maxNannyAge.HasValue && minNannyAge > maxNannyAge)
            throw new InvalidOperationException("Độ tuổi tối thiểu không được lớn hơn độ tuổi tối đa của bảo mẫu.");
    }

    private static string getDisplayName(User? user)
    {
        if (user == null) return "Phu huynh";
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private static SearchJobResponse mapToListItem(
        JobPosting job,
        double? nannyLat = null,
        double? nannyLng = null,
        Guid? currentUserId = null,
        HashSet<Guid>? favoriteJobIds = null)
    {
        var entitlement = getJobEntitlement(job);
        var selectedChildren = getSelectedChildrenForJob(job);
        var childResponses = mapChildResponses(selectedChildren);
        var childSnapshot = getChildSnapshot(job);
        double? distance = null;
        if (nannyLat.HasValue && nannyLng.HasValue && job.Latitude.HasValue && job.Longitude.HasValue)
        {
            distance = GeocodingService.CalculateDistanceKm(
                nannyLat.Value,
                nannyLng.Value,
                (double)job.Latitude.Value,
                (double)job.Longitude.Value);
        }

        return new SearchJobResponse
        {
            Id = job.Id,
            ParentProfileId = job.ParentProfileId,
            ParentUserId = job.ParentProfile?.UserId,
            ChildProfileId = job.ChildProfileId,
            IsOwner = currentUserId.HasValue && job.ParentProfile?.UserId == currentUserId.Value,
            IsFavorite = favoriteJobIds?.Contains(job.Id) == true,
            Title = job.Title,
            Description = job.Description,
            ParentName = $"{job.ParentProfile?.User?.FirstName} {job.ParentProfile?.User?.LastName}".Trim(),
            JobType = job.JobType,
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            SalaryNegotiable = job.SalaryNegotiable,
            City = job.City,
            District = job.District,
            Location = job.Location,
            Characteristic = childSnapshot.Characteristic,
            BirthType = childSnapshot.BirthType,
            BirthTypeLabel = getBirthTypeLabel(childSnapshot.BirthType),
            SpecialNeeds = childSnapshot.SpecialNeeds,
            MinNannyAge = job.MinNannyAge,
            MaxNannyAge = job.MaxNannyAge,
            Skills = job.JobRequirements.Where(r => !r.IsDeleted).Select(r => r.Skill.Name).Distinct().ToList(),
            ScheduleSlots = job.JobScheduleRequirements
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.TimeSlot)
                .ThenBy(r => r.DayOfWeek)
                .Select(r => new JobScheduleSlotResponse
                {
                    DayOfWeek = r.DayOfWeek,
                    TimeSlot = r.TimeSlot
                })
                .ToList(),
            Children = childResponses,
            NumberOfChildren = job.NumberOfChildren,
            Latitude = (double?)job.Latitude,
            Longitude = (double?)job.Longitude,
            Status = job.Status,
            ModerationStatus = job.ModerationStatus,
            ModerationNote = job.ModerationNote,
            ModeratedAt = job.ModeratedAt,
            PublishedAt = job.PublishedAt,
            DistanceKm = distance,
            SubscriptionPlanCode = entitlement.PlanCode,
            FeaturedBadge = entitlement.Benefits.FeaturedBadge,
            SearchPriority = entitlement.Benefits.SearchPriority
        };
    }

    private static JobPostingDetailResponse mapToDetail(JobPosting job)
    {
        var entitlement = getJobEntitlement(job);
        var selectedChildren = getSelectedChildrenForJob(job);
        var childResponses = mapChildResponses(selectedChildren);
        var childSnapshot = getChildSnapshot(job);
        return new JobPostingDetailResponse
        {
            Id = job.Id,
            ParentProfileId = job.ParentProfileId,
            ParentUserId = job.ParentProfile?.UserId ?? Guid.Empty,
            ChildProfileId = job.ChildProfileId,
            ParentName = $"{job.ParentProfile?.User?.FirstName} {job.ParentProfile?.User?.LastName}".Trim(),
            Title = job.Title,
            Description = job.Description,
            JobType = job.JobType,
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            SalaryNegotiable = job.SalaryNegotiable,
            NumberOfChildren = job.NumberOfChildren,
            Location = job.Location,
            City = job.City,
            District = job.District,
            Characteristic = childSnapshot.Characteristic,
            BirthType = childSnapshot.BirthType,
            BirthTypeLabel = getBirthTypeLabel(childSnapshot.BirthType),
            SpecialNeeds = childSnapshot.SpecialNeeds,
            MinNannyAge = job.MinNannyAge,
            MaxNannyAge = job.MaxNannyAge,
            Skills = job.JobRequirements.Where(r => !r.IsDeleted).Select(r => r.Skill.Name).Distinct().ToList(),
            ScheduleSlots = job.JobScheduleRequirements
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.TimeSlot)
                .ThenBy(r => r.DayOfWeek)
                .Select(r => new JobScheduleSlotResponse
                {
                    DayOfWeek = r.DayOfWeek,
                    TimeSlot = r.TimeSlot
                })
                .ToList(),
            Children = childResponses,
            Latitude = (double?)job.Latitude,
            Longitude = (double?)job.Longitude,
            Status = job.Status,
            ModerationStatus = job.ModerationStatus,
            ModerationNote = job.ModerationNote,
            ModeratedAt = job.ModeratedAt,
            PublishedAt = job.PublishedAt,
            ExpiresAt = job.ExpiresAt,
            ClosedAt = job.ClosedAt,
            CreatedAt = job.CreatedAt,
            ApplicationCount = job.JobApplications.Count,
            SubscriptionPlanCode = entitlement.PlanCode,
            FeaturedBadge = entitlement.Benefits.FeaturedBadge,
            SearchPriority = entitlement.Benefits.SearchPriority
        };
    }

    private static string? getBirthTypeLabel(int? birthType)
    {
        if (!birthType.HasValue || !Enum.IsDefined(typeof(ChildAgeGroup), (byte)birthType.Value))
            return null;

        return EnumDisplayHelper.GetDisplayName((ChildAgeGroup)birthType.Value);
    }

    private static (string? Characteristic, int? BirthType, string? SpecialNeeds) getChildSnapshot(JobPosting job)
    {
        var selectedChildren = getSelectedChildrenForJob(job);
        var childSnapshot = ChildProfileSnapshotHelper.BuildSnapshot(
            selectedChildren,
            job.ParentProfile?.FamilyDescription);

        return (childSnapshot.Characteristic, childSnapshot.BirthType, childSnapshot.SpecialNeeds);
    }

    private static List<ChildProfile> getSelectedChildrenForJob(JobPosting job)
    {
        return ChildProfileSnapshotHelper.ResolveChildren(
            job.ParentProfile,
            job.ChildProfileId ?? job.ChildProfile?.Id,
            job.NumberOfChildren);
    }

    private static List<JobPostingPrefillChildResponse> mapChildResponses(List<ChildProfile> children)
    {
        return children
            .Select((child, index) => new JobPostingPrefillChildResponse
            {
                Id = child.Id,
                Label = $"Be {index + 1}",
                Characteristic = child.Characteristic,
                BirthType = child.ChildAgeGroup,
                BirthTypeLabel = child.ChildAgeGroup.HasValue
                    ? EnumDisplayHelper.GetDisplayName((ChildAgeGroup)child.ChildAgeGroup.Value)
                    : null,
                SpecialNeeds = child.SpecialNeeds
            })
            .ToList();
    }

    private static List<ChildProfile> resolveSelectedChildren(
        ParentProfile parentProfile,
        Guid? primaryChildId,
        List<Guid>? childProfileIds,
        int? requestedChildren)
    {
        var children = parentProfile.ChildProfiles
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToList();
        if (children.Count == 0)
            throw new InvalidOperationException("Vui lòng tạo ít nhất 1 hồ sơ trẻ trước khi đăng bài.");

        var explicitIds = (childProfileIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (explicitIds.Count > 0)
        {
            if (requestedChildren.HasValue && requestedChildren.Value != explicitIds.Count)
                throw new InvalidOperationException(
                    "Số lượng trẻ cần chăm phải trùng với danh sách hồ sơ trẻ đã chọn.");

            if (explicitIds.Count > children.Count)
                throw new InvalidOperationException(
                    $"Bạn đã chọn {explicitIds.Count} trẻ nhưng hiện chỉ có {children.Count} hồ sơ trẻ.");

            var childMap = children.ToDictionary(child => child.Id, child => child);
            if (explicitIds.Any(id => !childMap.ContainsKey(id)))
                throw new InvalidOperationException("Có hồ sơ trẻ không thuộc hồ sơ phụ huynh hiện tại.");

            return explicitIds.Select(id => childMap[id]).ToList();
        }

        var requestedCount = Math.Max(1, requestedChildren ?? 1);
        if (requestedCount > children.Count)
            throw new InvalidOperationException(
                $"Bạn đã chọn {requestedCount} trẻ nhưng hiện chỉ có {children.Count} hồ sơ trẻ.");

        if (primaryChildId.HasValue && children.All(child => child.Id != primaryChildId.Value))
            throw new InvalidOperationException("Trẻ được chọn không thuộc hồ sơ phụ huynh hiện tại.");

        return ChildProfileSnapshotHelper.ResolveChildren(parentProfile, primaryChildId, requestedCount);
    }

    private static (string? PlanCode, SubscriptionBenefitResponse Benefits) getJobEntitlement(JobPosting job)
    {
        var nowUtc = DateTime.UtcNow;
        var activeSubscription = job.ParentProfile?.User?.UserSubscriptions?
            .Where(s => !s.IsDeleted && s.Status == 1 && s.EndDate >= nowUtc)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();

        var plan = activeSubscription?.SubscriptionPlan;
        var planName = plan?.Name;
        if (string.Equals(planName, "Pro", StringComparison.OrdinalIgnoreCase))
        {
            return ("PRO", new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 5,
                FeaturedBadge = true,
                SearchPriority = true,
                ListingDurationDays = 60,
                CanUseRecommendation = plan!.CanUseRecommendation
            });
        }

        if (string.Equals(planName, "Plus", StringComparison.OrdinalIgnoreCase))
        {
            return ("PLUS", new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 3,
                FeaturedBadge = true,
                SearchPriority = false,
                ListingDurationDays = 45,
                CanUseRecommendation = plan!.CanUseRecommendation
            });
        }

        return (null, SubscriptionBenefitResponse.FreeParent);
    }

    private async Task EnsurePublicJobExpiryStateAsync()
    {
        var missingExpiryJobs = await _jobRepo.GetApprovedPublicJobsMissingExpiryAsync();
        if (missingExpiryJobs.Count > 0)
        {
            var nowUtc = DateTime.UtcNow;
            var benefitCache = new Dictionary<Guid, int>();

            foreach (var job in missingExpiryJobs)
            {
                if (!job.PublishedAt.HasValue)
                    continue;

                if (!benefitCache.TryGetValue(job.ParentProfileId, out var listingDurationDays))
                {
                    var benefits = await _subscriptionService.getBenefitsForParentProfile(job.ParentProfileId);
                    listingDurationDays = Math.Max(1, benefits.ListingDurationDays);
                    benefitCache[job.ParentProfileId] = listingDurationDays;
                }

                job.ExpiresAt = job.PublishedAt.Value.AddDays(listingDurationDays);
                job.UpdatedAt = nowUtc;
            }

            await _jobRepo.SaveChangesAsync();
        }

        await _jobRepo.hideExpiredPostings();
    }

    public async Task<(List<SearchJobResponse> Items, int TotalCount)> GetModeratorJobsAsync(
        int? status,
        int? moderationStatus,
        string? search,
        int page,
        int pageSize)
    {
        var (items, totalCount) = await _jobRepo.GetModeratorJobPostingsAsync(status, moderationStatus, search, page, pageSize);
        var mapped = items.Select(j => mapToListItem(j)).ToList();
        return (mapped, totalCount);
    }

    public async Task ReviewJobAsync(Guid jobId, Guid moderatorUserId, int moderationStatus, string? note)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xóa.");

        ensureJobModerationIsPending(job);
        var nowUtc = DateTime.UtcNow;
        job.ModerationStatus = moderationStatus;
        job.ModerationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        job.ModeratedAt = nowUtc;
        job.ModeratedBy = moderatorUserId;
        job.UpdatedBy = moderatorUserId;

        if (moderationStatus == (int)JobPostingModerationStatus.Approved)
        {
            job.PublishedAt = job.Status == (int)JobPostingStatus.Public ? nowUtc : null;
            job.ClosedAt = job.Status == (int)JobPostingStatus.Hidden ? nowUtc : null;
            if (job.Status == (int)JobPostingStatus.Public)
            {
                var benefits = await _subscriptionService.getBenefitsForParentProfile(job.ParentProfileId);
                var listingDurationDays = Math.Max(1, benefits.ListingDurationDays);
                job.ExpiresAt = nowUtc.AddDays(listingDurationDays);
            }
            else
            {
                job.ExpiresAt = null;
            }
        }
        else
        {
            job.PublishedAt = null;
            job.ClosedAt = nowUtc;
            job.ExpiresAt = null;
        }

        await _jobRepo.updateJobPosting(job);

        // Notifications
        var isApproved = moderationStatus == (int)JobPostingModerationStatus.Approved;
        var title = isApproved ? "Bài đăng của bạn đã được duyệt" : "Bài đăng của bạn đã bị từ chối";
        var content = isApproved 
            ? $"Bài đăng \"{job.Title}\" đã được điều hành viên duyệt." 
            : $"Bài đăng \"{job.Title}\" đã bị từ chối.{(string.IsNullOrWhiteSpace(job.ModerationNote) ? "" : $" Lý do: {job.ModerationNote}")}";

        var notifType = isApproved ? NotificationTypes.JobPostingApproved : NotificationTypes.JobPostingRejected;

        if (job.ParentProfile != null)
        {
            await _notificationService.createNotification(
                job.ParentProfile.UserId,
                title,
                content,
                notifType,
                job.Id,
                "JobPosting",
                moderatorUserId);
        }
    }

    public async Task DeactivateJobAsync(Guid jobId, Guid moderatorUserId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xóa.");

        if (job.IsDeleted)
            return;

        var nowUtc = DateTime.UtcNow;
        job.IsDeleted = true;
        job.Status = (int)JobPostingStatus.Hidden;
        job.ClosedAt = nowUtc;
        job.UpdatedAt = nowUtc;
        job.UpdatedBy = moderatorUserId;

        await _jobRepo.saveChanges();

        if (job.ParentProfile != null)
        {
            await _notificationService.createNotification(
                job.ParentProfile.UserId,
                "Bài đăng đã bị vô hiệu hóa",
                $"Bài đăng \"{job.Title}\" đã bị điều hành viên vô hiệu hóa.",
                NotificationTypes.JobPostingRejected,
                job.Id,
                "JobPosting",
                moderatorUserId);
        }
    }

    public async Task<(List<SearchJobResponse> Items, int TotalCount)> ModeratorViewJobListAsync(
        int? status,
        int? moderationStatus,
        string? search,
        int page,
        int pageSize) =>
        await GetModeratorJobsAsync(status, moderationStatus, search, page, pageSize);

    public async Task<JobPostingDetailResponse> ModeratorViewJobDetailAsync(Guid jobId)
    {
        var job = await _jobRepo.ModeratorViewJobDetailAsync(jobId);
        if (job == null || job.IsDeleted)
            throw new KeyNotFoundException("Không tìm thấy bài đăng công việc.");

        return mapToDetail(job);
    }

    public async Task ModeratorReviewJobAsync(Guid jobId, Guid moderatorUserId, ModerateJobPostingRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.Action is not 1 and not 2)
            throw new InvalidOperationException("Trạng thái kiểm duyệt không hợp lệ.");

        await ReviewJobAsync(jobId, moderatorUserId, request.Action, request.Note);
    }

    public async Task ModeratorDeactivateJobAsync(Guid jobId, Guid moderatorUserId) =>
        await DeactivateJobAsync(jobId, moderatorUserId);

    public async Task<BackfillJobCoordinatesResult> BackfillJobCoordinatesAsync(
        BackfillJobCoordinatesRequest request,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new BackfillJobCoordinatesRequest();

        var maxItems = Math.Clamp(request.MaxItems, 1, 1000);
        var delayMs = Math.Clamp(request.DelayMs, 0, 5000);
        var jobs = await _jobRepo.GetJobsForCoordinateBackfillAsync(request.CreatedBeforeUtc, maxItems);
        var result = new BackfillJobCoordinatesResult
        {
            DryRun = request.DryRun,
            ScannedCount = jobs.Count
        };

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var needsFix = request.ForceGeocode || NeedsCoordinateBackfill(job.Latitude, job.Longitude);
            if (!needsFix)
                continue;

            result.CandidateCount++;
            var oldLat = job.Latitude;
            var oldLng = job.Longitude;
            var isSwapped = IsLikelySwapped(oldLat, oldLng);
            var hasAddress = HasAddressInput(job.Location, job.City, job.District);

            var item = new BackfillJobCoordinateItemResult
            {
                JobId = job.Id,
                Title = job.Title,
                OldLatitude = oldLat,
                OldLongitude = oldLng
            };

            if (request.DryRun)
            {
                if (!request.ForceGeocode && isSwapped)
                {
                    var (newLat, newLng) = SwapCoordinatePair(oldLat!.Value, oldLng!.Value);
                    item.NewLatitude = newLat;
                    item.NewLongitude = newLng;
                    item.Action = "would_swap";
                    item.Message = "Tọa độ có dấu hiệu bị đảo lat/lng.";
                }
                else if (hasAddress)
                {
                    item.Action = "would_geocode";
                    item.Message = "Sẽ gọi geocoding từ địa chỉ để cập nhật tọa độ.";
                }
                else if (isSwapped)
                {
                    var (newLat, newLng) = SwapCoordinatePair(oldLat!.Value, oldLng!.Value);
                    item.NewLatitude = newLat;
                    item.NewLongitude = newLng;
                    item.Action = "would_swap_fallback";
                    item.Message = "Không đủ thông tin địa chỉ, sẽ sửa bằng cách đảo lat/lng.";
                }
                else
                {
                    item.Action = "would_skip";
                    item.Message = "Không đủ địa chỉ để geocode.";
                    result.FailedCount++;
                }

                result.Items.Add(item);
                continue;
            }

            var updated = false;
            var usedGeocode = false;

            if (!request.ForceGeocode && isSwapped)
            {
                var (newLat, newLng) = SwapCoordinatePair(oldLat!.Value, oldLng!.Value);
                job.Latitude = newLat;
                job.Longitude = newLng;
                item.NewLatitude = newLat;
                item.NewLongitude = newLng;
                item.Action = "swapped";
                item.Message = "Đã sửa bằng cách đảo lat/lng.";
                updated = true;
                result.SwappedCount++;
            }
            else if (hasAddress)
            {
                usedGeocode = true;
                var coords = await _geo.geocode(job.Location, job.City, job.District);
                if (coords.HasValue)
                {
                    job.Latitude = coords.Value.Lat;
                    job.Longitude = coords.Value.Lng;
                    item.NewLatitude = coords.Value.Lat;
                    item.NewLongitude = coords.Value.Lng;
                    item.Action = "geocoded";
                    item.Message = "Đã cập nhật tọa độ từ geocoding.";
                    updated = true;
                    result.GeocodedCount++;
                }
            }

            if (!updated && isSwapped)
            {
                var (newLat, newLng) = SwapCoordinatePair(oldLat!.Value, oldLng!.Value);
                job.Latitude = newLat;
                job.Longitude = newLng;
                item.NewLatitude = newLat;
                item.NewLongitude = newLng;
                item.Action = "swapped_fallback";
                item.Message = "Geocoding không trả về kết quả, đã fallback đảo lat/lng.";
                updated = true;
                result.SwappedCount++;
            }

            if (updated)
            {
                job.UpdatedAt = DateTime.UtcNow;
                if (actorUserId.HasValue)
                    job.UpdatedBy = actorUserId.Value;
                result.UpdatedCount++;
            }
            else
            {
                item.Action = "skipped";
                item.Message = hasAddress
                    ? "Geocoding không trả về kết quả hợp lệ."
                    : "Không đủ địa chỉ để geocode.";
                result.FailedCount++;
            }

            result.Items.Add(item);

            if (usedGeocode && delayMs > 0)
                await Task.Delay(delayMs, cancellationToken);
        }

        if (!request.DryRun && result.UpdatedCount > 0)
            await _jobRepo.saveChanges();

        return result;
    }

    private static void ensureJobModerationIsPending(JobPosting job)
    {
        if (job.ModerationStatus != (int)JobPostingModerationStatus.Pending)
            throw new InvalidOperationException("Tin đăng đã được kiểm duyệt trước đó, không thể xử lý lại.");
    }

    // Background embedding helper
    private async Task EmbedJobInBackgroundAsync(Guid jobId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var embedService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            await embedService.EmbedJobAsync(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background re-embed thất bại cho JobId={JobId}", jobId);
        }
    }

    private static bool NeedsCoordinateBackfill(decimal? lat, decimal? lng)
    {
        if (!lat.HasValue || !lng.HasValue)
            return true;

        if (IsLikelySwapped(lat, lng))
            return true;

        return !IsWithinVietnamBounds(lat.Value, lng.Value);
    }

    private static bool IsLikelySwapped(decimal? lat, decimal? lng)
    {
        if (!lat.HasValue || !lng.HasValue)
            return false;

        return lat.Value is >= 102m and <= 110m &&
               lng.Value is >= 8m and <= 24m;
    }

    private static bool IsWithinVietnamBounds(decimal lat, decimal lng) =>
        lat is >= 8m and <= 24m &&
        lng is >= 102m and <= 110m;

    private static bool HasAddressInput(string? location, string? city, string? district) =>
        !string.IsNullOrWhiteSpace(location) ||
        !string.IsNullOrWhiteSpace(city) ||
        !string.IsNullOrWhiteSpace(district);

    private static (decimal Lat, decimal Lng) SwapCoordinatePair(decimal lat, decimal lng) => (lng, lat);
}
