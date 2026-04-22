using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Address;

namespace Nanny_BackEnd.Services.Interfaces;

public interface ILocationService
{
    Task<IReadOnlyList<ProvinceLocationDto>> GetLocationTreeAsync(CancellationToken cancellationToken = default);
    Task<List<LocationApproxSuggestion>> SuggestApproximateAsync(string query, int limit, CancellationToken cancellationToken = default);
}
