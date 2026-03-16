using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class JobService
{
    private readonly JobRepository _jobRepo;
    private readonly FavoriteRepository _favoriteRepo;
    private readonly GeocodingService _geo;
    private readonly SubscriptionService _subscriptionService;

    public JobService(
        JobRepository jobRepo,
        FavoriteRepository favoriteRepo,
        GeocodingService geo,
        SubscriptionService subscriptionService)
    {
        _jobRepo = jobRepo;
        _favoriteRepo = favoriteRepo;
        _geo = geo;
        _subscriptionService = subscriptionService;
    }

    public async Task<List<SearchJobResponse>> findJobs(
        SearchJobRequest filters,
        double? nannyLat = null,
        double? nannyLng = null,
        Guid? currentUserId = null,
        bool canSeeNannyOnlyJobs = false)
    {
        if (filters.PageSize > 50) filters.PageSize = 50;
        if (filters.Page < 1) filters.Page = 1;

        var jobs = await _jobRepo.searchJobPosting(filters, currentUserId, canSeeNannyOnlyJobs);
        return jobs.Select(j => mapToListItem(j, nannyLat, nannyLng, currentUserId)).ToList();
    }

    public async Task<List<SearchJobResponse>> getMyJobs(Guid parentProfileId)
    {
        var jobs = await _jobRepo.getListPosting(parentProfileId);
        return jobs.Select(j => mapToListItem(j)).ToList();
    }

    public async Task<List<SearchJobResponse>> searchByTitle(string? title)
    {
        var jobs = await _jobRepo.searchByTitle(title);
        return jobs.Select(j => mapToListItem(j)).ToList();
    }

    public async Task<JobPostingDetailResponse> getDetail(Guid jobId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");
        return mapToDetail(job);
    }

    public async Task<Guid> createJob(Guid parentProfileId, CreateJobPostingRequest req)
    {
        var benefits = await _subscriptionService.getBenefitsForParentProfile(parentProfileId);

        var countThisMonth = await _jobRepo.countJobPostingsInCurrentMonth(parentProfileId);
        if (countThisMonth >= benefits.MonthlyJobPostLimit)
            throw new InvalidOperationException($"Ban chi duoc dang toi da {benefits.MonthlyJobPostLimit} bai viet trong 1 thang.");

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException("Phai nhap muc luong toi thieu hoac chon 'Thuong luong'.");

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
            SalaryType = 2,
            SalaryNegotiable = req.SalaryNegotiable,
            NumberOfChildren = req.NumberOfChildren,
            Location = req.Location?.Trim(),
            City = req.City?.Trim(),
            District = req.District?.Trim(),
            Latitude = lat,
            Longitude = lng,
            ExpiresAt = nowUtc.AddDays(benefits.ListingDurationDays),
            Status = req.Status,
            ModerationStatus = 2,
            PublishedAt = req.Status == 1 ? nowUtc : null,
            CreatedAt = nowUtc,
            CreatedBy = parentProfileId
        };

        await _jobRepo.createJobPosting(job);
        return job.Id;
    }

    public async Task updateJob(Guid jobId, Guid parentProfileId, UpdateJobPostingRequest req)
    {
        var benefits = await _subscriptionService.getBenefitsForParentProfile(parentProfileId);
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Ban khong co quyen chinh sua tin dang nay.");

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException("Phai nhap muc luong toi thieu hoac chon 'Thuong luong'.");

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

        var wasInactive = job.Status != 1;
        var nowUtc = DateTime.UtcNow;

        job.Title = req.Title.Trim();
        job.Description = req.Description.Trim();
        job.JobType = req.JobType;
        job.SalaryMin = req.SalaryMin;
        job.SalaryType = 2;
        job.SalaryNegotiable = req.SalaryNegotiable;
        job.NumberOfChildren = req.NumberOfChildren;
        job.Location = req.Location?.Trim();
        job.City = req.City?.Trim();
        job.District = req.District?.Trim();
        job.Status = req.Status;
        job.PublishedAt = req.Status == 1 ? (job.PublishedAt ?? nowUtc) : null;
        job.ClosedAt = req.Status == 0 ? nowUtc : null;

        if (req.Status == 1 && (wasInactive || job.ExpiresAt == null || job.ExpiresAt < nowUtc))
            job.ExpiresAt = nowUtc.AddDays(benefits.ListingDurationDays);

        await _jobRepo.updateJobPosting(job);
    }

    public async Task<string> togglePublish(Guid jobId, Guid parentProfileId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Ban khong co quyen thay doi trang thai tin dang nay.");

        await _jobRepo.togglePublishPosting(job);
        return job.Status == 1 ? "Tin dang da duoc publish thanh cong." : "Tin dang da duoc unpublish.";
    }

    public async Task deletePost(Guid jobId, Guid parentProfileId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Ban khong co quyen xoa tin dang nay.");

        var hasPending = job.JobApplications.Any(a => a.Status == 0);
        if (hasPending)
        {
            throw new InvalidOperationException(
                "Khong the xoa tin dang co don ung tuyen cho xet duyet. Vui long xu ly cac don truoc.");
        }

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

    private static SearchJobResponse mapToListItem(
        JobPosting job,
        double? nannyLat = null,
        double? nannyLng = null,
        Guid? currentUserId = null)
    {
        var entitlement = getJobEntitlement(job);
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
            IsOwner = currentUserId.HasValue && job.ParentProfile?.UserId == currentUserId.Value,
            Title = job.Title,
            Description = job.Description,
            ParentName = $"{job.ParentProfile?.User?.FirstName} {job.ParentProfile?.User?.LastName}".Trim(),
            JobType = job.JobType,
            SalaryMin = job.SalaryMin,
            SalaryNegotiable = job.SalaryNegotiable,
            City = job.City,
            District = job.District,
            Location = job.Location,
            NumberOfChildren = job.NumberOfChildren,
            Latitude = (double?)job.Latitude,
            Longitude = (double?)job.Longitude,
            Status = job.Status,
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
        return new JobPostingDetailResponse
        {
            Id = job.Id,
            ParentProfileId = job.ParentProfileId,
            ParentName = $"{job.ParentProfile?.User?.FirstName} {job.ParentProfile?.User?.LastName}".Trim(),
            Title = job.Title,
            Description = job.Description,
            JobType = job.JobType,
            SalaryMin = job.SalaryMin,
            SalaryNegotiable = job.SalaryNegotiable,
            NumberOfChildren = job.NumberOfChildren,
            Location = job.Location,
            City = job.City,
            District = job.District,
            Latitude = (double?)job.Latitude,
            Longitude = (double?)job.Longitude,
            Status = job.Status,
            ModerationStatus = job.ModerationStatus,
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
                MonthlyJobPostLimit = 30,
                FeaturedBadge = true,
                SearchPriority = true,
                ListingDurationDays = 60
            });
        }

        if (string.Equals(planName, "Plus", StringComparison.OrdinalIgnoreCase))
        {
            return ("PLUS", new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 10,
                FeaturedBadge = true,
                SearchPriority = false,
                ListingDurationDays = 45
            });
        }

        return (null, SubscriptionBenefitResponse.Free);
    }
}
