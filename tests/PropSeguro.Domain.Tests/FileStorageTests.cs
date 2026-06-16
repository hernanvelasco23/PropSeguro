using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PropSeguro.Api.Storage;

namespace PropSeguro.Domain.Tests;

public sealed class FileStorageTests
{
    [Fact]
    public async Task Local_storage_saves_and_reads_file()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"propsegura-storage-test-{Guid.NewGuid():N}");
        var storage = CreateLocalStorage(rootPath);

        await using var input = new MemoryStream([1, 2, 3, 4]);
        await storage.SaveAsync("property-analyses/test/documents/original.pdf", input, "application/pdf", CancellationToken.None);

        var storedFile = await storage.OpenReadAsync("property-analyses/test/documents/original.pdf", CancellationToken.None);

        Assert.NotNull(storedFile);
        Assert.True(await storage.ExistsAsync("property-analyses/test/documents/original.pdf", CancellationToken.None));
        await using (storedFile!.Content)
        {
            using var output = new MemoryStream();
            await storedFile.Content.CopyToAsync(output);
            Assert.Equal([1, 2, 3, 4], output.ToArray());
        }

        await storage.DeleteAsync("property-analyses/test/documents/original.pdf", CancellationToken.None);
        Assert.False(await storage.ExistsAsync("property-analyses/test/documents/original.pdf", CancellationToken.None));

        Directory.Delete(rootPath, recursive: true);
    }

    [Fact]
    public void Local_storage_generates_safe_storage_path()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"propsegura-storage-test-{Guid.NewGuid():N}");
        var storage = CreateLocalStorage(rootPath);

        var storagePath = storage.CreateStoragePath(
            "property-analyses",
            Guid.NewGuid().ToString("D"),
            "documents",
            Guid.NewGuid().ToString("D"),
            "original.pdf");

        Assert.StartsWith("property-analyses/", storagePath);
        Assert.EndsWith("/original.pdf", storagePath);
        Assert.DoesNotContain("\\", storagePath);
        Assert.DoesNotContain("..", storagePath);
    }

    [Fact]
    public async Task Local_storage_rejects_path_traversal()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"propsegura-storage-test-{Guid.NewGuid():N}");
        var storage = CreateLocalStorage(rootPath);

        await using var input = new MemoryStream([1]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.SaveAsync("../outside.pdf", input, "application/pdf", CancellationToken.None));

        Assert.Throws<InvalidOperationException>(() => storage.CreateStoragePath("property-analyses", "..", "outside.pdf"));
    }

    [Fact]
    public void Azure_blob_storage_options_require_connection_string_and_container()
    {
        var options = new StorageOptions
        {
            Provider = "AzureBlob",
            AzureBlob = new AzureBlobStorageOptions
            {
                ConnectionString = "",
                ContainerName = "propsegura-files"
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            new AzureBlobPropertyFileStorage(Options.Create(options)));
    }

    private static LocalPropertyFileStorage CreateLocalStorage(string rootPath)
    {
        return new LocalPropertyFileStorage(
            new FakeWebHostEnvironment(),
            Options.Create(new StorageOptions
            {
                RootPath = rootPath
            }));
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PropSeguro.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
