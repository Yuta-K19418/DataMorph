using System.Text;
using AwesomeAssertions;
using DataMorph.Engine.IO.JsonObject;

namespace DataMorph.Tests.Engine.IO.JsonObject;

public sealed partial class TopLevelScannerTests
{
    private static ReadOnlyMemory<byte> Utf8(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Scan_WithEmptyObject_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Scan_WithEmptyFile_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Scan_WithNonExistentFilePath_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"does_not_exist_{Guid.NewGuid()}.json");

        // Act
        var act = () => TopLevelScanner.Scan(nonExistentPath);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Scan_WithSinglePrimitiveString_ReturnsOneEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":\"v\"}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("k");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("\"v\"").ToArray());
    }

    [Fact]
    public void Scan_WithSinglePrimitiveNumber_ReturnsOneEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"n\":42}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("n");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("42").ToArray());
    }

    [Fact]
    public void Scan_WithNegativeNumber_ReturnsCorrectBytes()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"n\":-42}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("n");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("-42").ToArray());
    }

    [Fact]
    public void Scan_WithFloatingPointNumber_ReturnsCorrectBytes()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"n\":3.14}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("n");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("3.14").ToArray());
    }

    [Fact]
    public void Scan_WithEmptyStringValue_ReturnsCorrectBytes()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":\"\"}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("k");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("\"\"").ToArray());
    }

    [Fact]
    public void Scan_WithSingleNestedObject_ReturnsOneEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"o\":{\"a\":1}}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("o");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("{\"a\":1}").ToArray());
    }

    [Fact]
    public void Scan_WithSingleNestedArray_ReturnsOneEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"a\":[1,2]}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("a");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("[1,2]").ToArray());
    }

    [Theory]
    [InlineData("{\"k\":{}}", "{}")]
    [InlineData("{\"k\":[]}", "[]")]
    public void Scan_WithEmptyContainer_ReturnsCorrectBytes(string json, string expectedValue)
    {
        // Arrange
        File.WriteAllText(_testFilePath, json);

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("k");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8(expectedValue).ToArray());
    }

    [Fact]
    public void Scan_WithMultipleKeys_PreservesInsertionOrder()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"b\":2,\"a\":1}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(2);
        result[0].key.Should().Be("b");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("2").ToArray());
        result[1].key.Should().Be("a");
        result[1].value.ToArray().Should().BeEquivalentTo(Utf8("1").ToArray());
    }

    [Fact]
    public void Scan_WithDuplicateKeys_LastValueWins()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":1,\"k\":2}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("k");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("2").ToArray());
    }

    [Fact]
    public void Scan_WithTripleDuplicateKey_LastValueWins()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":1,\"k\":2,\"k\":3}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("k");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("3").ToArray());
    }

    [Fact]
    public void Scan_WithDuplicateKey_PositionStableWithOtherKeys()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"a\":1,\"k\":1,\"b\":2,\"k\":2}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(3);
        result[0].key.Should().Be("a");
        result[1].key.Should().Be("k");
        result[2].key.Should().Be("b");
        result[1].value.ToArray().Should().BeEquivalentTo(Utf8("2").ToArray());
    }

    [Fact]
    public void Scan_WithDeeplyNestedValue_CapturesCorrectByteRange()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"x\":{\"y\":{\"z\":1}}}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("x");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("{\"y\":{\"z\":1}}").ToArray());
    }

    [Fact]
    public void Scan_WithUnicodeKey_ReturnsCorrectEntry()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"日本語キー\":1}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("日本語キー");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("1").ToArray());
    }

    [Fact]
    public void Scan_WithEscapedCharacterInKey_ReturnsCorrectEntry()
    {
        // Arrange — JSON: {"key\"q\"":1}  →  key is: key"q"
        File.WriteAllText(_testFilePath, "{\"key\\\"q\\\"\":1}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("key\"q\"");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("1").ToArray());
    }

    [Theory]
    [InlineData("{\"k\":true}", "true")]
    [InlineData("{\"k\":false}", "false")]
    [InlineData("{\"k\":null}", "null")]
    public void Scan_WithScalarKeyword_ReturnsCorrectBytes(string json, string expectedValue)
    {
        // Arrange
        File.WriteAllText(_testFilePath, json);

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("k");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8(expectedValue).ToArray());
    }

    [Fact]
    public void Scan_WithFileSizeExceedingInitialBufferSize_CapturesCorrectBytes()
    {
        // Arrange — object with many small fields so the total exceeds 1 MB
        // without any single JSON string token exceeding the buffer
        var fields = string.Join(",", Enumerable.Range(0, 90_000).Select(i => $"\"f{i}\":{i}"));
        File.WriteAllText(_testFilePath, $"{{{fields}}}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(90_000);
        result[0].key.Should().Be("f0");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("0").ToArray());
        result[89_999].key.Should().Be("f89999");
        result[89_999].value.ToArray().Should().BeEquivalentTo(Utf8("89999").ToArray());
    }

    [Fact]
    public void Scan_WithLargeNestedValueSpanningBufferBoundary_CapturesCorrectBytes()
    {
        // Arrange — single nested object whose value exceeds the 1 MB initial buffer,
        // exercising the buffer-origin arithmetic when a value straddles buffer refills
        var innerFields = string.Join(",", Enumerable.Range(0, 100_000).Select(i => $"\"f{i}\":{i}"));
        var innerObject = $"{{{innerFields}}}";
        File.WriteAllText(_testFilePath, $"{{\"big\":{innerObject}}}");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("big");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8(innerObject).ToArray());
    }

    [Fact]
    public void Scan_WithStringValueExceedingMaxBufferSize_ThrowsNotSupportedException()
    {
        // Arrange — string value larger than 16 MB
        var oversizedString = new string('a', 16 * 1024 * 1024 + 1);
        File.WriteAllText(_testFilePath, $"{{\"k\":\"{oversizedString}\"}}");

        // Act
        var act = () => TopLevelScanner.Scan(_testFilePath);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Scan_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"k\":}");

        // Act
        var act = () => TopLevelScanner.Scan(_testFilePath);

        // Assert
        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void Scan_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var fields = string.Join(",", Enumerable.Range(0, 100_000).Select(i => $"\"f{i}\":{i}"));
        File.WriteAllText(_testFilePath, $"{{{fields}}}");
        cts.Cancel();

        // Act
        var act = () => TopLevelScanner.Scan(_testFilePath, cts.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Scan_WithPreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange — file exists and is valid, but the token is already cancelled
        // before Scan is invoked. The first ThrowIfCancellationRequested in the
        // loop will throw deterministically, regardless of timing.
        File.WriteAllText(_testFilePath, "{\"k\":1}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => TopLevelScanner.Scan(_testFilePath, cts.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Scan_WithLeadingTrailingWhitespace_SameResultAsCompactForm()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "  { \"k\" : 1 }  ");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().HaveCount(1);
        result[0].key.Should().Be("k");
        result[0].value.ToArray().Should().BeEquivalentTo(Utf8("1").ToArray());
    }

    [Fact]
    public void Scan_WithRootArray_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[1,2,3]");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Scan_WithRootPrimitive_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "42");

        // Act
        var result = TopLevelScanner.Scan(_testFilePath);

        // Assert
        result.Should().BeEmpty();
    }
}
