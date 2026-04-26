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

public class ContractDetailDto
{
    public Guid ContractId { get; set; }
    public Guid HiringRecordId { get; set; }
    public Guid? ContractTemplateId { get; set; }
    public string ContractContent { get; set; } = string.Empty;
    public int ContractStatus { get; set; }
    public bool SignedByParent { get; set; }
    public bool SignedByNanny { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public string ParentEmail { get; set; } = string.Empty;
    public string NannyName { get; set; } = string.Empty;
    public string NannyPhone { get; set; } = string.Empty;
    public string NannyEmail { get; set; } = string.Empty;
    public string CurrentUserRole { get; set; } = string.Empty;
    public bool CanParentConfirmInfo { get; set; }
    public bool CanNannyConfirmInfo { get; set; }
    public bool CanParentFinalConfirm { get; set; }
    public bool IsReadOnly { get; set; }
}

public class ContractParentFillRequestDto
{
    public string ParentName { get; set; } = string.Empty;
    public string ParentDob { get; set; } = string.Empty;
    public string ParentIdentityNumber { get; set; } = string.Empty;
    public string ParentIdentityIssueDate { get; set; } = string.Empty;
    public string ParentIdentityIssuePlace { get; set; } = string.Empty;
    public string ParentPermanentAddress { get; set; } = string.Empty;
    public string ParentCurrentAddress { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public string ParentEmail { get; set; } = string.Empty;
    public string ContractDurationMonths { get; set; } = string.Empty;
    public string ProbationStartDate { get; set; } = string.Empty;
    public string ProbationEndDate { get; set; } = string.Empty;
    public string WorkAddress { get; set; } = string.Empty;
    public string SalaryAmount { get; set; } = string.Empty;
    public string ProbationSalaryAmount { get; set; } = string.Empty;
    public string AllowanceAmount { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string SalaryReceivedDate { get; set; } = string.Empty;
    public string MealPerDay { get; set; } = string.Empty;
}

public class ContractNannyFillRequestDto
{
    public string NannyName { get; set; } = string.Empty;
    public string NannyDob { get; set; } = string.Empty;
    public string NannyIdentityNumber { get; set; } = string.Empty;
    public string NannyIdentityIssueDate { get; set; } = string.Empty;
    public string NannyIdentityIssuePlace { get; set; } = string.Empty;
    public string NannyPermanentAddress { get; set; } = string.Empty;
    public string NannyCurrentAddress { get; set; } = string.Empty;
    public string NannyPhone { get; set; } = string.Empty;
}
