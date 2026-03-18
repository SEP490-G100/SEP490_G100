using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ContractRepository
{
    private readonly Sep490NannyDbContext _db;

    public ContractRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public IQueryable<Contract> GetQuery() => _db.Contracts.AsQueryable();
}
