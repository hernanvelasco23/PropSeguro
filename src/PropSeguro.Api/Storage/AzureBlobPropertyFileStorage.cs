using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace PropSeguro.Api.Storage;

public sealed class AzureBlobPropertyFileStorage : IPropertyFileStorage
{
    private readonly BlobContainerClient containerClient;

    public AzureBlobPropertyFileStorage(IOptions<StorageOptions> options)
    {
        var blobOptions = options.Value.AzureBlob;

        if (string.IsNullOrWhiteSpace(blobOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(blobOptions.ContainerName))
        {
            throw new InvalidOperationException("Storage:AzureBlob requires ConnectionString and ContainerName.");
        }

        this.containerClient = new BlobContainerClient(blobOptions.ConnectionString, blobOptions.ContainerName);
    }

    public async Task SaveAsync(string storagePath, Stream content, string? contentType, CancellationToken cancellationToken)
    {
        await this.containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blobClient = this.containerClient.GetBlobClient(NormalizeBlobName(storagePath));

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            },
            cancellationToken);
    }

    public async Task<StoredPropertyFile?> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        var blobClient = this.containerClient.GetBlobClient(NormalizeBlobName(storagePath));

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new StoredPropertyFile(download.Value.Content);
    }

    public async Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken)
    {
        var blobClient = this.containerClient.GetBlobClient(NormalizeBlobName(storagePath));
        return await blobClient.ExistsAsync(cancellationToken);
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken)
    {
        var blobClient = this.containerClient.GetBlobClient(NormalizeBlobName(storagePath));
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task DeletePrefixAsync(string storagePrefix, CancellationToken cancellationToken)
    {
        var prefix = NormalizeBlobName(storagePrefix).TrimEnd('/') + "/";

        await foreach (var blob in this.containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            await this.containerClient.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: cancellationToken);
        }
    }

    public string CreateStoragePath(params string[] segments)
    {
        return NormalizeBlobName(string.Join(
            '/',
            segments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Select(segment => segment.Trim().Trim('/', '\\'))));
    }

    private static string NormalizeBlobName(string storagePath)
    {
        var blobName = storagePath.Replace('\\', '/').Trim('/');

        if (blobName.StartsWith("storage/", StringComparison.OrdinalIgnoreCase))
        {
            blobName = blobName["storage/".Length..];
        }

        if (string.IsNullOrWhiteSpace(blobName) ||
            Path.IsPathRooted(blobName) ||
            blobName.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            throw new InvalidOperationException("Invalid blob storage path.");
        }

        return blobName;
    }
}
