using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ContractRepository : IContractRepository
{
    private readonly Sep490NannyDbContext _db;

    public ContractRepository(Sep490NannyDbContext db) => _db = db;

    public IQueryable<Contract> GetQuery() => _db.Contracts.AsQueryable();

    // Lấy tất cả contracts của user (cả khi là Parent lẫn Nanny)
    public async Task<List<Contract>> GetContractsByUserIdAsync(Guid userId) =>
        await _db.Contracts
            .Where(c => !c.IsDeleted &&
                        !c.HiringRecord.IsDeleted &&
                        (c.HiringRecord.ParentProfile.UserId == userId ||
                         c.HiringRecord.NannyProfile.UserId == userId))
            .Include(c => c.HiringRecord)
                .ThenInclude(h => h.JobApplication)
                    .ThenInclude(a => a.JobPosting)
            .Include(c => c.HiringRecord)
                .ThenInclude(h => h.ParentProfile)
                    .ThenInclude(p => p.User)
            .Include(c => c.HiringRecord)
                .ThenInclude(h => h.NannyProfile)
                    .ThenInclude(n => n.User)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    // Lấy chi tiết 1 contract kèm đầy đủ thông tin 2 bên
    public async Task<Contract?> GetContractDetailAsync(Guid contractId) =>
        await _db.Contracts
            .Where(c => c.Id == contractId && !c.IsDeleted)
            .Include(c => c.ContractTemplate)
            .Include(c => c.HiringRecord)
                .ThenInclude(h => h.JobApplication)
                    .ThenInclude(a => a.JobPosting)
                        .ThenInclude(p => p.JobScheduleRequirements)
            .Include(c => c.HiringRecord)
                .ThenInclude(h => h.ParentProfile)
                    .ThenInclude(p => p.User)
            .Include(c => c.HiringRecord)
                .ThenInclude(h => h.NannyProfile)
                    .ThenInclude(n => n.User)
            .FirstOrDefaultAsync();

    public Task<Contract?> GetContractForUpdateAsync(Guid contractId) =>
        GetContractDetailAsync(contractId);

    public void AddContract(Contract contract) => _db.Contracts.Add(contract);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
