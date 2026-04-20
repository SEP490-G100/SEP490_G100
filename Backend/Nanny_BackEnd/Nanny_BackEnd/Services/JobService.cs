using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class JobService
{
    private readonly JobRepository _jobRepo;
    private readonly FavoriteRepository _favoriteRepo;
    private readonly GeocodingService _geo;
    private readonly SubscriptionService _subscriptionService;
    private readonly NotificationService _notificationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobService> _logger;

    public JobService(
        JobRepository jobRepo,
        FavoriteRepository favoriteRepo,
        GeocodingService geo,
        SubscriptionService subscriptionService,
        NotificationService notificationService,
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
        await _jobRepo.hideExpiredPostings();
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
        await _jobRepo.hideExpiredPostings();
        var jobs = await _jobRepo.getListPosting(parentProfileId);
        return jobs.Select(j => mapToListItem(j)).ToList();
    }

    public async Task<(List<SearchJobResponse> Items, int TotalCount)> getFavoriteJobs(
        Guid nannyProfileId,
        int page = 1,
        int pageSize = 20,
        Guid? currentUserId = null)
    {
        await _jobRepo.hideExpiredPostings();
        var (jobs, totalCount) = await _favoriteRepo.getFavoriteJobs(nannyProfileId, page, pageSize);
        var favoriteJobIds = jobs.Select(job => job.Id).ToHashSet();
        var mapped = jobs.Select(job => mapToListItem(job, null, null, currentUserId, favoriteJobIds)).ToList();
        return (mapped, totalCount);
    }

    public async Task<List<SearchJobResponse>> searchByTitle(string? title)
    {
        await _jobRepo.hideExpiredPostings();
        var jobs = await _jobRepo.searchByTitle(title);
        return jobs.Select(j => mapToListItem(j)).ToList();
    }

    public async Task<JobPostingDetailResponse> getDetail(Guid jobId)
    {
        await _jobRepo.hideExpiredPostings();
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");
        return mapToDetail(job);
    }

    public async Task<JobPostingPrefillResponse> getCreatePrefill(Guid parentProfileId)
    {
        var parentProfile = await _jobRepo.getParentProfileSnapshot(parentProfileId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so phu huynh.");
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
        const int freeParentActivePostingLimit = 3;
        var benefits = await _subscriptionService.getBenefitsForParentProfile(parentProfileId);
        var parentProfile = await _jobRepo.getParentProfileSnapshot(parentProfileId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so phu huynh.");

        validateAgeRange(req.MinNannyAge, req.MaxNannyAge);
        var salaryValidationError = SalaryValidationRules.GetFirstError(
            req.SalaryMin,
            req.SalaryMax,
            "Luong toi thieu",
            "Luong toi da");
        if (!string.IsNullOrWhiteSpace(salaryValidationError))
            throw new InvalidOperationException(salaryValidationError);
        var selectedChildren = resolveSelectedChildren(parentProfile, req.ChildProfileId, req.ChildProfileIds, req.NumberOfChildren);
        var primaryChild = selectedChildren.FirstOrDefault();

        var hasActiveParentSubscription = await _subscriptionService.hasActiveParentSubscription(parentProfileId);
        if (!hasActiveParentSubscription)
        {
            var activePostingCount = await _jobRepo.countActiveJobPostings(parentProfileId);
            if (activePostingCount >= freeParentActivePostingLimit)
                throw new InvalidOperationException($"Tai khoan Parent mien phi chi duoc dang toi da {freeParentActivePostingLimit} bai dang. Vui long mua goi de dang them.");

            // Với free user: giới hạn tháng theo FreeParent benefits
            var countThisMonth = await _jobRepo.countJobPostingsInCurrentMonth(parentProfileId);
            var freeMonthlyLimit = Nanny_BackEnd.DTOs.Subscription.SubscriptionBenefitResponse.FreeParent.MonthlyJobPostLimit;
            if (countThisMonth >= freeMonthlyLimit)
                throw new InvalidOperationException($"Ban chi duoc dang toi da {freeMonthlyLimit} bai viet trong 1 thang. Vui long mua goi de tang gioi han.");
        }
        // Paid users: không giới hạn bài đăng tháng ở đây
        // (plan benefits được apply khi tính ExpiresAt và FeaturedBadge)

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException("Phai nhap muc luong toi thieu hoac chon 'Thuong luong'.");
        if (req.SalaryMin.HasValue && req.SalaryMax.HasValue && req.SalaryMin > req.SalaryMax)
            throw new InvalidOperationException("Luong toi thieu khong duoc lon hon luong toi da.");

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
            ExpiresAt = nowUtc.AddDays(benefits.ListingDurationDays),
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
            "Bai dang cua ban dang cho moderator duyet",
            $"Bai dang \"{job.Title}\" da duoc gui thanh cong va hien dang cho moderator duyet.",
            NotificationTypes.JobPostingPending,
            job.Id,
            "JobPosting",
            null);
        await _notificationService.createNotificationForModerators(
            "Co bai dang moi can duyet",
            $"Phu huynh {getDisplayName(parentProfile.User)} vua gui bai dang \"{job.Title}\" de moderator xem xet.",
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
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");
        var parentProfile = await _jobRepo.getParentProfileSnapshot(parentProfileId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so phu huynh.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Ban khong co quyen chinh sua tin dang nay.");

        validateAgeRange(req.MinNannyAge, req.MaxNannyAge);
        var salaryValidationError = SalaryValidationRules.GetFirstError(
            req.SalaryMin,
            req.SalaryMax,
            "Luong toi thieu",
            "Luong toi da");
        if (!string.IsNullOrWhiteSpace(salaryValidationError))
            throw new InvalidOperationException(salaryValidationError);
        var selectedChildren = resolveSelectedChildren(parentProfile, req.ChildProfileId, req.ChildProfileIds, req.NumberOfChildren);
        var primaryChild = selectedChildren.FirstOrDefault();

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException("Phai nhap muc luong toi thieu hoac chon 'Thuong luong'.");
        if (req.SalaryMin.HasValue && req.SalaryMax.HasValue && req.SalaryMin > req.SalaryMax)
            throw new InvalidOperationException("Luong toi thieu khong duoc lon hon luong toi da.");

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
            job.ModerationStatus = (int)JobPostingModerationStatus.Pending;
            job.ModerationNote = null;
            job.ModeratedAt = null;
            job.ModeratedBy = null;
        }

        if (job.ExpiresAt == null || job.ExpiresAt < nowUtc)
            job.ExpiresAt = nowUtc.AddDays(benefits.ListingDurationDays);

        await syncRequirements(job, req.Skills, parentProfile.UserId);
        await syncScheduleRequirements(job, req.ScheduleSlots, parentProfile.UserId);
        await _jobRepo.updateJobPosting(job);

        // Fire-and-forget: cập nhật embedding sau khi sửa job
        _ = EmbedJobInBackgroundAsync(job.Id);
    }

    public async Task moderateJob(Guid jobId, Guid moderatorUserId, bool approved, string? note)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

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

        await _jobRepo.updateJobPosting(job);

        var title = approved
            ? "Bai dang cua ban da duoc duyet"
            : "Bai dang cua ban da bi tu choi";
        var content = approved
            ? $"Bai dang \"{job.Title}\" da duoc moderator duyet va se hien thi tren he thong."
            : $"Bai dang \"{job.Title}\" da bi tu choi.{(string.IsNullOrWhiteSpace(job.ModerationNote) ? "" : $" Ly do: {job.ModerationNote}")}";

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
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Ban khong co quyen xoa tin dang nay.");

        var hasPending = job.JobApplications.Any(a => a.Status == 0);
        if (hasPending)
            throw new InvalidOperationException("Khong the xoa tin dang co don ung tuyen cho xet duyet. Vui long xu ly cac don truoc.");

        await _jobRepo.deleteJobPosting(job);
    }

    public async Task addFavoriteJob(Guid nannyProfileId, Guid jobPostingId)
    {
        var job = await _jobRepo.viewDetailPosting(jobPostingId)
            ?? throw new KeyNotFoundException("Tin dang khong ton tai.");

        var alreadySaved = await _favoriteRepo.isFavoriteJob(nannyProfileId, jobPostingId);
        if (alreadySaved)
            throw new InvalidOperationException("Ban da luu tin nay truoc do roi.");

        await _favoriteRepo.addFavoriteJob(nannyProfileId, jobPostingId);
    }

    public async Task<bool> toggleFavoriteJob(Guid nannyProfileId, Guid jobPostingId, Guid actorUserId)
    {
        var job = await _jobRepo.viewDetailPosting(jobPostingId)
            ?? throw new KeyNotFoundException("Tin dang khong ton tai.");

        if (job.IsDeleted || job.ModerationStatus != (int)JobPostingModerationStatus.Approved)
            throw new InvalidOperationException("Khong the luu tin dang nay.");

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
            throw new InvalidOperationException("Ky nang yeu cau khong hop le. Vui long chon tu danh sach ky nang co san.");

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
            throw new InvalidOperationException("Do tuoi toi thieu khong duoc lon hon do tuoi toi da cua bao mau.");
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
            throw new InvalidOperationException("Vui long tao it nhat 1 Child Profile truoc khi dang bai.");

        var explicitIds = (childProfileIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (explicitIds.Count > 0)
        {
            if (requestedChildren.HasValue && requestedChildren.Value != explicitIds.Count)
                throw new InvalidOperationException(
                    "So luong tre khai bao phai trung voi danh sach Child Profile duoc chon.");

            if (explicitIds.Count > children.Count)
                throw new InvalidOperationException(
                    $"Ban da chon {explicitIds.Count} tre nhung hien chi co {children.Count} Child Profile.");

            var childMap = children.ToDictionary(child => child.Id, child => child);
            if (explicitIds.Any(id => !childMap.ContainsKey(id)))
                throw new InvalidOperationException("Co Child Profile khong thuoc ho so phu huynh hien tai.");

            return explicitIds.Select(id => childMap[id]).ToList();
        }

        var requestedCount = Math.Max(1, requestedChildren ?? 1);
        if (requestedCount > children.Count)
            throw new InvalidOperationException(
                $"Ban da chon {requestedCount} tre nhung hien chi co {children.Count} Child Profile.");

        if (primaryChildId.HasValue && children.All(child => child.Id != primaryChildId.Value))
            throw new InvalidOperationException("Tre duoc chon khong thuoc ho so phu huynh hien tai.");

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
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

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
        }
        else
        {
            job.PublishedAt = null;
            job.ClosedAt = nowUtc;
        }

        await _jobRepo.updateJobPosting(job);

        // Notifications
        var isApproved = moderationStatus == (int)JobPostingModerationStatus.Approved;
        var title = isApproved ? "Bai dang cua ban da duoc duyet" : "Bai dang cua ban da bi tu choi";
        var content = isApproved 
            ? $"Bai dang \"{job.Title}\" da duoc moderator duyet." 
            : $"Bai dang \"{job.Title}\" da bi tu choi.{(string.IsNullOrWhiteSpace(job.ModerationNote) ? "" : $" Ly do: {job.ModerationNote}")}";

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
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

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
                "Bai dang da bi vo hieu hoa",
                $"Bai dang \"{job.Title}\" da bi moderator vo hieu hoa.",
                NotificationTypes.JobPostingRejected,
                job.Id,
                "JobPosting",
                moderatorUserId);
        }
    }

    // Background embedding helper
    private async Task EmbedJobInBackgroundAsync(Guid jobId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var embedService = scope.ServiceProvider.GetRequiredService<EmbeddingService>();
            await embedService.EmbedJobAsync(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background re-embed thất bại cho JobId={JobId}", jobId);
        }
    }
}
