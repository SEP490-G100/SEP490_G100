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
    public bool SignedByParent { get; set; }
    public bool SignedByNanny { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContractListResponseViewModel
{
    public List<ContractListItemViewModel> Active { get; set; } = new();
    public List<ContractListItemViewModel> Pending { get; set; } = new();
    public List<ContractListItemViewModel> History { get; set; } = new();
}

public class ContractDetailViewModel
{
    public Guid ContractId { get; set; }
    public Guid HiringRecordId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? ContractDuration { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string? ParentAvatar { get; set; }
    public string? ParentPhone { get; set; }
    public string? ParentEmail { get; set; }
    public string? ParentAddress { get; set; }
    public string NannyName { get; set; } = string.Empty;
    public string? NannyAvatar { get; set; }
    public string? NannyPhone { get; set; }
    public string? NannyEmail { get; set; }
    public string? NannyAddress { get; set; }
    public string ContractContent { get; set; } = string.Empty;
    public bool SignedByParent { get; set; }
    public bool SignedByNanny { get; set; }
    public DateTime? SignedAt { get; set; }
    public int HiringStatus { get; set; }
    public int ContractStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContractDetailPageViewModel
{
    public Guid ContractId { get; set; }
}
