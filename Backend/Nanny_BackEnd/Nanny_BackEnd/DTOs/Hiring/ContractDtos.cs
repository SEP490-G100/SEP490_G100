namespace Nanny_BackEnd.DTOs.Hiring;

// ─── Task 1: DTOs cho Contract List & Detail ─────────────────────────────────

public class ContractListItemDto
{
    public Guid ContractId { get; set; }
    public Guid HiringRecordId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string? ParentAvatar { get; set; }
    public string NannyName { get; set; } = string.Empty;
    public string? NannyAvatar { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    /// <summary>
    /// HiringRecord.Status: 0=Pending, 1=Active, 2=Declined, 3=Cancelled, 4=Completed
    /// </summary>
    public int HiringStatus { get; set; }
    /// <summary>
    /// Contract.Status: 0=Draft, 1=Signed
    /// </summary>
    public int ContractStatus { get; set; }
    public bool SignedByParent { get; set; }
    public bool SignedByNanny { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContractDetailDto
{
    // Thông tin định danh
    public Guid ContractId { get; set; }
    public Guid HiringRecordId { get; set; }
    public string TemplateName { get; set; } = string.Empty;

    // Công việc
    public string JobTitle { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? ContractDuration { get; set; }

    // Phụ huynh
    public string ParentName { get; set; } = string.Empty;
    public string? ParentAvatar { get; set; }
    public string? ParentPhone { get; set; }
    public string? ParentEmail { get; set; }
    public string? ParentAddress { get; set; }

    // Bảo mẫu
    public string NannyName { get; set; } = string.Empty;
    public string? NannyAvatar { get; set; }
    public string? NannyPhone { get; set; }
    public string? NannyEmail { get; set; }
    public string? NannyAddress { get; set; }

    // Nội dung hợp đồng (đã render token)
    public string ContractContent { get; set; } = string.Empty;

    // Trạng thái ký
    public bool SignedByParent { get; set; }
    public bool SignedByNanny { get; set; }
    public DateTime? SignedAt { get; set; }
    public int HiringStatus { get; set; }
    public int ContractStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContractListResponseDto
{
    public List<ContractListItemDto> Active { get; set; } = new();
    public List<ContractListItemDto> Pending { get; set; } = new();
    public List<ContractListItemDto> History { get; set; } = new();
}
