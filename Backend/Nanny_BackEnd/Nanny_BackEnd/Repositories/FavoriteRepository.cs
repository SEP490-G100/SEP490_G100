using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class FavoriteRepository
{
    private readonly Sep490NannyDbContext _db;

    public FavoriteRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<bool> isFavoriteJob(Guid nannyProfileId, Guid jobPostingId) =>
        await _db.FavoriteJobPostings.AnyAsync(f =>
            f.NannyProfileId == nannyProfileId &&
            f.JobPostingId == jobPostingId &&
            !f.IsDeleted);

    public async Task addFavoriteJob(Guid nannyProfileId, Guid jobPostingId)
    {
        _db.FavoriteJobPostings.Add(new FavoriteJobPosting
        {
            Id = Guid.NewGuid(),
            NannyProfileId = nannyProfileId,
            JobPostingId = jobPostingId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

}
