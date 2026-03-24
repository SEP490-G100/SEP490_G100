namespace WebSite.Models.Verification;

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

public class VerificationDocumentDto
{
    public Guid    Id           { get; set; }
    public int     DocumentType { get; set; }
    public string  DocumentUrl  { get; set; } = "";
    public string  FileName     { get; set; } = "";
    public int?    FileSize     { get; set; }

    public string TypeLabel => DocumentType switch
    {
        1 => "National ID",
        2 => "Degree / Certificate",
        3 => "Background Check",
        4 => "Health Certificate",
        _ => "Document"
    };

    public string TypeIcon => DocumentType switch
    {
        1 => "badge",
        2 => "school",
        3 => "verified_user",
        4 => "medical_services",
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
