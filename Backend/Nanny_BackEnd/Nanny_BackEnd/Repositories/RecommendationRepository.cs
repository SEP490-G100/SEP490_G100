using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class RecommendationRepository
{
    private readonly Sep490NannyDbContext _db;

    public RecommendationRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    // ──────────────────────────────────────────────────────────────
    // Hard Filter: Nanny candidates cho một Job
    // ──────────────────────────────────────────────────────────────

    public class NannyCandidate
    {
        public Guid NannyProfileId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }
        public int? YearsOfExperience { get; set; }
        public int? EducationLevel { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int? MaxTravelDistance { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? Embedding { get; set; }
        public List<NannySkillDto> Skills { get; set; } = new();
    }

    public class JobCandidate
    {
        public Guid JobId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public bool SalaryNegotiable { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? Embedding { get; set; }
        public List<JobRequiredSkillDto> RequiredSkills { get; set; } = new();
    }

    /// <summary>
    /// Hard filter: lấy danh sách nanny candidates cho một job.
    /// Điều kiện: IsDeleted=0, VerificationStatus=2, User.Status=1,
    /// tuổi trong range, lương overlap (khi cả 2 có giá trị),
    /// lịch phủ TẤT CẢ IsRequired=1 slots, ít nhất 1 skill khớp.
    /// </summary>
    public async Task<List<NannyCandidate>> GetNannyCandidatesAsync(Guid jobId)
    {
        var job = await _db.JobPostings
            .Include(j => j.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .Include(j => j.JobScheduleRequirements.Where(s => !s.IsDeleted && s.IsRequired))
            .FirstOrDefaultAsync(j => j.Id == jobId && !j.IsDeleted);

        if (job == null) return new List<NannyCandidate>();

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Set required skill IDs (job side)
        var requiredSkillIds = job.JobRequirements
            .Where(r => r.IsRequired)
            .Select(r => r.SkillId)
            .ToHashSet();

        // Set required schedule slots (DayOfWeek, TimeSlot)
        var requiredSlots = job.JobScheduleRequirements
            .Select(s => (s.DayOfWeek, s.TimeSlot))
            .ToHashSet();

        var query = _db.NannyProfiles
            .Include(n => n.User)
            .Include(n => n.NannySkills.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Skill)
            .Include(n => n.NannyAvailabilities.Where(a => !a.IsDeleted && a.IsAvailable))
            .Where(n =>
                !n.IsDeleted &&
                n.VerificationStatus == 2 &&       // Approved
                n.User.Status == 1);               // Active

        // Tuổi nanny filter (MinNannyAge / MaxNannyAge)
        if (job.MinNannyAge.HasValue)
        {
            var maxBirth = today.AddYears(-job.MinNannyAge.Value);
            query = query.Where(n => n.User.DateOfBirth == null || n.User.DateOfBirth <= maxBirth);
        }
        if (job.MaxNannyAge.HasValue)
        {
            var minBirth = today.AddYears(-job.MaxNannyAge.Value - 1);
            query = query.Where(n => n.User.DateOfBirth == null || n.User.DateOfBirth >= minBirth);
        }

        // Lương overlap: chỉ filter khi CẢ 2 bên có giá trị
        if (job.SalaryMin.HasValue && job.SalaryMax.HasValue)
        {
            query = query.Where(n =>
                !n.ExpectedSalaryMin.HasValue || !n.ExpectedSalaryMax.HasValue ||
                (n.ExpectedSalaryMin <= job.SalaryMax && n.ExpectedSalaryMax >= job.SalaryMin));
        }

        var candidates = await query.ToListAsync();

        // In-memory filter: lịch và skill
        var result = new List<NannyCandidate>();

        foreach (var n in candidates)
        {
            // Schedule: nanny phải có TẤT CẢ required slots
            if (requiredSlots.Count > 0)
            {
                var nannySlots = n.NannyAvailabilities
                    .Select(a => (a.DayOfWeek, a.TimeSlot))
                    .ToHashSet();

                if (!requiredSlots.IsSubsetOf(nannySlots))
                    continue;
            }

            // Skill: ít nhất 1 skill khớp (nếu job có required skills)
            if (requiredSkillIds.Count > 0)
            {
                var nannySkillIds = n.NannySkills.Select(s => s.SkillId).ToHashSet();
                if (!requiredSkillIds.Overlaps(nannySkillIds))
                    continue;
            }

            result.Add(new NannyCandidate
            {
                NannyProfileId = n.Id,
                UserId = n.UserId,
                FullName = $"{n.User.FirstName} {n.User.LastName}".Trim(),
                AvatarUrl = n.User.AvatarUrl,
                Bio = n.Bio,
                YearsOfExperience = n.YearsOfExperience,
                EducationLevel = n.EducationLevel,
                AverageRating = n.AverageRating,
                TotalReviews = n.TotalReviews,
                MaxTravelDistance = n.MaxTravelDistance,
                Latitude = n.User.Latitude,
                Longitude = n.User.Longitude,
                Embedding = n.Embedding,
                Skills = n.NannySkills.Select(s => new NannySkillDto
                {
                    SkillName = s.Skill.Name,
                    ProficiencyLevel = s.ProficiencyLevel
                }).ToList()
            });
        }

        return result;
    }

    /// <summary>
    /// Hard filter: lấy danh sách job candidates cho một nanny.
    /// Điều kiện: IsDeleted=0, Status=1(Public), ModerationStatus=2(Approved),
    /// lương overlap (khi cả 2 có giá trị), ít nhất 1 skill khớp.
    /// </summary>
    public async Task<List<JobCandidate>> GetJobCandidatesAsync(Guid nannyProfileId)
    {
        var nanny = await _db.NannyProfiles
            .Include(n => n.NannySkills.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(n => n.Id == nannyProfileId && !n.IsDeleted);

        if (nanny == null) return new List<JobCandidate>();

        var nannySkillIds = nanny.NannySkills.Select(s => s.SkillId).ToHashSet();

        var query = _db.JobPostings
            .Include(j => j.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .Where(j =>
                !j.IsDeleted &&
                j.Status == 1 &&               // Public
                j.ModerationStatus == 2);      // Approved

        // Lương overlap: chỉ filter khi CẢ 2 bên có giá trị
        if (nanny.ExpectedSalaryMin.HasValue && nanny.ExpectedSalaryMax.HasValue)
        {
            query = query.Where(j =>
                !j.SalaryMin.HasValue || !j.SalaryMax.HasValue ||
                (j.SalaryMin <= nanny.ExpectedSalaryMax && j.SalaryMax >= nanny.ExpectedSalaryMin));
        }

        var jobs = await query.ToListAsync();

        // In-memory filter: skill
        var result = new List<JobCandidate>();

        foreach (var j in jobs)
        {
            var jobRequiredSkillIds = j.JobRequirements
                .Where(r => r.IsRequired)
                .Select(r => r.SkillId)
                .ToHashSet();

            // Ít nhất 1 skill khớp (nếu job có required skills)
            if (jobRequiredSkillIds.Count > 0 && !jobRequiredSkillIds.Overlaps(nannySkillIds))
                continue;

            result.Add(new JobCandidate
            {
                JobId = j.Id,
                Title = j.Title,
                Description = j.Description,
                City = j.City,
                District = j.District,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                SalaryNegotiable = j.SalaryNegotiable,
                Latitude = j.Latitude,
                Longitude = j.Longitude,
                Embedding = j.Embedding,
                RequiredSkills = j.JobRequirements.Select(r => new JobRequiredSkillDto
                {
                    SkillName = r.Skill.Name,
                    MinProficiencyLevel = r.MinProficiencyLevel
                }).ToList()
            });
        }

        return result;
    }

    // ──────────────────────────────────────────────────────────────
    // Read models cho embedding
    // ──────────────────────────────────────────────────────────────

    public async Task<NannyReadModelDto?> GetNannyReadModelAsync(Guid nannyProfileId)
    {
        var n = await _db.NannyProfiles
            .Include(x => x.User)
            .Include(x => x.NannySkills.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Skill)
            .FirstOrDefaultAsync(x => x.Id == nannyProfileId && !x.IsDeleted);

        if (n == null) return null;

        return new NannyReadModelDto
        {
            NannyProfileId = n.Id,
            UserId = n.UserId,
            YearsOfExperience = n.YearsOfExperience,
            EducationLevel = n.EducationLevel,
            Bio = n.Bio,
            SkillNames = n.NannySkills.Select(s => s.Skill.Name).ToList(),
            ExpectedSalaryMin = n.ExpectedSalaryMin,
            ExpectedSalaryMax = n.ExpectedSalaryMax,
            MaxTravelDistance = n.MaxTravelDistance,
            AverageRating = n.AverageRating,
            TotalReviews = n.TotalReviews,
            Latitude = n.User.Latitude,
            Longitude = n.User.Longitude,
            DateOfBirth = n.User.DateOfBirth,
            Embedding = n.Embedding,
            EmbeddingUpdatedAt = n.EmbeddingUpdatedAt
        };
    }

    public async Task<JobReadModelDto?> GetJobReadModelAsync(Guid jobId)
    {
        var j = await _db.JobPostings
            .Include(x => x.ChildProfile)
            .Include(x => x.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .FirstOrDefaultAsync(x => x.Id == jobId && !x.IsDeleted);

        if (j == null) return null;

        return new JobReadModelDto
        {
            JobId = j.Id,
            Title = j.Title,
            ChildAgeGroup = j.ChildProfile?.ChildAgeGroup,
            Description = j.Description,
            Characteristic = j.ChildProfile?.Characteristic,
            SpecialNeeds = j.ChildProfile?.SpecialNeeds,
            RequiredSkillNames = j.JobRequirements.Select(r => r.Skill.Name).ToList(),
            SalaryMin = j.SalaryMin,
            SalaryMax = j.SalaryMax,
            SalaryNegotiable = j.SalaryNegotiable,
            MinNannyAge = j.MinNannyAge,
            MaxNannyAge = j.MaxNannyAge,
            Latitude = j.Latitude,
            Longitude = j.Longitude,
            City = j.City,
            District = j.District,
            Embedding = j.Embedding,
            EmbeddingUpdatedAt = j.EmbeddingUpdatedAt
        };
    }

    public async Task<List<NannyReadModelDto>> GetPendingEmbedNanniesAsync()
    {
        var nannies = await _db.NannyProfiles
            .Include(n => n.User)
            .Include(n => n.NannySkills.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Skill)
            .Where(n => !n.IsDeleted && n.Embedding == null)
            .ToListAsync();

        return nannies.Select(n => new NannyReadModelDto
        {
            NannyProfileId = n.Id,
            UserId = n.UserId,
            YearsOfExperience = n.YearsOfExperience,
            EducationLevel = n.EducationLevel,
            Bio = n.Bio,
            SkillNames = n.NannySkills.Select(s => s.Skill.Name).ToList(),
            ExpectedSalaryMin = n.ExpectedSalaryMin,
            ExpectedSalaryMax = n.ExpectedSalaryMax,
            MaxTravelDistance = n.MaxTravelDistance,
            AverageRating = n.AverageRating,
            TotalReviews = n.TotalReviews,
            Latitude = n.User.Latitude,
            Longitude = n.User.Longitude,
            DateOfBirth = n.User.DateOfBirth,
            Embedding = n.Embedding,
            EmbeddingUpdatedAt = n.EmbeddingUpdatedAt
        }).ToList();
    }

    public async Task<List<JobReadModelDto>> GetPendingEmbedJobsAsync()
    {
        var jobs = await _db.JobPostings
            .Include(j => j.ChildProfile)
            .Include(j => j.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .Where(j => !j.IsDeleted && j.Embedding == null)
            .ToListAsync();

        return jobs.Select(j => new JobReadModelDto
        {
            JobId = j.Id,
            Title = j.Title,
            ChildAgeGroup = j.ChildProfile?.ChildAgeGroup,
            Description = j.Description,
            Characteristic = j.ChildProfile?.Characteristic,
            SpecialNeeds = j.ChildProfile?.SpecialNeeds,
            RequiredSkillNames = j.JobRequirements.Select(r => r.Skill.Name).ToList(),
            SalaryMin = j.SalaryMin,
            SalaryMax = j.SalaryMax,
            SalaryNegotiable = j.SalaryNegotiable,
            MinNannyAge = j.MinNannyAge,
            MaxNannyAge = j.MaxNannyAge,
            Latitude = j.Latitude,
            Longitude = j.Longitude,
            City = j.City,
            District = j.District,
            Embedding = j.Embedding,
            EmbeddingUpdatedAt = j.EmbeddingUpdatedAt
        }).ToList();
    }

    // ──────────────────────────────────────────────────────────────
    // Embedding persistence
    // ──────────────────────────────────────────────────────────────

    public async Task SaveNannyEmbeddingAsync(Guid nannyProfileId, string embeddingJson)
    {
        var n = await _db.NannyProfiles.FindAsync(nannyProfileId);
        if (n == null) return;

        n.Embedding = embeddingJson;
        n.EmbeddingUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task SaveJobEmbeddingAsync(Guid jobId, string embeddingJson)
    {
        var j = await _db.JobPostings.FindAsync(jobId);
        if (j == null) return;

        j.Embedding = embeddingJson;
        j.EmbeddingUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
