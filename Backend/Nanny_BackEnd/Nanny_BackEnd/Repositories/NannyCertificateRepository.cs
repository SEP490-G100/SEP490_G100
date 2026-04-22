using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class NannyCertificateRepository : INannyCertificateRepository
{
    private readonly Sep490NannyDbContext _db;

    public NannyCertificateRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<List<NannyCertificate>> GetByNannyProfileIdAsync(Guid nannyProfileId) =>
        await _db.NannyCertificates
            .Where(c => c.NannyProfileId == nannyProfileId && !c.IsDeleted)
            .OrderByDescending(c => c.IssueDate)
            .ToListAsync();

    public void Add(NannyCertificate certificate) => _db.NannyCertificates.Add(certificate);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
