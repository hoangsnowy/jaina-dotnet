using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Options;

namespace Jaina.Storage.AzureFileShare;

public class AzureFileShareStorage : IFileStorage
{
    private readonly ShareClient _share;

    public AzureFileShareStorage(IOptions<AzureFileShareOptions> options)
    {
        var opts = options.Value;
        _share = new ShareClient(opts.ConnectionString, opts.ShareName);
        _share.CreateIfNotExists();
    }

    public string? BaseUri => _share.Uri.ToString();

    private ShareDirectoryClient GetDirectory(string? directory)
    {
        var root = _share.GetRootDirectoryClient();
        if (string.IsNullOrEmpty(directory)) return root;
        var dir = root.GetSubdirectoryClient(directory);
        dir.CreateIfNotExists();
        return dir;
    }

    public async Task SaveAsync(string path, byte[] content, CancellationToken ct = default)
    {
        var dir = GetDirectory(Path.GetDirectoryName(path));
        var file = dir.GetFileClient(Path.GetFileName(path));
        using var ms = new MemoryStream(content);
        await file.CreateAsync(ms.Length, cancellationToken: ct).ConfigureAwait(false);
        await file.UploadAsync(ms, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        var dir = GetDirectory(Path.GetDirectoryName(path));
        var file = dir.GetFileClient(Path.GetFileName(path));
        await file.CreateAsync(content.Length, cancellationToken: ct).ConfigureAwait(false);
        await file.UploadAsync(content, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken ct = default)
    {
        var dir = GetDirectory(Path.GetDirectoryName(path));
        var file = dir.GetFileClient(Path.GetFileName(path));
        var response = await file.ExistsAsync(ct).ConfigureAwait(false);
        return response.Value;
    }

    public async Task<byte[]> GetBytesAsync(string path, CancellationToken ct = default)
    {
        var dir = GetDirectory(Path.GetDirectoryName(path));
        var file = dir.GetFileClient(Path.GetFileName(path));
        var download = await file.DownloadAsync(cancellationToken: ct).ConfigureAwait(false);
        using var ms = new MemoryStream();
#if NET6_0_OR_GREATER
        await download.Value.Content.CopyToAsync(ms, ct).ConfigureAwait(false);
#else
        await download.Value.Content.CopyToAsync(ms, 81920, ct).ConfigureAwait(false);
#endif
        return ms.ToArray();
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var dir = GetDirectory(Path.GetDirectoryName(path));
        var file = dir.GetFileClient(Path.GetFileName(path));
        var download = await file.DownloadAsync(cancellationToken: ct).ConfigureAwait(false);
        return download.Value.Content;
    }

    public async Task<DateTime> GetLastModifiedAsync(string path, CancellationToken ct = default)
    {
        var dir = GetDirectory(Path.GetDirectoryName(path));
        var file = dir.GetFileClient(Path.GetFileName(path));
        var props = await file.GetPropertiesAsync(ct).ConfigureAwait(false);
        return props.Value.LastModified.UtcDateTime;
    }

    public Task<IEnumerable<string>> GetFileNamesAsync(string directory, CancellationToken ct = default)
    {
        var dir = GetDirectory(directory);
        var results = new List<string>();
        foreach (var item in dir.GetFilesAndDirectories())
            if (!item.IsDirectory) results.Add(item.Name);
        return Task.FromResult<IEnumerable<string>>(results);
    }

    public Task<IEnumerable<FileEntry>> GetFilesAsync(string directory, bool recursive = false, CancellationToken ct = default)
    {
        var results = new List<FileEntry>();
        ListFiles(GetDirectory(directory), directory ?? "", recursive, results);
        return Task.FromResult<IEnumerable<FileEntry>>(results);
    }

    private void ListFiles(ShareDirectoryClient dir, string prefix, bool recursive, List<FileEntry> results)
    {
        foreach (var item in dir.GetFilesAndDirectories())
        {
            var fullPath = string.IsNullOrEmpty(prefix) ? item.Name : $"{prefix}/{item.Name}";
            if (item.IsDirectory && recursive)
                ListFiles(dir.GetSubdirectoryClient(item.Name), fullPath, true, results);
            else if (!item.IsDirectory)
                results.Add(new FileEntry(fullPath, item.Name));
        }
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var dir = GetDirectory(Path.GetDirectoryName(path));
        var file = dir.GetFileClient(Path.GetFileName(path));
        await file.DeleteIfExistsAsync(conditions: null, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task MoveAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
    {
        var content = await GetBytesAsync(sourcePath, ct).ConfigureAwait(false);
        await SaveAsync(destinationPath, content, ct).ConfigureAwait(false);
        await DeleteAsync(sourcePath, ct).ConfigureAwait(false);
    }

    public void Dispose() { }
}
