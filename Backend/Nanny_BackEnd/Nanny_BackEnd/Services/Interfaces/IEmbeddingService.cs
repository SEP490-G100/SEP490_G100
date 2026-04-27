using System;
using System.Threading.Tasks;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IEmbeddingService
{
    Task EmbedNannyAsync(Guid nannyProfileId);
    Task EmbedJobAsync(Guid jobId);
    Task<int> EmbedAllPendingNanniesAsync();
    Task<int> EmbedAllPendingJobsAsync();
    Task<int> EmbedAllNanniesForceAsync();
    Task<int> EmbedAllJobsForceAsync();
}
