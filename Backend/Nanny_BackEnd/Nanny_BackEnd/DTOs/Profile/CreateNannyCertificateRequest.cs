namespace Nanny_BackEnd.DTOs.Profile;

public class CreateNannyCertificateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? IssuingOrganization { get; set; }
    public string? CertificateUrl { get; set; }
}
