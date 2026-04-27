using System;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Hiring;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IContractService
{
    Task<ContractListResponseDto> GetMyContractsAsync(Guid userId);
    Task<ContractListItemDto> SaveContractStoragePdfAsync(Guid contractId, Guid userId, SaveContractStoragePdfRequestDto request);
    Task<List<ContractTemplateOptionDto>> GetActiveContractTemplatesAsync();
    Task<ContractTemplatePreviewDto> GetContractTemplatePreviewAsync(Guid templateId);
    Task<ContractDetailDto> GetContractDetailAsync(Guid userId, Guid? contractId, Guid? hiringRecordId);
    Task<ContractDetailDto> ParentConfirmInfoAsync(Guid contractId, Guid userId, ContractParentFillRequestDto request);
    Task<ContractDetailDto> NannyConfirmInfoAsync(Guid contractId, Guid userId, ContractNannyFillRequestDto request);
    Task<ContractDetailDto> ParentFinalConfirmAsync(Guid contractId, Guid userId);
    Task<(byte[] Content, string FileName)> DownloadContractPdfAsync(Guid contractId, Guid userId);
}
