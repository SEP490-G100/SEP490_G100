namespace WebSite.Models.Verification;

using WebSite.Enums;

public class VerificationRequestListResponse
{
    public List<VerificationRequestListDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page      { get; set; }
    public int PageSize  { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class VerificationRequestListDto
{
    public Guid     Id              { get; set; }
    public Guid     NannyProfileId  { get; set; }
    public int      RequestType     { get; set; }
    public List<int> DocumentTypes  { get; set; } = new();
    public DateTime? ExpiryDate     { get; set; }
    public int      Status          { get; set; }   // 1=Pending, 2=Approved, 3=Rejected
    public DateTime CreatedAt       { get; set; }
    public DateTime? ReviewedAt     { get; set; }
    public Guid?    ReviewedBy      { get; set; }
    public string?  ReviewedByName  { get; set; }
    public string?  RejectionReason { get; set; }

    public Guid    NannyUserId    { get; set; }
    public string  NannyFirstName { get; set; } = "";
    public string  NannyLastName  { get; set; } = "";
    public string  NannyEmail     { get; set; } = "";
    public string? NannyAvatarUrl { get; set; }
    public string? NannyCity      { get; set; }

    public string FullName    => $"{NannyFirstName} {NannyLastName}".Trim();
    public string StatusLabel => Status switch { 2 => "Da duyet", 3 => "Da tu choi", _ => "Dang cho" };
    public string StatusClass => Status switch { 2 => "badge-active", 3 => "badge-inactive", _ => "badge-pending" };
    public string RequestTypeLabel => RequestType == 2 ? "Giay kham suc khoe" : "Ho so xac minh";
    public string ExpiryDateLabel => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("dd/MM/yyyy") : "-";
}

public class VerificationRequestDetailDto
{
    public Guid     Id              { get; set; }
    public Guid     NannyProfileId  { get; set; }
    public int      RequestType     { get; set; }
    public int      Status          { get; set; }
    public string?  RejectionReason { get; set; }
    public DateTime CreatedAt       { get; set; }
    public DateTime? ReviewedAt     { get; set; }
    public string?  ReviewedByName  { get; set; }

    public Guid    NannyUserId      { get; set; }
    public string  NannyFirstName   { get; set; } = "";
    public string  NannyLastName    { get; set; } = "";
    public string  NannyEmail       { get; set; } = "";
    public string? NannyPhoneNumber { get; set; }
    public string? NannyAvatarUrl   { get; set; }
    public string? NannyCity        { get; set; }
    public string? NannyAddress     { get; set; }
    public string? NannyDistrict    { get; set; }
    public string? NannyWard        { get; set; }
    public int?    NannyGender      { get; set; }
    public DateOnly? NannyDateOfBirth { get; set; }

    public string? Bio                { get; set; }
    public int?    YearsOfExperience  { get; set; }
    public int?    EducationLevel     { get; set; }
    public int     VerificationStatus { get; set; }
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public int     SalaryType         { get; set; }
    public int?    MaxTravelDistance  { get; set; }

    public List<VerificationSkillDto> Skills { get; set; } = new();
    public List<VerificationCertificateDto> Certificates { get; set; } = new();
    public List<VerificationDocumentDto> Documents { get; set; } = new();

    public string FullName    => $"{NannyFirstName} {NannyLastName}".Trim();
    public string StatusLabel => Status switch { 2 => "Da duyet", 3 => "Da tu choi", _ => "Dang cho" };
    public string StatusClass => Status switch { 2 => "badge-active", 3 => "badge-inactive", _ => "badge-pending" };
    public string RequestTypeLabel => RequestType == 2 ? "Giay kham suc khoe" : "Ho so xac minh";
    public string EducationLabel => EducationLevel switch
    {
        0 => "Khong chinh quy",
        1 => "Trung hoc pho thong",
        2 => "Cao dang",
        3 => "Dai hoc",
        4 => "Thac si",
        _ => "Khac"
    };
}

public class VerificationSkillDto
{
    public Guid   Id               { get; set; }
    public Guid   SkillId          { get; set; }
    public string SkillName        { get; set; } = "";
    public string SkillCategory    { get; set; } = "";
    public int?   ProficiencyLevel { get; set; }

    public string ProficiencyLabel => ProficiencyLevel switch
    {
        1 => "Co ban",
        2 => "Trung binh",
        3 => "Nang cao",
        _ => "Chua xac dinh"
    };
}

public class VerificationCertificateDto
{
    public Guid      Id                  { get; set; }
    public string    Name                { get; set; } = "";
    public string?   IssuingOrganization { get; set; }
    public DateOnly? IssueDate           { get; set; }
    public DateOnly? ExpiryDate          { get; set; }
    public string?   CertificateUrl      { get; set; }
    public int       VerificationStatus  { get; set; }

    public string VerificationStatusLabel => VerificationStatus switch
    {
        1 => "Dang cho",
        2 => "Da duyet",
        3 => "Da tu choi",
        _ => "Chua gui"
    };

    public string VerificationStatusClass => VerificationStatus switch
    {
        1 => "badge-pending",
        2 => "badge-active",
        3 => "badge-inactive",
        _ => "bg-gray-100 text-gray-500"
    };
}

public class VerificationDocumentDto
{
    public Guid    Id           { get; set; }
    public int     DocumentType { get; set; }
    public string  DocumentUrl  { get; set; } = "";
    public string  FileName     { get; set; } = "";
    public int?    FileSize     { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public string TypeLabel => DocumentType switch
    {
        (int)VerificationDocumentType.IdentityCard => "Can cuoc cong dan",
        (int)VerificationDocumentType.DegreeCertificate => "Bang cap / Chung chi",
        (int)VerificationDocumentType.HealthCertificate => "Giay kham suc khoe",
        _ => "Tai lieu"
    };

    public string TypeIcon => DocumentType switch
    {
        (int)VerificationDocumentType.IdentityCard => "badge",
        (int)VerificationDocumentType.DegreeCertificate => "school",
        (int)VerificationDocumentType.HealthCertificate => "medical_services",
        _ => "description"
    };

    public string FileSizeLabel => FileSize.HasValue
        ? FileSize.Value >= 1024 * 1024
            ? $"{FileSize.Value / 1024 / 1024.0:F1} MB"
            : $"{FileSize.Value / 1024.0:F0} KB"
        : "";
}

public class ReviewVerificationRequest
{
    public int     Action          { get; set; }   // 2=Approve, 3=Reject
    public string? RejectionReason { get; set; }
}
