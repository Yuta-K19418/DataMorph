using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.Engine.IO.JsonLines;

public sealed class CellExtractorTests
{
    [Fact]
    public void ExtractCell_StringValue_ReturnsUnquotedString()
    {
        // Arrange
        var line = "{\"name\": \"Alice\"}"u8;
        var columnName = "name"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("Alice");
    }

    [Fact]
    public void ExtractCell_IntegerValue_ReturnsNumberAsString()
    {
        // Arrange
        var line = "{\"id\": 42}"u8;
        var columnName = "id"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public void ExtractCell_DecimalValue_ReturnsNumberAsString()
    {
        // Arrange
        var line = "{\"price\": 3.14}"u8;
        var columnName = "price"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("3.14");
    }

    [Fact]
    public void ExtractCell_TrueValue_ReturnsTrueString()
    {
        // Arrange
        var line = "{\"active\": true}"u8;
        var columnName = "active"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("True");
    }

    [Fact]
    public void ExtractCell_FalseValue_ReturnsFalseString()
    {
        // Arrange
        var line = "{\"active\": false}"u8;
        var columnName = "active"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("False");
    }

    [Fact]
    public void ExtractCell_NullValue_ReturnsNullPlaceholder()
    {
        // Arrange
        var line = "{\"value\": null}"u8;
        var columnName = "value"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("<null>");
    }

    [Fact]
    public void ExtractCell_NestedObject_ReturnsCollapsedPreview()
    {
        // Arrange
        var line = "{\"address\": {\"city\": \"Tokyo\"}}"u8;
        var columnName = "address"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("{...}");
    }

    [Fact]
    public void ExtractCell_Array_ReturnsCollapsedPreview()
    {
        // Arrange
        var line = "{\"tags\": [\"a\", \"b\"]}"u8;
        var columnName = "tags"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("[...]");
    }

    [Fact]
    public void ExtractCell_MissingKey_ReturnsNullPlaceholder()
    {
        // Arrange
        var line = "{\"id\": 1}"u8;
        var columnName = "name"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("<null>");
    }

    [Fact]
    public void ExtractCell_MalformedJson_ReturnsErrorPlaceholder()
    {
        // Arrange
        var line = "not-json"u8;
        var columnName = "id"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("<error>");
    }

    [Fact]
    public void ExtractCell_EmptyLine_ReturnsErrorPlaceholder()
    {
        // Arrange
        ReadOnlySpan<byte> line = [];
        var columnName = "id"u8;

        // Act
        var result = CellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("<error>");
    }
}
