using System;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Hiring;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IContractService
{
    Task<ContractListResponseDto> GetMyContractsAsync(Guid userId);
    Task<ContractDetailDto> GetContractDetailAsync(Guid contractId, Guid userId);
    Task<ContractDetailDto> SaveDraftByParentAsync(Guid contractId, Guid userId, ContractUpsertRequestDto request);
    Task<ContractDetailDto> FillByNannyAsync(Guid contractId, Guid userId, ContractUpsertRequestDto request);
    Task<ContractDetailDto> FinalConfirmByParentAsync(Guid contractId, Guid userId);
}
