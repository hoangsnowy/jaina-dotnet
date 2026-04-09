using Microsoft.Extensions.Options;

namespace Jaina.Storage.Local;

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IOptions<LocalStorageOptions> options)
    {
        _basePath = options.Value.BasePath;
        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);
    }

    public string? BaseUri => _basePath;

    private string GetFullPath(string path) => Path.Combine(_basePath, path);

    public async Task SaveAsync(string path, byte[] content, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
#if NET6_0_OR_GREATER
        await File.WriteAllBytesAsync(fullPath, content, ct).ConfigureAwait(false);
#else
        using var fs = File.Create(fullPath);
        await fs.WriteAsync(content, 0, content.Length, ct).ConfigureAwait(false);
#endif
    }

    public async Task SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        using var fs = File.Create(fullPath);
#if NET6_0_OR_GREATER
        await content.CopyToAsync(fs, ct).ConfigureAwait(false);
#else
        await content.CopyToAsync(fs, 81920, ct).ConfigureAwait(false);
#endif
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(GetFullPath(path)));

    public async Task<byte[]> GetBytesAsync(string path, CancellationToken ct = default)
    {
#if NET6_0_OR_GREATER
        return await File.ReadAllBytesAsync(GetFullPath(path), ct).ConfigureAwait(false);
#else
        using var fs = File.OpenRead(GetFullPath(path));
        using var ms = new MemoryStream();
        await fs.CopyToAsync(ms, 81920, ct).ConfigureAwait(false);
        return ms.ToArray();
#endif
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
        Task.FromResult<Stream>(File.OpenRead(GetFullPath(path)));

    public Task<DateTime> GetLastModifiedAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(File.GetLastWriteTimeUtc(GetFullPath(path)));

    public Task<IEnumerable<string>> GetFileNamesAsync(string directory, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(directory);
        var files = Directory.Exists(fullPath)
            ? Directory.GetFiles(fullPath).Select(Path.GetFileName).Where(f => f is not null).Cast<string>()
            : Enumerable.Empty<string>();
        return Task.FromResult(files);
    }

    public Task<IEnumerable<FileEntry>> GetFilesAsync(string directory, bool recursive = false, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(directory);
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.Exists(fullPath)
            ? Directory.GetFiles(fullPath, "*", option)
                .Select(f =>
                {
#if NET6_0_OR_GREATER
                    var relativePath = Path.GetRelativePath(_basePath, f);
#else
                    var relativePath = f.StartsWith(_basePath)
                        ? f.Substring(_basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        : f;
#endif
                    return new FileEntry(relativePath, Path.GetFileName(f));
                })
            : Enumerable.Empty<FileEntry>();
        return Task.FromResult(files);
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task MoveAsync(string sourcePath, string destinationPath, CancellationToken ct = default)
    {
        var src = GetFullPath(sourcePath);
        var dst = GetFullPath(destinationPath);
        var dir = Path.GetDirectoryName(dst);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
#if NET6_0_OR_GREATER
        File.Move(src, dst, overwrite: true);
#else
        if (File.Exists(dst)) File.Delete(dst);
        File.Move(src, dst);
#endif
        return Task.CompletedTask;
    }

    public void Dispose() { }
}
