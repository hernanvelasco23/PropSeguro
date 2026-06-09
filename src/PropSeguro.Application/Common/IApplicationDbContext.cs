using Microsoft.EntityFrameworkCore;
using PropSeguro.Domain.BetaAccess;
using PropSeguro.Domain.PropertyAnalyses;
using PropSeguro.Domain.Users;

namespace PropSeguro.Application.Common;

public interface IApplicationDbContext
{
    DbSet<PropertyAnalysis> PropertyAnalyses { get; }
    DbSet<PropertyDocument> PropertyDocuments { get; }
    DbSet<BuyerChecklistItem> BuyerChecklistItems { get; }
    DbSet<DocumentFinding> DocumentFindings { get; }
    DbSet<AppUser> Users { get; }
    DbSet<BetaAccessRequest> BetaAccessRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
