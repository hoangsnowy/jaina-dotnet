namespace Jaina.Storage;

public interface IFileStorage : IDisposable
{
    string? BaseUri { get; }

    Task SaveAsync(string path, byte[] content, CancellationToken ct = default);
    Task SaveAsync(string path, Stream content, CancellationToken ct = default);

    Task<bool> ExistsAsync(string path, CancellationToken ct = default);

    Task<byte[]> GetBytesAsync(string path, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);

    Task<DateTime> GetLastModifiedAsync(string path, CancellationToken ct = default);

    Task<IEnumerable<string>> GetFileNamesAsync(string directory, CancellationToken ct = default);
    Task<IEnumerable<FileEntry>> GetFilesAsync(string directory, bool recursive = false, CancellationToken ct = default);

    Task DeleteAsync(string path, CancellationToken ct = default);
    Task MoveAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
}
