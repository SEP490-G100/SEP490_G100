using System;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Hiring;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IContractService
{
    Task<ContractListResponseDto> GetMyContractsAsync(Guid userId);
    Task<ContractListItemDto> SaveContractStoragePdfAsync(Guid contractId, Guid userId, SaveContractStoragePdfRequestDto request);
}
