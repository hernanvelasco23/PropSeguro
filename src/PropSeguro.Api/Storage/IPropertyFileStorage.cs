namespace PropSeguro.Api.Storage;

public interface IFileStorageService
{
    Task SaveAsync(string storagePath, Stream content, string? contentType, CancellationToken cancellationToken);

    Task<StoredPropertyFile?> OpenReadAsync(string storagePath, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken);

    Task DeletePrefixAsync(string storagePrefix, CancellationToken cancellationToken);

    string CreateStoragePath(params string[] segments);
}

public interface IPropertyFileStorage : IFileStorageService
{
}

public sealed record StoredPropertyFile(Stream Content);
