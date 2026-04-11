using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Review;

public class CreateReviewRequest
{
    [Required(ErrorMessage = "HiringRecordId không được để trống.")]
    public Guid HiringRecordId { get; set; }

    [Range(1, 5, ErrorMessage = "Rating phải từ 1 đến 5.")]
    public int Rating { get; set; }

    [StringLength(2000, ErrorMessage = "Nhận xét tối đa 2000 ký tự.")]
    public string? Comment { get; set; }
}
