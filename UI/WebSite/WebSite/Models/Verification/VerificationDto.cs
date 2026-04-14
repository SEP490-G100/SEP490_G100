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
    public string StatusLabel => Status switch { 2 => "Approved", 3 => "Rejected", _ => "Pending" };
    public string StatusClass => Status switch { 2 => "badge-active", 3 => "badge-inactive", _ => "badge-pending" };
}

public class VerificationRequestDetailDto
{
    public Guid     Id              { get; set; }
    public Guid     NannyProfileId  { get; set; }
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
    public string StatusLabel => Status switch { 2 => "Approved", 3 => "Rejected", _ => "Pending" };
    public string StatusClass => Status switch { 2 => "badge-active", 3 => "badge-inactive", _ => "badge-pending" };
    public string EducationLabel => EducationLevel switch
    {
        0 => "No formal education",
        1 => "High School",
        2 => "Associate Degree",
        3 => "Bachelor's Degree",
        4 => "Master's Degree",
        _ => "Other"
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
        1 => "Basic",
        2 => "Intermediate",
        3 => "Advanced",
        _ => "Not specified"
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
        1 => "Pending",
        2 => "Approved",
        3 => "Rejected",
        _ => "Not submitted"
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

    public string TypeLabel => DocumentType switch
    {
        (int)VerificationDocumentType.IdentityCard => "National ID",
        (int)VerificationDocumentType.DegreeCertificate => "Degree / Certificate",
        (int)VerificationDocumentType.HealthCertificate => "Health Certificate",
        _ => "Document"
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
