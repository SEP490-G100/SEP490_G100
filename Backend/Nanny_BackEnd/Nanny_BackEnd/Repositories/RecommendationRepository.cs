using System.Data;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Recommendation;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class RecommendationRepository : IRecommendationRepository
{
    private readonly Sep490NannyDbContext _db;

    public RecommendationRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    // Hard Filter: Nanny candidates cho một Job

    public async Task<List<NannyCandidate>> GetNannyCandidatesAsync(Guid jobId)
    {
        var job = await _db.JobPostings
            .Include(j => j.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .Include(j => j.JobScheduleRequirements.Where(s => !s.IsDeleted && s.IsRequired))
            .FirstOrDefaultAsync(j => j.Id == jobId && !j.IsDeleted);

        if (job == null) return new List<NannyCandidate>();

        var today = DateOnly.FromDateTime(DateTime.Today);

        var requiredSkillIds = job.JobRequirements
            .Where(r => r.IsRequired)
            .Select(r => r.SkillId)
            .ToHashSet();

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
                n.VerificationStatus == 2 &&
                n.User.Status == 1);

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

        var candidates = await query.ToListAsync();

        // In-memory filter: lịch và skill 
        var passed = new List<NannyProfile>();
        foreach (var n in candidates)
        {
            if (requiredSlots.Count > 0)
            {
                var nannySlots = n.NannyAvailabilities
                    .Select(a => (a.DayOfWeek, a.TimeSlot))
                    .ToHashSet();
                if (!requiredSlots.IsSubsetOf(nannySlots)) continue;
            }

            if (requiredSkillIds.Count > 0)
            {
                var nannySkillIds = n.NannySkills.Select(s => s.SkillId).ToHashSet();
                if (!requiredSkillIds.Overlaps(nannySkillIds)) continue;
            }

            passed.Add(n);
        }

        var passedIds = passed.Select(n => n.Id).ToList();
        var embeddings = await LoadNannyEmbeddingsAsync(passedIds);

        return passed.Select(n => new NannyCandidate
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
            ExpectedSalaryMin = n.ExpectedSalaryMin,
            ExpectedSalaryMax = n.ExpectedSalaryMax,
            SkillIds = n.NannySkills.Select(s => s.SkillId).ToList(),
            Embedding = embeddings.GetValueOrDefault(n.Id),
            Skills = n.NannySkills.Select(s => new NannySkillDto
            {
                SkillName = s.Skill.Name,
                ProficiencyLevel = s.ProficiencyLevel
            }).ToList()
        }).ToList();
    }

    // Hard Filter: Job candidates cho một Nanny

    public async Task<List<JobCandidate>> GetJobCandidatesAsync(Guid nannyProfileId)
    {
        var nanny = await _db.NannyProfiles
            .Include(n => n.NannySkills.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(n => n.Id == nannyProfileId && !n.IsDeleted);

        if (nanny == null) return new List<JobCandidate>();

        var nannySkillIds = nanny.NannySkills.Select(s => s.SkillId).ToHashSet();

        // Lương đã bỏ khỏi hard filter — xử lý mềm bởi salaryScore
        var jobs = await _db.JobPostings
            .Include(j => j.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .Where(j =>
                !j.IsDeleted &&
                j.Status == 1 &&
                j.ModerationStatus == 2)
            .ToListAsync();

        var passed = new List<JobPosting>();
        foreach (var j in jobs)
        {
            var jobRequiredSkillIds = j.JobRequirements
                .Where(r => r.IsRequired)
                .Select(r => r.SkillId)
                .ToHashSet();

            if (jobRequiredSkillIds.Count > 0 && !jobRequiredSkillIds.Overlaps(nannySkillIds))
                continue;

            passed.Add(j);
        }

        var passedIds = passed.Select(j => j.Id).ToList();
        var embeddings = await LoadJobEmbeddingsAsync(passedIds);

        return passed.Select(j => new JobCandidate
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
            RequiredSkillIds = j.JobRequirements
                .Where(r => r.IsRequired)
                .Select(r => r.SkillId).ToList(),
            Embedding = embeddings.GetValueOrDefault(j.Id),
            RequiredSkills = j.JobRequirements.Select(r => new JobRequiredSkillDto
            {
                SkillName = r.Skill.Name,
                MinProficiencyLevel = r.MinProficiencyLevel
            }).ToList()
        }).ToList();
    }

    // Read models cho embedding (Admin endpoints)

    public async Task<NannyReadModelDto?> GetNannyReadModelAsync(Guid nannyProfileId)
    {
        var n = await _db.NannyProfiles
            .Include(x => x.User)
            .Include(x => x.NannySkills.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Skill)
            .FirstOrDefaultAsync(x => x.Id == nannyProfileId && !x.IsDeleted);

        if (n == null) return null;

        var (embedding, updatedAt) = await LoadNannyEmbeddingWithTimestampAsync(nannyProfileId);

        return new NannyReadModelDto
        {
            NannyProfileId = n.Id,
            UserId = n.UserId,
            YearsOfExperience = n.YearsOfExperience,
            EducationLevel = n.EducationLevel,
            Bio = n.Bio,
            SkillNames = n.NannySkills.Select(s => s.Skill.Name).ToList(),
            SkillIds = n.NannySkills.Select(s => s.SkillId).ToList(),
            ExpectedSalaryMin = n.ExpectedSalaryMin,
            ExpectedSalaryMax = n.ExpectedSalaryMax,
            MaxTravelDistance = n.MaxTravelDistance,
            AverageRating = n.AverageRating,
            TotalReviews = n.TotalReviews,
            Latitude = n.User.Latitude,
            Longitude = n.User.Longitude,
            DateOfBirth = n.User.DateOfBirth,
            Embedding = embedding,
            EmbeddingUpdatedAt = updatedAt
        };
    }

    public async Task<JobReadModelDto?> GetJobReadModelAsync(Guid jobId)
    {
        var j = await _db.JobPostings
            .Include(x => x.ChildProfile)
            .Include(x => x.ParentProfile)
                .ThenInclude(parent => parent.ChildProfiles.Where(child => !child.IsDeleted))
            .Include(x => x.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .FirstOrDefaultAsync(x => x.Id == jobId && !x.IsDeleted);

        if (j == null) return null;

        var (embedding, updatedAt) = await LoadJobEmbeddingWithTimestampAsync(jobId);

        var model = mapJobReadModel(j);
        model.Embedding = embedding;
        model.EmbeddingUpdatedAt = updatedAt;
        return model;
    }

    public async Task<List<NannyReadModelDto>> GetAllNannyReadModelsAsync()
    {
        var nannies = await _db.NannyProfiles
            .Include(n => n.User)
            .Include(n => n.NannySkills.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Skill)
            .Where(n => !n.IsDeleted)
            .ToListAsync();

        return nannies.Select(n => new NannyReadModelDto
        {
            NannyProfileId = n.Id,
            UserId = n.UserId,
            YearsOfExperience = n.YearsOfExperience,
            EducationLevel = n.EducationLevel,
            Bio = n.Bio,
            SkillNames = n.NannySkills.Select(s => s.Skill.Name).ToList(),
            SkillIds = n.NannySkills.Select(s => s.SkillId).ToList(),
            ExpectedSalaryMin = n.ExpectedSalaryMin,
            ExpectedSalaryMax = n.ExpectedSalaryMax,
            MaxTravelDistance = n.MaxTravelDistance,
            AverageRating = n.AverageRating,
            TotalReviews = n.TotalReviews,
            Latitude = n.User.Latitude,
            Longitude = n.User.Longitude,
            DateOfBirth = n.User.DateOfBirth,
            Embedding = null,
            EmbeddingUpdatedAt = null
        }).ToList();
    }

    public async Task<List<JobReadModelDto>> GetAllJobReadModelsAsync()
    {
        var jobs = await _db.JobPostings
            .Include(j => j.ChildProfile)
            .Include(j => j.ParentProfile)
                .ThenInclude(parent => parent.ChildProfiles.Where(child => !child.IsDeleted))
            .Include(j => j.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .Where(j => !j.IsDeleted)
            .ToListAsync();

        return jobs.Select(mapJobReadModel).ToList();
    }

    public async Task<List<NannyReadModelDto>> GetPendingEmbedNanniesAsync()
    {
        var pendingIds = await GetIdsWithNullEmbeddingAsync("NannyProfiles");
        if (!pendingIds.Any()) return new List<NannyReadModelDto>();

        var nannies = await _db.NannyProfiles
            .Include(n => n.User)
            .Include(n => n.NannySkills.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Skill)
            .Where(n => pendingIds.Contains(n.Id))
            .ToListAsync();

        return nannies.Select(n => new NannyReadModelDto
        {
            NannyProfileId = n.Id,
            UserId = n.UserId,
            YearsOfExperience = n.YearsOfExperience,
            EducationLevel = n.EducationLevel,
            Bio = n.Bio,
            SkillNames = n.NannySkills.Select(s => s.Skill.Name).ToList(),
            SkillIds = n.NannySkills.Select(s => s.SkillId).ToList(),
            ExpectedSalaryMin = n.ExpectedSalaryMin,
            ExpectedSalaryMax = n.ExpectedSalaryMax,
            MaxTravelDistance = n.MaxTravelDistance,
            AverageRating = n.AverageRating,
            TotalReviews = n.TotalReviews,
            Latitude = n.User.Latitude,
            Longitude = n.User.Longitude,
            DateOfBirth = n.User.DateOfBirth,
            Embedding = null,
            EmbeddingUpdatedAt = null
        }).ToList();
    }

    public async Task<List<JobReadModelDto>> GetPendingEmbedJobsAsync()
    {
        var pendingIds = await GetIdsWithNullEmbeddingAsync("JobPostings");
        if (!pendingIds.Any()) return new List<JobReadModelDto>();

        var jobs = await _db.JobPostings
            .Include(j => j.ChildProfile)
            .Include(j => j.ParentProfile)
                .ThenInclude(parent => parent.ChildProfiles.Where(child => !child.IsDeleted))
            .Include(j => j.JobRequirements.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Skill)
            .Where(j => pendingIds.Contains(j.Id))
            .ToListAsync();

        return jobs.Select(mapJobReadModel).ToList();
    }

    private static JobReadModelDto mapJobReadModel(JobPosting job)
    {
        var selectedChildren = ChildProfileSnapshotHelper.ResolveChildren(
            job.ParentProfile,
            job.ChildProfileId ?? job.ChildProfile?.Id,
            job.NumberOfChildren);
        var childSnapshot = ChildProfileSnapshotHelper.BuildSnapshot(
            selectedChildren,
            job.ParentProfile?.FamilyDescription);

        return new JobReadModelDto
        {
            JobId = job.Id,
            Title = job.Title,
            ChildAgeGroup = childSnapshot.BirthType.HasValue ? (byte?)childSnapshot.BirthType.Value : null,
            NumberOfChildren = job.NumberOfChildren,
            Description = job.Description,
            Characteristic = childSnapshot.Characteristic,
            SpecialNeeds = childSnapshot.SpecialNeeds,
            RequiredSkillNames = job.JobRequirements.Select(requirement => requirement.Skill.Name).ToList(),
            RequiredSkillIds = job.JobRequirements.Where(requirement => requirement.IsRequired).Select(requirement => requirement.SkillId).ToList(),
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            SalaryNegotiable = job.SalaryNegotiable,
            MinNannyAge = job.MinNannyAge,
            MaxNannyAge = job.MaxNannyAge,
            Latitude = job.Latitude,
            Longitude = job.Longitude,
            City = job.City,
            District = job.District,
            Embedding = null,
            EmbeddingUpdatedAt = null
        };
    }

    // Embedding persistence (raw SQL — EF Core ignores these columns)

    public async Task SaveNannyEmbeddingAsync(Guid nannyProfileId, string embeddingJson)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE NannyProfiles SET Embedding = @emb, EmbeddingUpdatedAt = @ts WHERE Id = @id";
        AddParam(cmd, "@emb", embeddingJson);
        AddParam(cmd, "@ts", DateTime.UtcNow);
        AddParam(cmd, "@id", nannyProfileId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveJobEmbeddingAsync(Guid jobId, string embeddingJson)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE JobPostings SET Embedding = @emb, EmbeddingUpdatedAt = @ts WHERE Id = @id";
        AddParam(cmd, "@emb", embeddingJson);
        AddParam(cmd, "@ts", DateTime.UtcNow);
        AddParam(cmd, "@id", jobId);
        await cmd.ExecuteNonQueryAsync();
    }

    // Raw SQL helpers (private)

    private async Task<Dictionary<Guid, string?>> LoadNannyEmbeddingsAsync(IEnumerable<Guid> ids)
        => await LoadEmbeddingsAsync("NannyProfiles", ids);

    private async Task<Dictionary<Guid, string?>> LoadJobEmbeddingsAsync(IEnumerable<Guid> ids)
        => await LoadEmbeddingsAsync("JobPostings", ids);

    private async Task<(string? Embedding, DateTime? UpdatedAt)> LoadNannyEmbeddingWithTimestampAsync(Guid id)
        => await LoadEmbeddingWithTimestampAsync("NannyProfiles", id);

    private async Task<(string? Embedding, DateTime? UpdatedAt)> LoadJobEmbeddingWithTimestampAsync(Guid id)
        => await LoadEmbeddingWithTimestampAsync("JobPostings", id);

    private async Task<Dictionary<Guid, string?>> LoadEmbeddingsAsync(string table, IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        var result = new Dictionary<Guid, string?>();
        if (!idList.Any()) return result;

        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var paramNames = idList.Select((_, i) => $"@p{i}").ToList();
        cmd.CommandText = $"SELECT Id, Embedding FROM {table} WHERE Id IN ({string.Join(",", paramNames)})";
        for (int i = 0; i < idList.Count; i++)
            AddParam(cmd, $"@p{i}", idList[i]);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var rowId = reader.GetGuid(0);
            var emb = reader.IsDBNull(1) ? null : reader.GetString(1);
            result[rowId] = emb;
        }
        return result;
    }

    private async Task<(string? Embedding, DateTime? UpdatedAt)> LoadEmbeddingWithTimestampAsync(string table, Guid id)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Embedding, EmbeddingUpdatedAt FROM {table} WHERE Id = @id";
        AddParam(cmd, "@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (null, null);

        var emb = reader.IsDBNull(0) ? null : reader.GetString(0);
        var ts  = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
        return (emb, ts);
    }

    private async Task<List<Guid>> GetIdsWithNullEmbeddingAsync(string table)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        var result = new List<Guid>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id FROM {table} WHERE IsDeleted = 0 AND Embedding IS NULL";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetGuid(0));
        return result;
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}



