using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Jaina.Storage.AzureBlob;

public class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobFileStorage(IOptions<AzureBlobStorageOptions> options)
    {
        var opts = options.Value;
        _container = new BlobContainerClient(opts.ConnectionString, opts.ContainerName);
        _container.CreateIfNotExists();
    }

    public string? BaseUri => _container.Uri.ToString();

    public async Task SaveAsync(string path, byte[] content, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(path);
        using var ms = new MemoryStream(content);
        await blob.UploadAsync(ms, overwrite: true, ct).ConfigureAwait(false);
    }

    public async Task SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(path);
        await blob.UploadAsync(content, overwrite: true, ct).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(path);
        var response = await blob.ExistsAsync(ct).ConfigureAwait(false);
        return response.Value;
    }

    public async Task<byte[]> GetBytesAsync(string path, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(path);
        using var ms = new MemoryStream();
        await blob.DownloadToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(path);
        return await blob.OpenReadAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<DateTime> GetLastModifiedAsync(string path, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(path);
        var props = await blob.GetPropertiesAsync(cancellationToken: ct).ConfigureAwait(false);
        return props.Value.LastModified.UtcDateTime;
    }

    public async Task<IEnumerable<string>> GetFileNamesAsync(string directory, CancellationToken ct = default)
    {
        var results = new List<string>();
        var prefix = string.IsNullOrEmpty(directory) ? null : directory.TrimEnd('/') + "/";
        await foreach (var blob in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
            results.Add(Path.GetFileName(blob.Name));
        return results;
    }

    public async Task<IEnumerable<FileEntry>> GetFilesAsync(string directory, bool recursive = false, CancellationToken ct = default)
    {
        var results = new List<FileEntry>();
        var prefix = string.IsNullOrEmpty(directory) ? null : directory.TrimEnd('/') + "/";

        if (recursive)
        {
            await foreach (var blob in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
                results.Add(new FileEntry(blob.Name, Path.GetFileName(blob.Name)));
        }
        else
        {
            await foreach (var item in _container.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/", cancellationToken: ct))
                if (item.IsBlob)
                    results.Add(new FileEntry(item.Blob.Name, Path.GetFileName(item.Blob.Name)));
        }

        return results;
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(path);
        await blob.DeleteIfExistsAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task MoveAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
    {
        var srcBlob = _container.GetBlobClient(sourcePath);
        var dstBlob = _container.GetBlobClient(destinationPath);
        await dstBlob.StartCopyFromUriAsync(srcBlob.Uri, cancellationToken: ct).ConfigureAwait(false);
        await srcBlob.DeleteIfExistsAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    public void Dispose() { }
}
