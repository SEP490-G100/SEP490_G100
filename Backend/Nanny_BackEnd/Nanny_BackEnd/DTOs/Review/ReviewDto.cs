namespace Nanny_BackEnd.DTOs.Review;

public class ReviewDto
{
    public Guid Id { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string ReviewerName { get; set; } = "";
    public string? ReviewerAvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReviewListResponse
{
    public List<ReviewDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public double? AverageRating { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
