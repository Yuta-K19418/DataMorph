using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DataMorph.Engine.IO.Json;

namespace DataMorph.Tests.Engine.IO.Json;

/// <summary>
/// Tests for the <see cref="JsonByteExtractor"/> class.
/// </summary>
public sealed class JsonByteExtractorTests
{
    [Fact]
    public void ExtractNestedBytes_ForSimpleObject_ReturnsCorrectByteSlice()
    {
        // Arrange
        var json = "{\"a\": 1}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var slice = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_ForSimpleArray_ReturnsCorrectByteSlice()
    {
        // Arrange
        var json = "[1, 2]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        var slice = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_ForComplexNestedStructure_ReturnsCorrectByteSlice()
    {
        // Arrange
        var json = "{\"a\": [1, 2], \"b\": {\"c\": 3}}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var slice = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_WithLargerContext_ReturnsOnlyTargetStructure()
    {
        // Arrange
        var fullJson = "{\"root\": {\"id\": 1, \"value\": \"test\"}, \"other\": 2}";
        var rawJson = Encoding.UTF8.GetBytes(fullJson);
        var reader = new Utf8JsonReader(rawJson);

        reader.Read(); // StartObject (root)
        reader.Read(); // PropertyName "root"
        reader.Read(); // StartObject (target nested object)

        var expectedNestedJson = "{\"id\": 1, \"value\": \"test\"}";
        var expectedBytes = Encoding.UTF8.GetBytes(expectedNestedJson);

        // Act
        var slice = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(expectedBytes);
    }
}
