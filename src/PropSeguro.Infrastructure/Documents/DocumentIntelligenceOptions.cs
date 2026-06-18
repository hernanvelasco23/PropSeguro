namespace PropSeguro.Infrastructure.Documents;

public sealed class DocumentIntelligenceOptions
{
    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "prebuilt-read";
}
