namespace WebSite.Models.Verification;

public class SubmitVerificationRequestViewModel
{
    public List<IFormFile> IdentityCardFiles { get; set; } = new();
    public List<IFormFile> CertificateFiles { get; set; } = new();
    public List<IFormFile> HealthCertificateFiles { get; set; } = new();

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
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public string? RejectionReason { get; set; }
}
