namespace PropSeguro.Domain.PropertyAnalyses;

public sealed record NeighborhoodBenchmark(
    string Neighborhood,
    decimal AverageUsdPerSquareMeter,
    DemandLevel InvestmentDemandLevel,
    DemandLevel LivingDemandLevel)
{
    public static NeighborhoodBenchmark? Find(string neighborhood)
    {
        return NeighborhoodBenchmarkProvider.Default.Find(neighborhood);
    }
}

public interface INeighborhoodBenchmarkProvider
{
    NeighborhoodBenchmark? Find(string neighborhood);
}

public sealed class NeighborhoodBenchmarkProvider : INeighborhoodBenchmarkProvider
{
    private static readonly IReadOnlyDictionary<string, NeighborhoodBenchmark> Benchmarks =
        new Dictionary<string, NeighborhoodBenchmark>(StringComparer.OrdinalIgnoreCase)
        {
            ["Recoleta"] = new("Recoleta", 2800m, DemandLevel.High, DemandLevel.High),
            ["Palermo"] = new("Palermo", 3000m, DemandLevel.High, DemandLevel.High),
            ["Belgrano"] = new("Belgrano", 2700m, DemandLevel.High, DemandLevel.High),
            ["Caballito"] = new("Caballito", 2300m, DemandLevel.Medium, DemandLevel.High),
            ["Villa Urquiza"] = new("Villa Urquiza", 2200m, DemandLevel.Medium, DemandLevel.High),
            ["Almagro"] = new("Almagro", 2100m, DemandLevel.High, DemandLevel.Medium)
        };

    public static NeighborhoodBenchmarkProvider Default { get; } = new();

    public NeighborhoodBenchmark? Find(string neighborhood)
    {
        if (string.IsNullOrWhiteSpace(neighborhood))
        {
            return null;
        }

        return Benchmarks.TryGetValue(neighborhood.Trim(), out var benchmark) ? benchmark : null;
    }
}
