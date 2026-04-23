namespace Nanny_BackEnd.DTOs.Hiring;

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
    public int HiringStatus { get; set; }
    public int ContractStatus { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveContractStoragePdfRequestDto
{
    public string PdfUrl { get; set; } = string.Empty;
}

public class ContractListResponseDto
{
    public List<ContractListItemDto> Active { get; set; } = new();
    public List<ContractListItemDto> Pending { get; set; } = new();
    public List<ContractListItemDto> History { get; set; } = new();
}
