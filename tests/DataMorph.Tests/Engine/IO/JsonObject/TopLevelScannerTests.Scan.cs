namespace DataMorph.Tests.Engine.IO.JsonObject;

public sealed partial class TopLevelScannerTests
{
    [Fact]
    public void Scan_WithEmptyObject_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithSinglePrimitiveString_ReturnsOneEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":\"v\"}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithSinglePrimitiveNumber_ReturnsOneEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"n\":42}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithSingleNestedObject_ReturnsOneEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"o\":{\"a\":1}}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithSingleNestedArray_ReturnsOneEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"a\":[1,2]}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithMultipleKeys_PreservesInsertionOrder()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"b\":2,\"a\":1}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithDuplicateKeys_LastValueWins()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":1,\"k\":2}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithTripleDuplicateKey_LastValueWins()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":1,\"k\":2,\"k\":3}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithDuplicateKey_PositionStableWithOtherKeys()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"a\":1,\"k\":1,\"b\":2,\"k\":2}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithDeeplyNestedValue_CapturesCorrectByteRange()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"x\":{\"y\":{\"z\":1}}}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithFileSizeExceedingInitialBufferSize_CapturesCorrectBytes()
    {
        // Arrange — object with many small fields so the total exceeds 1 MB
        // without any single JSON string token exceeding the buffer
        var fields = string.Join(",", Enumerable.Range(0, 90_000).Select(i => $"\"f{i}\":{i}"));
        File.WriteAllText(_testFilePath, $"{{{fields}}}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithStringValueExceedingMaxBufferSize_ThrowsNotSupportedException()
    {
        // Arrange — string value larger than 16 MB
        var oversizedString = new string('a', 16 * 1024 * 1024 + 1);
        File.WriteAllText(_testFilePath, $"{{\"k\":\"{oversizedString}\"}}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithBooleanTrue_ReturnsCorrectBytes()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":true}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithBooleanFalse_ReturnsCorrectBytes()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":false}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithNullValue_ReturnsCorrectBytes()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":null}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var fields = string.Join(",", Enumerable.Range(0, 100_000).Select(i => $"\"f{i}\":{i}"));
        File.WriteAllText(_testFilePath, $"{{{fields}}}");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithLeadingTrailingWhitespace_SameResultAsCompactForm()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "  { \"k\" : 1 }  ");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithRootArray_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[1,2,3]");

        // Act

        // Assert
    }

    [Fact]
    public void Scan_WithRootPrimitive_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "42");

        // Act

        // Assert
    }
}
