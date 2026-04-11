using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Data;

/// <summary>
/// Excludes Embedding / EmbeddingUpdatedAt from all regular EF Core queries.
/// These large NVARCHAR(MAX) columns are read/written exclusively via raw SQL
/// in RecommendationRepository to avoid transferring ~18 KB per row on every search.
/// </summary>
public partial class Sep490NannyDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NannyProfile>()
            .Ignore(n => n.Embedding)
            .Ignore(n => n.EmbeddingUpdatedAt);

        modelBuilder.Entity<JobPosting>()
            .Ignore(j => j.Embedding)
            .Ignore(j => j.EmbeddingUpdatedAt);
    }
}
