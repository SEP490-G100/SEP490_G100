using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Review;

public class CreateReviewViewModel
{
    public Guid HiringRecordId { get; set; }

    // Hiển thị (không post)
    public string NannyName { get; set; } = "";
    public string? NannyAvatarUrl { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5.")]
    public int Rating { get; set; }

    [StringLength(2000, ErrorMessage = "Nhận xét tối đa 2000 ký tự.")]
    public string? Comment { get; set; }
}

public class ReviewableHiringRecordViewModel
{
    public Guid HiringRecordId { get; set; }
    public string NannyName { get; set; } = "";
    public string? NannyAvatarUrl { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class ReviewHistoryViewModel
{
    public List<ReviewableHiringRecordViewModel> Reviewable { get; set; } = [];
}
