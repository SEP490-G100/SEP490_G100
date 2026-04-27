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
    public string StatusLabel => Status switch
    {
        2 => "Đã duyệt",
        3 => "Từ chối",
        1 => "Đang chờ",
        _ => "Đang chờ"
    };
    public string StatusClass => Status switch { 2 => "badge-active", 3 => "badge-inactive", _ => "badge-pending" };
    public string RequestTypeLabel => RequestType switch
    {
        2 => "Giấy khám sức khỏe",
        3 => "Bằng cấp / Chứng chỉ",
        _ => "Căn cước công dân"
    };
    public string IssueDateLabel => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("dd/MM/yyyy") : "-";
    public string ExpiryDateLabel => IssueDateLabel;
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
    public string StatusLabel => Status switch
    {
        2 => "Đã duyệt",
        3 => "Từ chối",
        1 => "Đang chờ",
        _ => "Đang chờ"
    };
    public string StatusClass => Status switch { 2 => "badge-active", 3 => "badge-inactive", _ => "badge-pending" };
    public string RequestTypeLabel => RequestType switch
    {
        2 => "Giấy khám sức khỏe",
        3 => "Bằng cấp / Chứng chỉ",
        _ => "Căn cước công dân"
    };
    public string EducationLabel => EducationLevel switch
    {
        0 => "Trung học",
        1 => "Cao đẳng",
        2 => "Đại học",
        3 => "Thạc sĩ",
        4 => "Khác",
        _ => "Chưa cập nhật"
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
        1 => "Cơ bản",
        2 => "Trung cấp",
        3 => "Nâng cao",
        _ => "Chưa cập nhật"
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
        1 => "Đang chờ duyệt",
        2 => "Đã duyệt",
        3 => "Từ chối",
        _ => "Chưa nộp"
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
        (int)VerificationDocumentType.IdentityCard => "Căn cước công dân",
        (int)VerificationDocumentType.DegreeCertificate => "Bằng cấp / Chứng chỉ",
        (int)VerificationDocumentType.HealthCertificate => "Giấy khám sức khỏe",
        _ => "Tài liệu"
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
