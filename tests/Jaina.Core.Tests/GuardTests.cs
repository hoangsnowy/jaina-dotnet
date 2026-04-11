namespace Jaina.Core.Tests;

public class GuardTests
{
    [Fact]
    public void NotNull_NonNullValue_ReturnsValue()
    {
        // Arrange
        var obj = new object();

        // Act
        var result = Guard.NotNull(obj);

        // Assert
        Assert.Same(obj, result);
    }

    [Fact]
    public void NotNull_NullValue_ThrowsArgumentNullException()
    {
        // Arrange
        object? value = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Guard.NotNull(value));
    }

    [Fact]
    public void NotNullOrEmpty_ValidString_ReturnsString()
    {
        // Arrange / Act
        var result = Guard.NotNullOrEmpty("hello");

        // Assert
        Assert.Equal("hello", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NotNullOrEmpty_NullOrEmpty_ThrowsArgumentException(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotNullOrWhiteSpace_NullEmptyOrWhitespace_ThrowsArgumentException(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhiteSpace(value!));
    }

    [Fact]
    public void NotNullOrWhiteSpace_ValidString_ReturnsString()
    {
        // Arrange / Act
        var result = Guard.NotNullOrWhiteSpace("valid");

        // Assert
        Assert.Equal("valid", result);
    }

    [Fact]
    public void NotNullOrEmpty_NonEmptyCollection_ReturnsCollection()
    {
        // Arrange
        var list = new[] { 1, 2, 3 };

        // Act
        var result = Guard.NotNullOrEmpty(list);

        // Assert
        Assert.Same(list, result);
    }

    [Fact]
    public void NotNullOrEmpty_EmptyCollection_ThrowsArgumentException()
    {
        // Arrange
        var empty = Array.Empty<int>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(empty));
    }

    [Fact]
    public void NotNullOrEmpty_NullCollection_ThrowsArgumentException()
    {
        // Arrange
        int[]? nullCollection = null;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(nullCollection!));
    }

    [Fact]
    public void IsValidDate_ValidDate_DoesNotThrow()
    {
        // Act & Assert (no exception expected)
        Guard.IsValidDate("2024-01-15");
    }

    [Fact]
    public void IsValidDate_InvalidDate_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Guard.IsValidDate("not-a-date"));
    }

    [Fact]
    public void Requires_ConditionTrue_DoesNotThrow()
    {
        // Act & Assert (no exception expected)
        Guard.Requires<InvalidOperationException>(true, "msg");
    }

    [Fact]
    public void Requires_ConditionFalse_ThrowsSpecifiedException()
    {
        // Act
        var ex = Assert.Throws<InvalidOperationException>(
            () => Guard.Requires<InvalidOperationException>(false, "Custom error"));

        // Assert
        Assert.Equal("Custom error", ex.Message);
    }
}
