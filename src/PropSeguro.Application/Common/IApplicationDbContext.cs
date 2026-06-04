using Microsoft.EntityFrameworkCore;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public interface IApplicationDbContext
{
    DbSet<PropertyAnalysis> PropertyAnalyses { get; }
    DbSet<PropertyDocument> PropertyDocuments { get; }
    DbSet<BuyerChecklistItem> BuyerChecklistItems { get; }
    DbSet<DocumentFinding> DocumentFindings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
