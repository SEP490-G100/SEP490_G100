using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class NannyProfileRepository
{
    private readonly Sep490NannyDbContext _db;

    public NannyProfileRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<NannyProfile?> FindByUserIdAsync(Guid userId) =>
        await _db.NannyProfiles.FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

    public IQueryable<NannyProfile> GetSearchQuery() =>
        _db.NannyProfiles
            .Where(n => !n.IsDeleted && !n.User.IsDeleted && n.User.Status == (int)UserStatus.Active)
            .Include(n => n.User)
            .Include(n => n.NannySkills.Where(s => !s.IsDeleted && !s.Skill.IsDeleted && s.Skill.IsActive))
                .ThenInclude(s => s.Skill)
            .Include(n => n.NannyAvailabilities.Where(a => !a.IsDeleted && a.IsAvailable))
            .AsQueryable();

    public async Task<(List<NannyProfile> Items, int TotalCount)> SearchAsync(
        NannyListRequest request,
        IEnumerable<Guid> skillIds)
    {
        var query = GetSearchQuery();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLower();
            query = query.Where(n =>
                (n.User.FirstName + " " + n.User.LastName).ToLower().Contains(keyword) ||
                (n.Bio != null && n.Bio.ToLower().Contains(keyword)) ||
                n.NannySkills.Any(ns => !ns.IsDeleted && ns.Skill.Name.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            var city = request.City.Trim().ToLower();
            query = query.Where(n => n.User.City != null && n.User.City.ToLower().Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(request.District))
        {
            var district = request.District.Trim().ToLower();
            query = query.Where(n => n.User.District != null && n.User.District.ToLower().Contains(district));
        }

        if (request.MinExperience.HasValue)
            query = query.Where(n => n.YearsOfExperience.HasValue && n.YearsOfExperience.Value >= request.MinExperience.Value);

        if (request.VerificationStatus.HasValue)
            query = query.Where(n => n.VerificationStatus == request.VerificationStatus.Value);

        if (request.MinExpectedSalary.HasValue)
            query = query.Where(n =>
                (n.ExpectedSalaryMin.HasValue && n.ExpectedSalaryMin.Value >= request.MinExpectedSalary.Value) ||
                (n.ExpectedSalaryMax.HasValue && n.ExpectedSalaryMax.Value >= request.MinExpectedSalary.Value));

        if (request.MaxExpectedSalary.HasValue)
            query = query.Where(n =>
                (n.ExpectedSalaryMin.HasValue && n.ExpectedSalaryMin.Value <= request.MaxExpectedSalary.Value) ||
                (n.ExpectedSalaryMax.HasValue && n.ExpectedSalaryMax.Value <= request.MaxExpectedSalary.Value));

        if (request.DayOfWeek.HasValue)
            query = query.Where(n => n.NannyAvailabilities.Any(a => !a.IsDeleted && a.IsAvailable && a.DayOfWeek == request.DayOfWeek.Value));

        if (request.TimeSlot.HasValue)
            query = query.Where(n => n.NannyAvailabilities.Any(a => !a.IsDeleted && a.IsAvailable && a.TimeSlot == request.TimeSlot.Value));

        var skillIdList = skillIds.Distinct().ToList();
        if (skillIdList.Count > 0)
            query = query.Where(n => n.NannySkills.Any(ns => !ns.IsDeleted && skillIdList.Contains(ns.SkillId)));

        if (request.MinAge.HasValue || request.MaxAge.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (request.MinAge.HasValue)
            {
                var maxBirthDate = today.AddYears(-request.MinAge.Value);
                query = query.Where(n => n.User.DateOfBirth.HasValue && n.User.DateOfBirth.Value <= maxBirthDate);
            }

            if (request.MaxAge.HasValue)
            {
                var minBirthDate = today.AddYears(-(request.MaxAge.Value + 1)).AddDays(1);
                query = query.Where(n => n.User.DateOfBirth.HasValue && n.User.DateOfBirth.Value >= minBirthDate);
            }
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 12 : Math.Min(request.PageSize, 50);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.VerificationStatus == 2)
            .ThenByDescending(n => n.AverageRating)
            .ThenByDescending(n => n.YearsOfExperience)
            .ThenBy(n => n.User.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<NannyProfile?> GetDetailAsync(Guid nannyProfileId) =>
        await GetSearchQuery()
            .FirstOrDefaultAsync(n => n.Id == nannyProfileId);

    public void Add(NannyProfile profile) => _db.NannyProfiles.Add(profile);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
