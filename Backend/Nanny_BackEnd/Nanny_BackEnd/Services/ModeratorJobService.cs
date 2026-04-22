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

public class ModeratorJobService : IModeratorJobService
{
    private readonly IModeratorJobRepository _moderatorJobRepository;
    private readonly INotificationService _notificationService;

    public ModeratorJobService(
        IModeratorJobRepository moderatorJobRepository,
        INotificationService notificationService)
    {
        _moderatorJobRepository = moderatorJobRepository;
        _notificationService = notificationService;
    }

    public async Task<(List<SearchJobResponse> Items, int TotalCount)> ModeratorViewJobListAsync(
        int? status,
        int? moderationStatus,
        string? search,
        int page,
        int pageSize)
    {
        var (items, totalCount) = await _moderatorJobRepository.ModeratorViewJobListAsync(
            status,
            moderationStatus,
            search,
            page,
            pageSize);

        var mapped = items.Select(mapToListItem).ToList();
        return (mapped, totalCount);
    }

    public async Task<JobPostingDetailResponse> ModeratorViewJobDetailAsync(Guid jobId)
    {
        var job = await _moderatorJobRepository.ModeratorViewJobDetailAsync(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

        return mapToDetail(job);
    }

    public async Task ModeratorReviewJobAsync(Guid jobId, Guid moderatorUserId, ModerateJobPostingRequest request)
    {
        var job = await _moderatorJobRepository.ModeratorViewJobDetailAsync(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

        var nowUtc = DateTime.UtcNow;
        job.ModerationStatus = request.Action;
        job.ModerationNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        job.ModeratedAt = nowUtc;
        job.ModeratedBy = moderatorUserId;
        job.UpdatedBy = moderatorUserId;

        if (request.Action == (int)JobPostingModerationStatus.Approved)
        {
            job.PublishedAt = job.Status == (int)JobPostingStatus.Public ? nowUtc : null;
            job.ClosedAt = job.Status == (int)JobPostingStatus.Hidden ? nowUtc : null;
        }
        else
        {
            job.PublishedAt = null;
            job.ClosedAt = nowUtc;
        }

        await _moderatorJobRepository.SaveModeratedJobAsync(job);

        var isApproved = request.Action == (int)JobPostingModerationStatus.Approved;
        var title = isApproved ? "Bai dang cua ban da duoc duyet" : "Bai dang cua ban da bi tu choi";
        var content = isApproved
            ? $"Bai dang \"{job.Title}\" da duoc moderator duyet."
            : $"Bai dang \"{job.Title}\" da bi tu choi.{(string.IsNullOrWhiteSpace(job.ModerationNote) ? "" : $" Ly do: {job.ModerationNote}")}";

        var notificationType = isApproved
            ? NotificationTypes.JobPostingApproved
            : NotificationTypes.JobPostingRejected;

        if (job.ParentProfile != null)
        {
            await _notificationService.createNotification(
                job.ParentProfile.UserId,
                title,
                content,
                notificationType,
                job.Id,
                "JobPosting",
                moderatorUserId);
        }
    }

    public async Task ModeratorDeactivateJobAsync(Guid jobId, Guid moderatorUserId)
    {
        var job = await _moderatorJobRepository.ModeratorViewJobDetailAsync(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

        if (job.IsDeleted)
            return;

        var nowUtc = DateTime.UtcNow;
        job.IsDeleted = true;
        job.Status = (int)JobPostingStatus.Hidden;
        job.ClosedAt = nowUtc;
        job.UpdatedAt = nowUtc;
        job.UpdatedBy = moderatorUserId;

        await _moderatorJobRepository.SaveChangesAsync();

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

    private static SearchJobResponse mapToListItem(JobPosting job)
    {
        var entitlement = getJobEntitlement(job);
        var selectedChildren = getSelectedChildrenForJob(job);
        var childResponses = mapChildResponses(selectedChildren);
        var childSnapshot = getChildSnapshot(job);

        return new SearchJobResponse
        {
            Id = job.Id,
            ParentProfileId = job.ParentProfileId,
            ParentUserId = job.ParentProfile?.UserId,
            ChildProfileId = job.ChildProfileId,
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

    private static (string? PlanCode, SubscriptionBenefitResponse Benefits) getJobEntitlement(JobPosting job)
    {
        var nowUtc = DateTime.UtcNow;
        var activeSubscription = job.ParentProfile?.User?.UserSubscriptions?
            .Where(s => !s.IsDeleted && s.Status == 1 && s.EndDate >= nowUtc)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();

        var planName = activeSubscription?.SubscriptionPlan?.Name;
        if (string.Equals(planName, "Pro", StringComparison.OrdinalIgnoreCase))
        {
            return ("PRO", new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 5,
                FeaturedBadge = true,
                SearchPriority = true,
                ListingDurationDays = 60
            });
        }

        if (string.Equals(planName, "Plus", StringComparison.OrdinalIgnoreCase))
        {
            return ("PLUS", new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 3,
                FeaturedBadge = true,
                SearchPriority = false,
                ListingDurationDays = 45
            });
        }

        return (null, SubscriptionBenefitResponse.FreeParent);
    }
}
