using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;


public class JobService
{
    private readonly JobRepository _jobRepo;
    private readonly FavoriteRepository _favoriteRepo;

    public JobService(JobRepository jobRepo, FavoriteRepository favoriteRepo)
    {
        _jobRepo = jobRepo;
        _favoriteRepo = favoriteRepo;
    }


    public async Task<List<SearchJobResponse>> findJobs(SearchJobRequest filters)
    {
        // Chuẩn hóa page size: không được vượt quá 50
        if (filters.PageSize > 50) filters.PageSize = 50;
        if (filters.Page < 1) filters.Page = 1;

        var jobs = await _jobRepo.searchJobPosting(filters);
        return jobs.Select(MapToListItem).ToList();
    }

 
    public async Task<List<SearchJobResponse>> GetMyJobsAsync(Guid parentProfileId)
    {
        var jobs = await _jobRepo.getListPosting(parentProfileId);
        return jobs.Select(MapToListItem).ToList();
    }


    public async Task<JobPostingDetailResponse> GetDetailAsync(Guid jobId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xoá.");

        return MapToDetail(job);
    }

    public async Task<Guid> CreateAsync(Guid parentProfileId, CreateJobPostingRequest req)
    {

        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException(
                "Phải nhập mức lương tối thiểu hoặc chọn 'Thương lượng'.");

        if (req.SalaryMin.HasValue && req.SalaryMax.HasValue && req.SalaryMin > req.SalaryMax)
            throw new InvalidOperationException(
                "Lương tối thiểu không được lớn hơn lương tối đa.");

        if (req.StartDate.HasValue && req.EndDate.HasValue && req.StartDate >= req.EndDate)
            throw new InvalidOperationException(
                "Ngày bắt đầu phải nhỏ hơn ngày kết thúc.");

        if (req.StartDate.HasValue && req.StartDate.Value < DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException(
                "Ngày bắt đầu không được là ngày trong quá khứ.");

        if (req.WorkingHoursStart.HasValue && req.WorkingHoursEnd.HasValue
            && req.WorkingHoursStart >= req.WorkingHoursEnd)
            throw new InvalidOperationException(
                "Giờ bắt đầu phải nhỏ hơn giờ kết thúc.");

        if (req.ExpiresAt.HasValue && req.ExpiresAt.Value <= DateTime.UtcNow)
            throw new InvalidOperationException(
                "Ngày hết hạn phải là ngày trong tương lai.");


        var job = new JobPosting
        {
            Id             = Guid.NewGuid(),
            ParentProfileId= parentProfileId,
            Title          = req.Title.Trim(),
            Description    = req.Description.Trim(),
            JobType        = req.JobType,
            SalaryMin      = req.SalaryMin,
            SalaryMax      = req.SalaryMax,
            SalaryType     = req.SalaryType,
            SalaryNegotiable = req.SalaryNegotiable,
            StartDate      = req.StartDate,
            EndDate        = req.EndDate,
            WorkingHoursStart = req.WorkingHoursStart,
            WorkingHoursEnd   = req.WorkingHoursEnd,
            WorkingDays    = req.WorkingDays?.Trim(),
            NumberOfChildren = req.NumberOfChildren,
            Location       = req.Location?.Trim(),
            City           = req.City?.Trim(),
            District       = req.District?.Trim(),
            Latitude       = req.Latitude,
            Longitude      = req.Longitude,
            ExpiresAt      = req.ExpiresAt,
            Status         = 0,   // Draft — chưa publish
            ModerationStatus = 0, // Pending — chưa được admin duyệt
            CreatedAt      = DateTime.UtcNow,
            CreatedBy      = parentProfileId
        };

        foreach (var skillId in req.RequiredSkillIds.Distinct())
        {
            job.JobRequirements.Add(new JobRequirement
            {
                Id          = Guid.NewGuid(),
                JobPostingId= job.Id,
                SkillId     = skillId,
                IsRequired  = true
            });
        }

        await _jobRepo.createJobPosting(job);
        return job.Id;
    }


    public async Task UpdateAsync(Guid jobId, Guid parentProfileId, UpdateJobPostingRequest req)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xoá.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException(
                "Bạn không có quyền chỉnh sửa tin đăng này.");

        if (job.Status == 1)
            throw new InvalidOperationException(
                "Không thể sửa tin đang Publish. Hãy Unpublish trước.");


        if (!req.SalaryNegotiable && req.SalaryMin == null)
            throw new InvalidOperationException(
                "Phải nhập mức lương tối thiểu hoặc chọn 'Thương lượng'.");

        if (req.SalaryMin.HasValue && req.SalaryMax.HasValue && req.SalaryMin > req.SalaryMax)
            throw new InvalidOperationException(
                "Lương tối thiểu không được lớn hơn lương tối đa.");

        if (req.StartDate.HasValue && req.EndDate.HasValue && req.StartDate >= req.EndDate)
            throw new InvalidOperationException(
                "Ngày bắt đầu phải nhỏ hơn ngày kết thúc.");

        if (req.WorkingHoursStart.HasValue && req.WorkingHoursEnd.HasValue
            && req.WorkingHoursStart >= req.WorkingHoursEnd)
            throw new InvalidOperationException(
                "Giờ bắt đầu phải nhỏ hơn giờ kết thúc.");

        if (req.ExpiresAt.HasValue && req.ExpiresAt.Value <= DateTime.UtcNow)
            throw new InvalidOperationException(
                "Ngày hết hạn phải là ngày trong tương lai.");


        job.Title            = req.Title.Trim();
        job.Description      = req.Description.Trim();
        job.JobType          = req.JobType;
        job.SalaryMin        = req.SalaryMin;
        job.SalaryMax        = req.SalaryMax;
        job.SalaryType       = req.SalaryType;
        job.SalaryNegotiable = req.SalaryNegotiable;
        job.StartDate        = req.StartDate;
        job.EndDate          = req.EndDate;
        job.WorkingHoursStart= req.WorkingHoursStart;
        job.WorkingHoursEnd  = req.WorkingHoursEnd;
        job.WorkingDays      = req.WorkingDays?.Trim();
        job.NumberOfChildren = req.NumberOfChildren;
        job.Location         = req.Location?.Trim();
        job.City             = req.City?.Trim();
        job.District         = req.District?.Trim();
        job.Latitude         = req.Latitude;
        job.Longitude        = req.Longitude;
        job.ExpiresAt        = req.ExpiresAt;


        _jobRepo.RemoveRequirements(job.JobRequirements.ToList());
        foreach (var skillId in req.RequiredSkillIds.Distinct())
        {
            job.JobRequirements.Add(new JobRequirement
            {
                Id = Guid.NewGuid(),
                JobPostingId = job.Id,
                SkillId = skillId,
                IsRequired = true
            });
        }

        await _jobRepo.updateJobPosting(job);
    }


    public async Task<string> togglePublish(Guid jobId, Guid parentProfileId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xoá.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException(
                "Bạn không có quyền thay đổi trạng thái tin đăng này.");

        await _jobRepo.togglePublishPosting(job);

        return job.Status == 1
            ? "Tin đăng đã được Publish thành công."
            : "Tin đăng đã được Unpublish.";
    }


    public async Task deletePost(Guid jobId, Guid parentProfileId)
    {
        var job = await _jobRepo.viewDetailPosting(jobId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xoá.");

        if (job.ParentProfileId != parentProfileId)
            throw new UnauthorizedAccessException(
                "Bạn không có quyền xoá tin đăng này.");

        // Không cho xoá nếu còn đơn đang pending (Status=0)
        var hasPendingApplications = job.JobApplications.Any(a => a.Status == 0);
        if (hasPendingApplications)
            throw new InvalidOperationException(
                "Không thể xoá tin đang có đơn ứng tuyển chờ xét duyệt. " +
                "Vui lòng xử lý các đơn trước.");

        await _jobRepo.deleteJobPosting(job);
    }


    public async Task addFavoriteJob(Guid nannyProfileId, Guid jobPostingId)
    {
        var job = await _jobRepo.viewDetailPosting(jobPostingId)
            ?? throw new KeyNotFoundException("Tin đăng không tồn tại.");

        // Kiểm tra đã lưu chưa
        var alreadySaved = await _favoriteRepo.IsFavoriteJobAsync(nannyProfileId, jobPostingId);
        if (alreadySaved)
            throw new InvalidOperationException("Bạn đã lưu tin này trước đó rồi.");

        await _favoriteRepo.AddFavoriteJobAsync(nannyProfileId, jobPostingId);
    }


    private static SearchJobResponse MapToListItem(JobPosting j) => new()
    {
        Id              = j.Id,
        Title           = j.Title,
        Description     = j.Description,
        JobType         = j.JobType,
        SalaryMin       = j.SalaryMin,
        SalaryMax       = j.SalaryMax,
        SalaryType      = j.SalaryType,
        SalaryNegotiable= j.SalaryNegotiable,
        City            = j.City,
        District        = j.District,
        Location        = j.Location,
        NumberOfChildren= j.NumberOfChildren,
        Latitude        = (double?)j.Latitude,
        Longitude       = (double?)j.Longitude,
        PublishedAt     = j.PublishedAt,
        ExpiresAt       = j.ExpiresAt,
        RequiredSkills  = j.JobRequirements.Select(jr => jr.Skill.Name).ToList()
    };

    private static JobPostingDetailResponse MapToDetail(JobPosting j) => new()
    {
        Id               = j.Id,
        ParentProfileId  = j.ParentProfileId,
        ParentName       = $"{j.ParentProfile?.User?.FirstName} {j.ParentProfile?.User?.LastName}".Trim(),
        Title            = j.Title,
        Description      = j.Description,
        JobType          = j.JobType,
        SalaryMin        = j.SalaryMin,
        SalaryMax        = j.SalaryMax,
        SalaryType       = j.SalaryType,
        SalaryNegotiable = j.SalaryNegotiable,
        StartDate        = j.StartDate,
        EndDate          = j.EndDate,
        WorkingHoursStart= j.WorkingHoursStart?.ToString("HH:mm"),
        WorkingHoursEnd  = j.WorkingHoursEnd?.ToString("HH:mm"),
        WorkingDays      = j.WorkingDays,
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
        RequiredSkills   = j.JobRequirements.Select(jr => jr.Skill.Name).ToList(),
        ApplicationCount = j.JobApplications.Count
    };
}
