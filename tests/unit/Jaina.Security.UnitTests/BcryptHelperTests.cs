using Jaina.Security.Hashing;

namespace Jaina.Security.UnitTests;

public class BcryptHelperTests
{
    [Fact]
    public void Hash_ValidInput_ProducesNonEmptyHash()
    {
        // Act
        var hash = BcryptHelper.Hash("myPassword");

        // Assert
        Assert.NotEmpty(hash);
        Assert.NotEqual("myPassword", hash);
    }

    [Fact]
    public void Hash_SameInput_ProducesDifferentHashes()
    {
        // Act (bcrypt uses random salt each call)
        var hash1 = BcryptHelper.Hash("password");
        var hash2 = BcryptHelper.Hash("password");

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        var hash = BcryptHelper.Hash("correct-horse");

        // Act
        var result = BcryptHelper.Verify("correct-horse", hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        // Arrange
        var hash = BcryptHelper.Hash("correct-horse");

        // Act
        var result = BcryptHelper.Verify("wrong-password", hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Hash_EmptyPassword_ThrowsHashingException()
    {
        // Act & Assert
        Assert.Throws<HashingException>(() => BcryptHelper.Hash(""));
    }

    [Fact]
    public void Hash_WhitespacePassword_ThrowsHashingException()
    {
        // Act & Assert
        Assert.Throws<HashingException>(() => BcryptHelper.Hash("   "));
    }
}
