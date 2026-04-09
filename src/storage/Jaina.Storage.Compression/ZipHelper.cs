using ICSharpCode.SharpZipLib.Zip;

namespace Jaina.Storage.Compression;

public static class ZipHelper
{
    public static byte[] Compress(IDictionary<string, byte[]> files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipOutputStream(ms))
        {
            zip.SetLevel(5);
            foreach (var kvp in files)
            {
                var name = kvp.Key;
                var content = kvp.Value;
                var entry = new ZipEntry(name) { DateTime = DateTime.UtcNow, Size = content.Length };
                zip.PutNextEntry(entry);
                zip.Write(content, 0, content.Length);
                zip.CloseEntry();
            }
        }
        return ms.ToArray();
    }

    public static IDictionary<string, byte[]> Decompress(byte[] zipContent)
    {
        var result = new Dictionary<string, byte[]>();
        using var ms = new MemoryStream(zipContent);
        using var zip = new ZipInputStream(ms);
        while (zip.GetNextEntry() is { } entry)
        {
            if (entry.IsDirectory) continue;
            using var entryMs = new MemoryStream();
            zip.CopyTo(entryMs);
            result[entry.Name] = entryMs.ToArray();
        }
        return result;
    }

    public static void CompressToFile(string outputPath, IDictionary<string, byte[]> files)
    {
        var content = Compress(files);
        File.WriteAllBytes(outputPath, content);
    }

    public static IDictionary<string, byte[]> DecompressFile(string zipFilePath) =>
        Decompress(File.ReadAllBytes(zipFilePath));
}
