using Jaina.Security.Hashing;

namespace Jaina.Security.UnitTests;

public class Sha256HelperTests
{
    [Fact]
    public void Hash_ValidInput_ProducesValidBase64Of32Bytes()
    {
        // Act
        var result = Sha256Helper.Hash("hello world");

        // Assert — SHA-256 always produces 32 bytes; result is Base64-encoded
        Assert.NotEmpty(result);
        var bytes = Convert.FromBase64String(result);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void Hash_SameInput_ProducesSameOutput()
    {
        // Act
        var a = Sha256Helper.Hash("hello");
        var b = Sha256Helper.Hash("hello");

        // Assert
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_DifferentInputs_ProduceDifferentOutputs()
    {
        // Act
        var a = Sha256Helper.Hash("hello");
        var b = Sha256Helper.Hash("world");

        // Assert
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Hash_EmptyInput_ThrowsHashingException()
    {
        // Act & Assert
        Assert.Throws<HashingException>(() => Sha256Helper.Hash(""));
    }
}
