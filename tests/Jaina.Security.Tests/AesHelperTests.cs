using Jaina.Security.Encryption;

namespace Jaina.Security.Tests;

public class AesHelperTests
{
    private const string Pepper = "test-pepper";
    private const string Salt = "test-salt";

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalText()
    {
        // Arrange
        var plain = "Hello, Jaina!";

        // Act
        var cipher = AesHelper.Encrypt(plain, Pepper, Salt);
        var result = AesHelper.Decrypt(cipher, Pepper, Salt);

        // Assert
        Assert.Equal(plain, result);
    }

    [Fact]
    public void Encrypt_ProducesValidBase64()
    {
        // Act
        var cipher = AesHelper.Encrypt("data", Pepper, Salt);
        var bytes = Convert.FromBase64String(cipher); // throws if not valid Base64

        // Assert
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Encrypt_SameInput_ProducesDifferentCiphertextEachCall()
    {
        // Act (AES CBC with random IV)
        var a = AesHelper.Encrypt("same", Pepper, Salt);
        var b = AesHelper.Encrypt("same", Pepper, Salt);

        // Assert
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Encrypt_EmptyPlainText_ThrowsEncryptException()
    {
        // Act & Assert
        Assert.Throws<EncryptException>(() => AesHelper.Encrypt("", Pepper, Salt));
    }

    [Fact]
    public void Decrypt_EmptyCipherText_ThrowsEncryptException()
    {
        // Act & Assert
        Assert.Throws<EncryptException>(() => AesHelper.Decrypt("", Pepper, Salt));
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsException()
    {
        // Arrange
        var cipher = AesHelper.Encrypt("secret", Pepper, Salt);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => AesHelper.Decrypt(cipher, "wrong-pepper", "wrong-salt"));
    }

    [Fact]
    public void Encrypt_LongText_RoundTrips()
    {
        // Arrange
        var longText = new string('x', 10_000);

        // Act
        var cipher = AesHelper.Encrypt(longText, Pepper, Salt);
        var result = AesHelper.Decrypt(cipher, Pepper, Salt);

        // Assert
        Assert.Equal(longText, result);
    }
}
