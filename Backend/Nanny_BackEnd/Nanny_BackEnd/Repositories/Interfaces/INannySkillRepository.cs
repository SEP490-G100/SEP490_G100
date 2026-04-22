using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface INannySkillRepository
{
    Task<List<NannySkill>> GetByNannyProfileIdAsync(Guid nannyProfileId);
    void AddRange(IEnumerable<NannySkill> skills);
    void RemoveRange(IEnumerable<NannySkill> skills);
    Task SaveChangesAsync();
}
