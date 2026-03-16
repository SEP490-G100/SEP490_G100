using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class JobService
{
    private readonly JobRepository _jobRepo;
    private readonly FavoriteRepository _favoriteRepo;
    private readonly GeocodingService _geo;

    public JobService(JobRepository jobRepo, FavoriteRepository favoriteRepo, GeocodingService geo)
    {
        _jobRepo = jobRepo;
        _favoriteRepo = favoriteRepo;
        _geo = geo;
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
        return jobs.Select(j => MapToListItem(j, nannyLat, nannyLng, currentUserId)).ToList();
    }

    public async Task<List<SearchJobResponse>> getMyJobs(Guid parentProfileId)
    {
        var jobs = await _jobRepo.getListPosting(parentProfileId);
        return jobs.Select(j => MapToListItem(j)).ToList();
    }

    /// <summary>Tìm kiếm theo tiêu đề (partial match, không phân biệt hoa thường)</summary>
    public async Task<List<SearchJobResponse>> searchByTitle(string? title)
    {
        var jobs = await _jobRepo.searchByTitle(title);
        return jobs.Select(j => MapToListItem(j)).ToList();
    }

    public async Task<JobPostingDetailResponse> getDetail(Guid jobId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xoá.");
        return MapToDetail(job);
    }

    public async Task<Guid> createJob(Guid parentProfileId, CreateJobPostingRequest req)
    {
        var countThisMonth = await _jobRepo.countJobPostingsInCurrentMonth(parentProfileId);
        if (countThisMonth >= 2)
            throw new InvalidOperationException("Bạn chỉ được đăng tối đa 2 bài viết trong 1 tháng.");

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException("Phải nhập mức lương tối thiểu hoặc chọn 'Thương lượng'.");

        decimal? lat = null, lng = null;
        var coords = await _geo.geocode(req.Location, req.City, req.District);
        if (coords.HasValue) { lat = coords.Value.Lat; lng = coords.Value.Lng; }

        var job = new JobPosting
        {
            Id               = Guid.NewGuid(),
            ParentProfileId  = parentProfileId,
            Title            = req.Title.Trim(),
            Description      = req.Description.Trim(),
            JobType          = req.JobType,
            SalaryMin        = req.SalaryMin,
            SalaryType       = 2,
            SalaryNegotiable = req.SalaryNegotiable,
            NumberOfChildren = req.NumberOfChildren,
            Location         = req.Location?.Trim(),
            City             = req.City?.Trim(),
            District         = req.District?.Trim(),
            Latitude         = lat,
            Longitude        = lng,
            ExpiresAt        = DateTime.UtcNow.AddMonths(1),
            Status           = req.Status,         
            ModerationStatus = 2,  
            PublishedAt      = req.Status == 1 ? DateTime.UtcNow : null,
            CreatedAt        = DateTime.UtcNow,
            CreatedBy        = parentProfileId
        };

        await _jobRepo.createJobPosting(job);
        return job.Id;
    }

    public async Task updateJob(Guid jobId, Guid parentProfileId, UpdateJobPostingRequest req)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xoá.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa tin đăng này.");

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException("Phải nhập mức lương tối thiểu hoặc chọn 'Thương lượng'.");

        // Re-geocode nếu địa chỉ thay đổi
        var addrChanged = req.Location != job.Location
                       || req.City     != job.City
                       || req.District != job.District;
        if (addrChanged)
        {
            var coords = await _geo.geocode(req.Location, req.City, req.District);
            if (coords.HasValue) { job.Latitude = coords.Value.Lat; job.Longitude = coords.Value.Lng; }
        }

        job.Title            = req.Title.Trim();
        job.Description      = req.Description.Trim();
        job.JobType          = req.JobType;
        job.SalaryMin        = req.SalaryMin;
        job.SalaryType       = 2;
        job.SalaryNegotiable = req.SalaryNegotiable;
        job.NumberOfChildren = req.NumberOfChildren;
        job.Location         = req.Location?.Trim();
        job.City             = req.City?.Trim();
        job.District         = req.District?.Trim();
        job.Status           = req.Status;
        job.PublishedAt      = req.Status == 1
            ? (job.PublishedAt ?? DateTime.UtcNow)
            : null;
        job.ClosedAt         = req.Status == 0 ? DateTime.UtcNow : null;

        await _jobRepo.updateJobPosting(job);
    }

    public async Task<string> togglePublish(Guid jobId, Guid parentProfileId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xoá.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Bạn không có quyền thay đổi trạng thái tin đăng này.");

        await _jobRepo.togglePublishPosting(job);
        return job.Status == 1 ? "Tin đăng đã được Publish thành công." : "Tin đăng đã được Unpublish.";
    }

    public async Task deletePost(Guid jobId, Guid parentProfileId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xoá.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException("Bạn không có quyền xoá tin đăng này.");

        var hasPending = job.JobApplications.Any(a => a.Status == 0);
        if (hasPending)
            throw new InvalidOperationException(
                "Không thể xoá tin đang có đơn ứng tuyển chờ xét duyệt. Vui lòng xử lý các đơn trước.");

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

    // ── Mapping ──────────────────────────────────────────────────────────

    private static SearchJobResponse MapToListItem(
        JobPosting j, double? nannyLat = null, double? nannyLng = null, Guid? currentUserId = null)
    {
        double? distance = null;
        if (nannyLat.HasValue && nannyLng.HasValue && j.Latitude.HasValue && j.Longitude.HasValue)
            distance = GeocodingService.CalculateDistanceKm(
                nannyLat.Value, nannyLng.Value,
                (double)j.Latitude.Value, (double)j.Longitude.Value);

        return new SearchJobResponse
        {
            Id               = j.Id,
            ParentProfileId  = j.ParentProfileId,
            IsOwner          = currentUserId.HasValue && j.ParentProfile?.UserId == currentUserId.Value,
            Title            = j.Title,
            Description      = j.Description,
            ParentName       = $"{j.ParentProfile?.User?.FirstName} {j.ParentProfile?.User?.LastName}".Trim(),
            JobType          = j.JobType,
            SalaryMin        = j.SalaryMin,
            SalaryNegotiable = j.SalaryNegotiable,
            City             = j.City,
            District         = j.District,
            Location         = j.Location,
            NumberOfChildren = j.NumberOfChildren,
            Latitude         = (double?)j.Latitude,
            Longitude        = (double?)j.Longitude,
            Status           = j.Status,
            PublishedAt      = j.PublishedAt,
            DistanceKm       = distance
        };
    }

    private static JobPostingDetailResponse MapToDetail(JobPosting j) => new()
    {
        Id               = j.Id,
        ParentProfileId  = j.ParentProfileId,
        ParentName       = $"{j.ParentProfile?.User?.FirstName} {j.ParentProfile?.User?.LastName}".Trim(),
        Title            = j.Title,
        Description      = j.Description,
        JobType          = j.JobType,
        SalaryMin        = j.SalaryMin,
        SalaryNegotiable = j.SalaryNegotiable,
        NumberOfChildren = j.NumberOfChildren,
        Location         = j.Location,
        City             = j.City,
        District         = j.District,
        Latitude         = (double?)j.Latitude,
        Longitude        = (double?)j.Longitude,
        Status           = j.Status,
        ModerationStatus = j.ModerationStatus,
        PublishedAt      = j.PublishedAt,
        ExpiresAt        = j.ExpiresAt,
        ClosedAt         = j.ClosedAt,
        CreatedAt        = j.CreatedAt,
        ApplicationCount = j.JobApplications.Count
    };
}
