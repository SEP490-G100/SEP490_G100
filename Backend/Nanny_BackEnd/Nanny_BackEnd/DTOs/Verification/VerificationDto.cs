namespace Nanny_BackEnd.DTOs.Verification;

/// <summary>DTO for list view of verification requests</summary>
public class VerificationRequestListDto
{
    public Guid Id { get; set; }
    public Guid NannyProfileId { get; set; }
    public int Status { get; set; }                // 0=Pending, 1=Approved, 2=Rejected
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Nanny info (from User via NannyProfile)
    public Guid NannyUserId { get; set; }
    public string NannyFirstName { get; set; } = null!;
    public string NannyLastName { get; set; } = null!;
    public string NannyEmail { get; set; } = null!;
    public string? NannyAvatarUrl { get; set; }
    public string? NannyCity { get; set; }
}

/// <summary>DTO for detail view of a single verification request</summary>
public class VerificationRequestDetailDto
{
    public Guid Id { get; set; }
    public Guid NannyProfileId { get; set; }
    public int Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }

    // Nanny info
    public Guid NannyUserId { get; set; }
    public string NannyFirstName { get; set; } = null!;
    public string NannyLastName { get; set; } = null!;
    public string NannyEmail { get; set; } = null!;
    public string? NannyPhoneNumber { get; set; }
    public string? NannyAvatarUrl { get; set; }
    public string? NannyCity { get; set; }

    // NannyProfile info
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public int? EducationLevel { get; set; }
    public int VerificationStatus { get; set; }

    // Documents
    public List<VerificationDocumentDto> Documents { get; set; } = new();
}

public class VerificationDocumentDto
{
    public Guid Id { get; set; }
    public int DocumentType { get; set; }
    public string DocumentUrl { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public int? FileSize { get; set; }
}

/// <summary>Paginated list response</summary>
public class VerificationRequestListResponse
{
    public List<VerificationRequestListDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>Request body for Approve/Reject action</summary>
public class ReviewVerificationRequest
{
    /// <summary>1 = Approved, 2 = Rejected</summary>
    public int Action { get; set; }

    /// <summary>Required when Action = 2 (Rejected)</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Id of the moderator reviewing (should come from JWT in real scenarios)</summary>
    public Guid? ReviewedBy { get; set; }
}
