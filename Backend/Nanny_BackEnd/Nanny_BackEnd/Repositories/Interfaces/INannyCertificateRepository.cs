using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface INannyCertificateRepository
{
    Task<List<NannyCertificate>> GetByNannyProfileIdAsync(Guid nannyProfileId);
    void Add(NannyCertificate certificate);
    Task SaveChangesAsync();
}
