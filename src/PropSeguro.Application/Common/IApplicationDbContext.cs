using Microsoft.EntityFrameworkCore;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public interface IApplicationDbContext
{
    DbSet<PropertyAnalysis> PropertyAnalyses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
