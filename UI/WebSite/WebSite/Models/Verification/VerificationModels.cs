using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Verification;

public class SubmitVerificationRequestViewModel
{
    [Required(ErrorMessage = "Bắt buộc phải tải lên tài liệu xác minh.")]
    public List<IFormFile> Files { get; set; } = new();

    // Read-only info for display on the form
    public string NannyFirstName { get; set; } = string.Empty;
    public string NannyLastName { get; set; } = string.Empty;
    public string NannyEmail { get; set; } = string.Empty;
    public string? NannyAvatarUrl { get; set; }
    public string? NannyCity { get; set; }
    public string? NannyAddress { get; set; }
    public string? PhoneNumber { get; set; }
}

public class VerificationRequestListViewModel
{
    public Guid Id { get; set; }
    public Guid NannyProfileId { get; set; }
    public int Status { get; set; } // 1=Pending, 2=Approved, 3=Rejected
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
