using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ReviewRepository
{
    private readonly Sep490NannyDbContext _db;

    public ReviewRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public IQueryable<Review> GetQuery() => _db.Reviews.AsQueryable();

    public async Task<bool> ExistsAsync(Guid hiringRecordId, Guid reviewerUserId)
        => await _db.Reviews.AnyAsync(r =>
            r.HiringRecordId == hiringRecordId &&
            r.ReviewerUserId == reviewerUserId &&
            !r.IsDeleted);

    public async Task<List<Review>> GetByRevieweeAsync(Guid revieweeUserId, int page, int pageSize)
        => await _db.Reviews
            .Where(r => r.RevieweeUserId == revieweeUserId && !r.IsDeleted && r.IsVisible)
            .Include(r => r.ReviewerUser)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<int> CountByRevieweeAsync(Guid revieweeUserId)
        => await _db.Reviews.CountAsync(r =>
            r.RevieweeUserId == revieweeUserId && !r.IsDeleted && r.IsVisible);

    public async Task<double?> GetAverageRatingAsync(Guid revieweeUserId)
        => await _db.Reviews
            .Where(r => r.RevieweeUserId == revieweeUserId && !r.IsDeleted && r.IsVisible)
            .Select(r => (double?)r.Rating)
            .AverageAsync();

    public void Add(Review review) => _db.Reviews.Add(review);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
