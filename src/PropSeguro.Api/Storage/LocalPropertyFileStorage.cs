using Microsoft.Extensions.Options;

namespace PropSeguro.Api.Storage;

public sealed class LocalPropertyFileStorage(IWebHostEnvironment environment, IOptions<StorageOptions> options) : IPropertyFileStorage
{
    private readonly string rootPath = ResolveRootPath(environment.ContentRootPath, options.Value.EffectiveLocalBasePath);

    public async Task SaveAsync(string storagePath, Stream content, string? contentType, CancellationToken cancellationToken)
    {
        var fullPath = this.ResolveFullPath(storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var output = File.Create(fullPath);
        await content.CopyToAsync(output, cancellationToken);
    }

    public Task<StoredPropertyFile?> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = this.ResolveFullPath(storagePath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<StoredPropertyFile?>(null);
        }

        return Task.FromResult<StoredPropertyFile?>(new StoredPropertyFile(File.OpenRead(fullPath)));
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = this.ResolveFullPath(storagePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = this.ResolveFullPath(storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string storagePrefix, CancellationToken cancellationToken)
    {
        var fullPath = this.ResolveFullPath(storagePrefix);

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        return Task.CompletedTask;
    }

    public string CreateStoragePath(params string[] segments)
    {
        return NormalizeStoragePath(string.Join(
            '/',
            segments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Select(segment => segment.Trim().Trim('/', '\\'))));
    }

    private string ResolveFullPath(string storagePath)
    {
        var normalizedPath = NormalizeStoragePath(storagePath);
        var fullPath = Path.GetFullPath(Path.Combine(this.rootPath, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(this.rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage path resolves outside the configured storage root.");
        }

        return fullPath;
    }

    private string NormalizeStoragePath(string storagePath)
    {
        var normalizedPath = storagePath.Replace('\\', '/').Trim('/');
        var rootDirectoryName = Path.GetFileName(this.rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (!string.IsNullOrWhiteSpace(rootDirectoryName) &&
            normalizedPath.StartsWith($"{rootDirectoryName}/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath[(rootDirectoryName.Length + 1)..];
        }

        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            Path.IsPathRooted(normalizedPath) ||
            normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return normalizedPath;
    }

    private static string ResolveRootPath(string contentRootPath, string configuredRootPath)
    {
        var rootPath = string.IsNullOrWhiteSpace(configuredRootPath)
            ? "storage"
            : configuredRootPath.Trim();

        if (Path.IsPathRooted(rootPath))
        {
            return Path.GetFullPath(rootPath);
        }

        return Path.GetFullPath(Path.Combine(ResolveRepositoryRoot(contentRootPath), rootPath));
    }

    private static string ResolveRepositoryRoot(string contentRootPath)
    {
        var directory = new DirectoryInfo(contentRootPath);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PropSeguro.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return contentRootPath;
    }
}
