using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace Jaina.Storage.Sftp;

public class SftpFileStorage : IFileStorage
{
    private readonly SftpStorageOptions _options;
    private SftpClient? _client;

    public SftpFileStorage(IOptions<SftpStorageOptions> options)
    {
        _options = options.Value;
    }

    public string? BaseUri => $"sftp://{_options.Host}:{_options.Port}{_options.BasePath}";

    private SftpClient Client
    {
        get
        {
            if (_client is { IsConnected: true }) return _client;

            _client?.Dispose();
            _client = CreateClient();
            _client.Connect();
            return _client;
        }
    }

    private SftpClient CreateClient()
    {
        if (!string.IsNullOrEmpty(_options.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(_options.PrivateKeyPassPhrase)
                ? new PrivateKeyFile(_options.PrivateKeyPath!)
                : new PrivateKeyFile(_options.PrivateKeyPath!, _options.PrivateKeyPassPhrase);
            return new SftpClient(_options.Host, _options.Port, _options.Username, keyFile);
        }
        return new SftpClient(_options.Host, _options.Port, _options.Username, _options.Password ?? string.Empty);
    }

    private string GetFullPath(string path) =>
        _options.BasePath.TrimEnd('/') + "/" + path.TrimStart('/');

    public Task SaveAsync(string path, byte[] content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream(content);
        Client.UploadFile(ms, GetFullPath(path), canOverride: true);
        return Task.CompletedTask;
    }

    public Task SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        Client.UploadFile(content, GetFullPath(path), canOverride: true);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Client.Exists(GetFullPath(path)));

    public Task<byte[]> GetBytesAsync(string path, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        Client.DownloadFile(GetFullPath(path), ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        Client.DownloadFile(GetFullPath(path), ms);
        ms.Position = 0;
        return Task.FromResult<Stream>(ms);
    }

    public Task<DateTime> GetLastModifiedAsync(string path, CancellationToken ct = default)
    {
        var attrs = Client.GetAttributes(GetFullPath(path));
        return Task.FromResult(attrs.LastWriteTimeUtc);
    }

    public Task<IEnumerable<string>> GetFileNamesAsync(string directory, CancellationToken ct = default)
    {
        var files = Client.ListDirectory(GetFullPath(directory))
            .Where(f => !f.IsDirectory)
            .Select(f => f.Name);
        return Task.FromResult(files);
    }

    public Task<IEnumerable<FileEntry>> GetFilesAsync(string directory, bool recursive = false, CancellationToken ct = default)
    {
        var results = new List<FileEntry>();
        ListFiles(GetFullPath(directory), directory, recursive, results);
        return Task.FromResult<IEnumerable<FileEntry>>(results);
    }

    private void ListFiles(string fullDir, string relativeDir, bool recursive, List<FileEntry> results)
    {
        foreach (var item in Client.ListDirectory(fullDir))
        {
            if (item.Name is "." or "..") continue;
            var relativePath = string.IsNullOrEmpty(relativeDir) ? item.Name : $"{relativeDir}/{item.Name}";
            if (item.IsDirectory && recursive)
                ListFiles(item.FullName, relativePath, true, results);
            else if (!item.IsDirectory)
                results.Add(new FileEntry(relativePath, item.Name));
        }
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(path);
        if (Client.Exists(fullPath)) Client.DeleteFile(fullPath);
        return Task.CompletedTask;
    }

    public Task MoveAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
    {
        Client.RenameFile(GetFullPath(sourcePath), GetFullPath(destinationPath));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }
}
