namespace PropSeguro.Api.Storage;

public sealed class StorageOptions
{
    public string Provider { get; set; } = "Local";

    public string LocalBasePath { get; set; } = string.Empty;

    public string RootPath { get; set; } = "storage";

    public string EffectiveLocalBasePath => string.IsNullOrWhiteSpace(LocalBasePath) ? RootPath : LocalBasePath;

    public long MaxPdfSizeBytes { get; set; } = 15 * 1024 * 1024;

    public long MaxImageSizeBytes { get; set; } = 5 * 1024 * 1024;

    public AzureBlobStorageOptions AzureBlob { get; set; } = new();
}

public sealed class AzureBlobStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "propsegura-files";
}
