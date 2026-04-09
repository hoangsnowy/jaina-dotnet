using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;
using System.Text;

namespace Jaina.Security.Encryption;

public static class PgpHelper
{
    public static IEnumerable<string> Decrypt(byte[] cipherContent, byte[] privateKeyContent, string passPhrase, Encoding? encoding = null)
    {
        if (cipherContent == null) throw new ArgumentNullException(nameof(cipherContent));
        if (privateKeyContent == null) throw new ArgumentNullException(nameof(privateKeyContent));

        encoding ??= Encoding.UTF8;
        using var inputStream = new MemoryStream(cipherContent);
        using var keyStream = new MemoryStream(privateKeyContent);
        return DecryptCore(inputStream, keyStream, passPhrase.ToCharArray(), encoding);
    }

    public static byte[] DecryptToBytes(byte[] cipherContent, byte[] privateKeyContent, string passPhrase)
    {
        if (cipherContent == null) throw new ArgumentNullException(nameof(cipherContent));
        if (privateKeyContent == null) throw new ArgumentNullException(nameof(privateKeyContent));

        using var inputStream = new MemoryStream(cipherContent);
        using var keyStream = new MemoryStream(privateKeyContent);
        return DecryptToBytesCore(inputStream, keyStream, passPhrase.ToCharArray());
    }

    public static byte[] Encrypt(byte[] plainContent, byte[] publicKeyContent, string? fileName = null)
    {
        if (plainContent == null) throw new ArgumentNullException(nameof(plainContent));
        if (publicKeyContent == null) throw new ArgumentNullException(nameof(publicKeyContent));

        using var publicKeyStream = new MemoryStream(publicKeyContent);
        using var cipherStream = EncryptCore(plainContent, publicKeyStream, fileName ?? PgpLiteralData.Console, armor: false, withIntegrityCheck: false);
        using var result = new MemoryStream();
        cipherStream.Seek(0, SeekOrigin.Begin);
        cipherStream.CopyTo(result);
        return result.ToArray();
    }

    public static void EncryptFile(string plainFilePath, string cipherFilePath, byte[] publicKeyContent)
    {
        if (!File.Exists(plainFilePath)) throw new FileNotFoundException($"File doesn't exist: {plainFilePath}");
        if (publicKeyContent == null) throw new ArgumentNullException(nameof(publicKeyContent));

        using var publicKeyStream = new MemoryStream(publicKeyContent);
        using var cipherStream = EncryptFileCore(plainFilePath, publicKeyStream, armor: false, withIntegrityCheck: false);
        using var output = File.Create(cipherFilePath);
        cipherStream.Seek(0, SeekOrigin.Begin);
        cipherStream.CopyTo(output);
    }

    private static IEnumerable<string> DecryptCore(Stream cipherStream, Stream privateKeyStream, char[] passPhrase, Encoding encoding)
    {
        var decoded = PgpUtilities.GetDecoderStream(cipherStream);
        var factory = new PgpObjectFactory(decoded);
        var obj = factory.NextPgpObject();
        var encList = obj as PgpEncryptedDataList ?? (PgpEncryptedDataList)factory.NextPgpObject();

        var (privateKey, encData) = FindPrivateKey(encList, privateKeyStream, passPhrase);
        var literalData = GetLiteralData(encData, privateKey);

        var lines = new List<string>();
        using var reader = new StreamReader(literalData.GetInputStream(), encoding);
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        }
        return lines;
    }

    private static byte[] DecryptToBytesCore(Stream cipherStream, Stream privateKeyStream, char[] passPhrase)
    {
        var decoded = PgpUtilities.GetDecoderStream(cipherStream);
        var factory = new PgpObjectFactory(decoded);
        var obj = factory.NextPgpObject();
        var encList = obj as PgpEncryptedDataList ?? (PgpEncryptedDataList)factory.NextPgpObject();

        var (privateKey, encData) = FindPrivateKey(encList, privateKeyStream, passPhrase);
        var literalData = GetLiteralData(encData, privateKey);

        using var ms = new MemoryStream();
        literalData.GetInputStream().CopyTo(ms);
        return ms.ToArray();
    }

    private static (PgpPrivateKey Key, PgpPublicKeyEncryptedData Data) FindPrivateKey(
        PgpEncryptedDataList encList, Stream privateKeyStream, char[] passPhrase)
    {
        var bundle = new PgpSecretKeyRingBundle(PgpUtilities.GetDecoderStream(privateKeyStream));
        foreach (PgpPublicKeyEncryptedData item in encList.GetEncryptedDataObjects())
        {
            var secretKey = bundle.GetSecretKey(item.KeyId);
            var key = secretKey?.ExtractPrivateKey(passPhrase);
            if (key is not null) return (key, item);
        }
        throw new PgpException("Could not find Private Key");
    }

    private static PgpLiteralData GetLiteralData(PgpPublicKeyEncryptedData encData, PgpPrivateKey privateKey)
    {
        var dataStream = encData.GetDataStream(privateKey);
        var factory = new PgpObjectFactory(dataStream);
        var obj = factory.NextPgpObject();

        if (obj is PgpCompressedData compressed)
        {
            factory = new PgpObjectFactory(compressed.GetDataStream());
            obj = factory.NextPgpObject();
        }

        return obj as PgpLiteralData
            ?? throw new PgpException(obj is PgpOnePassSignatureList
                ? "Encrypted message contains a signed message - not literal data."
                : "Message is not a simple encrypted file - type unknown.");
    }

    private static Stream EncryptCore(byte[] plainContent, Stream publicKeyStream, string fileName, bool armor, bool withIntegrityCheck)
    {
        Stream output = new MemoryStream();
        if (armor) output = new ArmoredOutputStream(output);

        var compressed = Compress(plainContent, fileName, CompressionAlgorithmTag.Zip);
        var gen = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Cast5, withIntegrityCheck, new SecureRandom());
        gen.AddMethod(ReadPublicKey(publicKeyStream));

        using (var cs = gen.Open(output, compressed.Length))
            cs.Write(compressed, 0, compressed.Length);

        return output;
    }

    private static Stream EncryptFileCore(string filePath, Stream publicKeyStream, bool armor, bool withIntegrityCheck)
    {
        Stream output = new MemoryStream();
        if (armor) output = new ArmoredOutputStream(output);

        var compressed = CompressFile(filePath, CompressionAlgorithmTag.Zip);
        var gen = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Cast5, withIntegrityCheck, new SecureRandom());
        gen.AddMethod(ReadPublicKey(publicKeyStream));

        using (var cs = gen.Open(output, compressed.Length))
            cs.Write(compressed, 0, compressed.Length);

        return output;
    }

    private static byte[] CompressFile(string filePath, CompressionAlgorithmTag algorithm)
    {
        using var ms = new MemoryStream();
        var gen = new PgpCompressedDataGenerator(algorithm);
        PgpUtilities.WriteFileToLiteralData(gen.Open(ms), PgpLiteralData.Binary, new FileInfo(filePath));
        return ms.ToArray();
    }

    private static byte[] Compress(byte[] data, string fileName, CompressionAlgorithmTag algorithm)
    {
        using var ms = new MemoryStream();
        var compGen = new PgpCompressedDataGenerator(algorithm);
        var cos = compGen.Open(ms);
        var litGen = new PgpLiteralDataGenerator();
        using (var pOut = litGen.Open(cos, PgpLiteralData.Binary, fileName, data.Length, DateTime.UtcNow))
            pOut.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    private static PgpPublicKey ReadPublicKey(Stream keyStream)
    {
        var decoded = PgpUtilities.GetDecoderStream(keyStream);
        var bundle = new PgpPublicKeyRingBundle(decoded);
        foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
            foreach (PgpPublicKey key in ring.GetPublicKeys())
                if (key.IsEncryptionKey) return key;
        throw new PgpException("Cannot find encryption key in key ring.");
    }
}
