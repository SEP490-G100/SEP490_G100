using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Review;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class ReviewService
{
    private readonly ReviewRepository _reviewRepo;
    private readonly Sep490NannyDbContext _db;


    public ReviewService(ReviewRepository reviewRepo, Sep490NannyDbContext db)
    {
        _reviewRepo = reviewRepo;
        _db = db;
    }

    /// <summary>Tạo đánh giá từ Parent cho Nanny sau khi hợp đồng hoàn thành.</summary>
    public async Task<ReviewDto> CreateReviewAsync(Guid parentUserId, CreateReviewRequest request)
    {
        var hiringRecord = await _db.HiringRecords
            .Include(h => h.ParentProfile)
            .Include(h => h.NannyProfile)
                .ThenInclude(n => n.User)
            .FirstOrDefaultAsync(h => h.Id == request.HiringRecordId && !h.IsDeleted)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng thuê.");

        if (hiringRecord.ParentProfile.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền đánh giá hợp đồng này.");

        if (hiringRecord.Status != (int)HiringRecordStatus.Completed)
            throw new InvalidOperationException("Chỉ có thể đánh giá sau khi hợp đồng hoàn thành.");

        if (await _reviewRepo.ExistsAsync(request.HiringRecordId, parentUserId))
            throw new InvalidOperationException("Bạn đã đánh giá hợp đồng này rồi.");

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerUserId = parentUserId,
            RevieweeUserId = hiringRecord.NannyProfile.UserId,
            HiringRecordId = request.HiringRecordId,
            Rating = request.Rating,
            Comment = request.Comment?.Trim(),
            IsVisible = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = parentUserId,
            IsDeleted = false,
        };

        _reviewRepo.Add(review);
        await _reviewRepo.SaveChangesAsync();

        var reviewer = await _db.Users.FirstAsync(u => u.Id == parentUserId);
        return MapToDto(review, reviewer);
    }

    /// <summary>Lấy danh sách đánh giá của một Nanny (public).</summary>
    public async Task<ReviewListResponse> GetNannyReviewsAsync(Guid nannyUserId, int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var items = await _reviewRepo.GetByRevieweeAsync(nannyUserId, page, pageSize);
        var total = await _reviewRepo.CountByRevieweeAsync(nannyUserId);
        var average = await _reviewRepo.GetAverageRatingAsync(nannyUserId);

        return new ReviewListResponse
        {
            Items = items.Select(r => MapToDto(r, r.ReviewerUser)).ToList(),
            TotalCount = total,
            AverageRating = average.HasValue ? Math.Round(average.Value, 1) : null,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <summary>Lấy các HiringRecord đã hoàn thành mà Parent chưa đánh giá.</summary>
    public async Task<List<ReviewableHiringRecordDto>> GetReviewableHiringRecordsAsync(Guid parentUserId)
    {
        var reviewedIds = await _db.Reviews
            .Where(r => r.ReviewerUserId == parentUserId && !r.IsDeleted)
            .Select(r => r.HiringRecordId)
            .ToListAsync();

        var records = await _db.HiringRecords
            .Include(h => h.NannyProfile).ThenInclude(n => n.User)
            .Include(h => h.ParentProfile)
            .Where(h =>
                h.ParentProfile.UserId == parentUserId &&
                h.Status == (int)HiringRecordStatus.Completed &&
                !h.IsDeleted &&
                !reviewedIds.Contains(h.Id))
            .OrderByDescending(h => h.EndDate)
            .ToListAsync();

        return records.Select(h => new ReviewableHiringRecordDto
        {
            HiringRecordId = h.Id,
            NannyName = $"{h.NannyProfile.User.FirstName} {h.NannyProfile.User.LastName}".Trim(),
            NannyAvatarUrl = h.NannyProfile.User.AvatarUrl,
            StartDate = h.StartDate,
            EndDate = h.EndDate,
        }).ToList();
    }

    /// <summary>Lấy tất cả đánh giá mà Parent đã gửi.</summary>
    public async Task<List<MyReviewDto>> GetMyReviewsAsync(Guid reviewerUserId)
    {
        var reviews = await _reviewRepo.GetByReviewerAsync(reviewerUserId);
        return reviews.Select(r => new MyReviewDto
        {
            Id = r.Id,
            Rating = r.Rating,
            Comment = r.Comment,
            NannyName = $"{r.RevieweeUser.FirstName} {r.RevieweeUser.LastName}".Trim(),
            NannyAvatarUrl = r.RevieweeUser.AvatarUrl,
            CreatedAt = r.CreatedAt,
        }).ToList();
    }

    private static ReviewDto MapToDto(Review review, User reviewer) => new()
    {
        Id = review.Id,
        Rating = review.Rating,
        Comment = review.Comment,
        ReviewerName = $"{reviewer.FirstName} {reviewer.LastName}".Trim(),
        ReviewerAvatarUrl = reviewer.AvatarUrl,
        CreatedAt = review.CreatedAt,
    };
}
