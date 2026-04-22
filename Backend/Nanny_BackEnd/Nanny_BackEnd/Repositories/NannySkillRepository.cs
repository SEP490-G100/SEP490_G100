using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class NannySkillRepository : INannySkillRepository
{
    private readonly Sep490NannyDbContext _db;

    public NannySkillRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<List<NannySkill>> GetByNannyProfileIdAsync(Guid nannyProfileId) =>
        await _db.NannySkills
            .Include(s => s.Skill)
            .Where(s => s.NannyProfileId == nannyProfileId && !s.IsDeleted)
            .ToListAsync();

    public void AddRange(IEnumerable<NannySkill> skills) => _db.NannySkills.AddRange(skills);

    public void RemoveRange(IEnumerable<NannySkill> skills) => _db.NannySkills.RemoveRange(skills);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
