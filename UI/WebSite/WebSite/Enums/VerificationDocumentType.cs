using System.ComponentModel.DataAnnotations;

namespace WebSite.Enums;

public enum VerificationDocumentType
{
    [Display(Name = "Căn cước công dân")]
    IdentityCard = 1,

    [Display(Name = "Bằng cấp/Chứng chỉ")]
    DegreeCertificate = 2,

    [Display(Name = "Giấy khám sức khỏe")]
    HealthCertificate = 3
}
