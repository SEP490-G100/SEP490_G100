namespace Nanny_BackEnd.DTOs.Review;

public class ReviewableHiringRecordDto
{
    public Guid HiringRecordId { get; set; }
    public string NannyName { get; set; } = "";
    public string? NannyAvatarUrl { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
