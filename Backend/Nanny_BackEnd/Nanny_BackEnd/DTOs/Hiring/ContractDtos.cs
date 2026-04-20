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
    public bool SignedByParent { get; set; }
    public bool SignedByNanny { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContractDetailDto
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

    public string CurrentUserRole { get; set; } = "Unknown";
    public List<string> EditableFields { get; set; } = new();
    public Dictionary<string, string> DraftFieldValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool CanSubmitByNanny { get; set; }
    public bool CanFinalConfirmByParent { get; set; }
    public List<ContractScheduleSlotDto> ScheduleSlots { get; set; } = new();
}

public class ContractScheduleSlotDto
{
    public int DayOfWeek { get; set; }
    public int TimeSlot { get; set; }
}

public class ContractUpsertRequestDto
{
    public Dictionary<string, string?> FieldValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PaymentMethod { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public bool IsSignedByActor { get; set; }
}

public class ContractListResponseDto
{
    public List<ContractListItemDto> Active { get; set; } = new();
    public List<ContractListItemDto> Pending { get; set; } = new();
    public List<ContractListItemDto> History { get; set; } = new();
}
