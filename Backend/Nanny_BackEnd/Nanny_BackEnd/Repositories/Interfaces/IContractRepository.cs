using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IContractRepository
{
    IQueryable<Contract> GetQuery();
    Task<List<Contract>> GetContractsByUserIdAsync(Guid userId);
    Task<Contract?> GetContractDetailAsync(Guid contractId);
    Task<Contract?> GetContractForUpdateAsync(Guid contractId);
    Task SaveChangesAsync();
}
