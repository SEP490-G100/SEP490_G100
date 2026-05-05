namespace WebSite.Models.Hiring;

public class HiringRecordListItemViewModel
{
    public Guid HiringRecordId { get; set; }
    public Guid? ContractId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string? ParentAvatar { get; set; }
    public string NannyName { get; set; } = string.Empty;
    public string? NannyAvatar { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int HiringStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}
