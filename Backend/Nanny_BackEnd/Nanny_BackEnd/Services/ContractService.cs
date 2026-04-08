using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class ContractService
{
    private readonly ContractRepository _repo;

    public ContractService(ContractRepository repo) => _repo = repo;

    // ─── Danh sách hợp đồng phân theo 3 tab ─────────────────────────────────
    public async Task<ContractListResponseDto> GetMyContractsAsync(Guid userId)
    {
        var contracts = await _repo.GetContractsByUserIdAsync(userId);

        var result = new ContractListResponseDto();
        foreach (var c in contracts)
        {
            var item = MapToListItem(c);
            switch (c.HiringRecord?.Status)
            {
                case 1: result.Active.Add(item); break;
                case 0: result.Pending.Add(item); break;
                default: result.History.Add(item); break; // 2=Declined, 3=Cancelled, 4=Completed
            }
        }
        return result;
    }

    // ─── Chi tiết hợp đồng với kiểm tra quyền truy cập ──────────────────────
    public async Task<ContractDetailDto> GetContractDetailAsync(Guid contractId, Guid userId)
    {
        var contract = await _repo.GetContractDetailAsync(contractId)
            ?? throw new KeyNotFoundException("Khong tim thay hop dong.");

        var parentUserId = contract.HiringRecord?.ParentProfile?.UserId;
        var nannyUserId  = contract.HiringRecord?.NannyProfile?.UserId;
        if (userId != parentUserId && userId != nannyUserId)
            throw new UnauthorizedAccessException("Ban khong co quyen xem hop dong nay.");

        return MapToDetail(contract);
    }

    // ─── Map helpers ─────────────────────────────────────────────────────────
    private static ContractListItemDto MapToListItem(Contract c)
    {
        var hiring  = c.HiringRecord;
        var parent  = hiring?.ParentProfile?.User;
        var nanny   = hiring?.NannyProfile?.User;
        var posting = hiring?.JobApplication?.JobPosting;

        return new ContractListItemDto
        {
            ContractId     = c.Id,
            HiringRecordId = hiring?.Id ?? Guid.Empty,
            JobTitle       = posting?.Title ?? "—",
            ParentName     = GetDisplayName(parent),
            ParentAvatar   = parent?.AvatarUrl,
            NannyName      = GetDisplayName(nanny),
            NannyAvatar    = nanny?.AvatarUrl,
            StartDate      = hiring?.StartDate ?? DateOnly.MinValue,
            EndDate        = hiring?.EndDate,
            HiringStatus   = hiring?.Status ?? 0,
            ContractStatus = c.Status,
            SignedByParent = c.SignedByParent,
            SignedByNanny  = c.SignedByNanny,
            CreatedAt      = c.CreatedAt
        };
    }

    private static ContractDetailDto MapToDetail(Contract c)
    {
        var hiring  = c.HiringRecord;
        var parent  = hiring?.ParentProfile?.User;
        var nanny   = hiring?.NannyProfile?.User;
        var posting = hiring?.JobApplication?.JobPosting;

        return new ContractDetailDto
        {
            ContractId       = c.Id,
            HiringRecordId   = hiring?.Id ?? Guid.Empty,
            TemplateName     = c.ContractTemplate?.Name ?? string.Empty,

            JobTitle         = posting?.Title ?? "—",
            StartDate        = hiring?.StartDate ?? DateOnly.MinValue,
            EndDate          = hiring?.EndDate,
            ContractDuration = hiring?.ContractDuration,

            ParentName       = GetDisplayName(parent),
            ParentAvatar     = parent?.AvatarUrl,
            ParentPhone      = parent?.PhoneNumber,
            ParentEmail      = parent?.Email,
            ParentAddress    = parent?.Address,

            NannyName        = GetDisplayName(nanny),
            NannyAvatar      = nanny?.AvatarUrl,
            NannyPhone       = nanny?.PhoneNumber,
            NannyEmail       = nanny?.Email,
            NannyAddress     = nanny?.Address,

            ContractContent  = c.ContractContent,
            SignedByParent   = c.SignedByParent,
            SignedByNanny    = c.SignedByNanny,
            SignedAt         = c.SignedAt,
            HiringStatus     = hiring?.Status ?? 0,
            ContractStatus   = c.Status,
            CreatedAt        = c.CreatedAt
        };
    }

    private static string GetDisplayName(User? user)
    {
        if (user == null) return "Nguoi dung";
        var full = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(full) ? user.Email : full;
    }
}
