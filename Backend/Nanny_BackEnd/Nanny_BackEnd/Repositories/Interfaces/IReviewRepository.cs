using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IReviewRepository
{
    IQueryable<Review> GetQuery();
    Task<bool> ExistsAsync(Guid hiringRecordId, Guid reviewerUserId);
    Task<List<Review>> GetByRevieweeAsync(Guid revieweeUserId, int page, int pageSize);
    Task<int> CountByRevieweeAsync(Guid revieweeUserId);
    Task<double?> GetAverageRatingAsync(Guid revieweeUserId);
    Task<List<Review>> GetByReviewerAsync(Guid reviewerUserId);
    Task<List<Guid>> GetHiringRecordIdsByReviewerAsync(Guid reviewerUserId);
    void Add(Review review);
    Task SaveChangesAsync();
}
