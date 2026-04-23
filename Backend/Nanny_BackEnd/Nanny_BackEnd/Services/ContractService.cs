using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class ContractService : IContractService
{
    private readonly IContractRepository _repo;

    public ContractService(IContractRepository repo) => _repo = repo;

    public async Task<ContractListResponseDto> GetMyContractsAsync(Guid userId)
    {
        var contracts = await _repo.GetContractsByUserIdAsync(userId);
        var result = new ContractListResponseDto();

        var displayContracts = contracts
            .GroupBy(c => c.HiringRecordId)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                    .ToList();

                var fallback = ordered[0];
                var own = ordered.FirstOrDefault(c => c.CreatedBy.HasValue && c.CreatedBy.Value == userId);
                var display = own ?? fallback;
                return new
                {
                    Contract = display,
                    HasOwnVersion = own != null,
                    IsOtherUserVersion = fallback.CreatedBy.HasValue && fallback.CreatedBy.Value != userId,
                    SortAt = display.UpdatedAt ?? display.CreatedAt
                };
            })
            .OrderByDescending(x => x.SortAt)
            .ToList();

        foreach (var entry in displayContracts)
        {
            var item = MapToListItem(entry.Contract);
            if (!entry.HasOwnVersion && entry.IsOtherUserVersion)
                item.PdfUrl = null;

            switch (entry.Contract.HiringRecord?.Status)
            {
                case 1:
                    result.Active.Add(item);
                    break;
                case 0:
                    result.Pending.Add(item);
                    break;
                default:
                    result.History.Add(item);
                    break;
            }
        }

        return result;
    }

    public async Task<ContractListItemDto> SaveContractStoragePdfAsync(
        Guid contractId,
        Guid userId,
        SaveContractStoragePdfRequestDto request)
    {
        if (request == null)
            throw new InvalidOperationException("Vui long cung cap thong tin file hop dong.");

        if (string.IsNullOrWhiteSpace(request.PdfUrl))
            throw new InvalidOperationException("Vui long cung cap duong dan file hop dong.");

        if (!Uri.TryCreate(request.PdfUrl.Trim(), UriKind.Absolute, out var pdfUri) ||
            !(string.Equals(pdfUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(pdfUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Duong dan file hop dong khong hop le.");
        }

        if (!string.Equals(Path.GetExtension(pdfUri.AbsolutePath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chi chap nhan file hop dong dinh dang PDF.");
        }

        var contract = await _repo.GetContractForUpdateAsync(contractId)
            ?? throw new KeyNotFoundException("Khong tim thay hop dong.");

        ResolveCurrentUserRole(contract, userId);
        var nowUtc = DateTime.UtcNow;
        var normalizedPdfUrl = request.PdfUrl.Trim();

        if (contract.CreatedBy.HasValue && contract.CreatedBy.Value != userId)
        {
            var personalContract = new Contract
            {
                Id = Guid.NewGuid(),
                HiringRecordId = contract.HiringRecordId,
                ContractTemplateId = null,
                ContractContent = string.IsNullOrWhiteSpace(contract.ContractContent)
                    ? "Hop dong da duoc luu duoi dang PDF."
                    : contract.ContractContent,
                SignedByParent = contract.SignedByParent,
                SignedByNanny = contract.SignedByNanny,
                SignedAt = contract.SignedAt,
                PdfUrl = normalizedPdfUrl,
                Status = contract.Status,
                CreatedAt = nowUtc,
                CreatedBy = userId,
                UpdatedAt = nowUtc,
                UpdatedBy = userId,
                IsDeleted = false,
                HiringRecord = contract.HiringRecord
            };

            _repo.AddContract(personalContract);
            await _repo.SaveChangesAsync();
            return MapToListItem(personalContract);
        }

        contract.PdfUrl = normalizedPdfUrl;
        contract.ContractTemplateId = null;
        if (string.IsNullOrWhiteSpace(contract.ContractContent))
            contract.ContractContent = "Hop dong da duoc luu duoi dang PDF.";
        contract.CreatedBy ??= userId;
        contract.UpdatedAt = nowUtc;
        contract.UpdatedBy = userId;

        await _repo.SaveChangesAsync();
        return MapToListItem(contract);
    }

    private static string ResolveCurrentUserRole(Contract contract, Guid userId)
    {
        var parentUserId = contract.HiringRecord?.ParentProfile?.UserId;
        var nannyUserId = contract.HiringRecord?.NannyProfile?.UserId;

        if (parentUserId.HasValue && parentUserId.Value == userId)
            return "Parent";
        if (nannyUserId.HasValue && nannyUserId.Value == userId)
            return "Nanny";

        throw new UnauthorizedAccessException("Ban khong co quyen truy cap hop dong nay.");
    }

    private static ContractListItemDto MapToListItem(Contract contract)
    {
        var hiring = contract.HiringRecord;
        var posting = hiring?.JobApplication?.JobPosting;
        var parentUser = hiring?.ParentProfile?.User;
        var nannyUser = hiring?.NannyProfile?.User;

        return new ContractListItemDto
        {
            ContractId = contract.Id,
            HiringRecordId = contract.HiringRecordId,
            JobTitle = posting?.Title ?? "Cong viec",
            ParentName = GetDisplayName(parentUser),
            ParentAvatar = parentUser?.AvatarUrl,
            NannyName = GetDisplayName(nannyUser),
            NannyAvatar = nannyUser?.AvatarUrl,
            StartDate = hiring?.StartDate ?? DateOnly.MinValue,
            EndDate = hiring?.EndDate,
            HiringStatus = hiring?.Status ?? contract.Status,
            ContractStatus = contract.Status,
            PdfUrl = contract.PdfUrl,
            CreatedAt = contract.CreatedAt
        };
    }

    private static string GetDisplayName(User? user)
    {
        if (user == null)
            return "Nguoi dung";

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}
