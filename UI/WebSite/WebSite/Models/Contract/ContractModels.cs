namespace WebSite.Models.Contract;

public class ContractListItemViewModel
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

public class ContractListResponseViewModel
{
    public List<ContractListItemViewModel> Active { get; set; } = new();
    public List<ContractListItemViewModel> Pending { get; set; } = new();
    public List<ContractListItemViewModel> History { get; set; } = new();
}
