using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Engine.IO.JsonLines;

public sealed partial class SchemaScannerTests
{
    [Theory]
    [InlineData("1", ColumnType.WholeNumber, false)]
    [InlineData("1.5", ColumnType.FloatingPoint, false)]
    [InlineData("\"text\"", ColumnType.Text, false)]
    [InlineData("true", ColumnType.Boolean, false)]
    [InlineData("false", ColumnType.Boolean, false)]
    [InlineData("null", ColumnType.Text, true)] // null: type defaults to Text, always nullable
    public void ScanSchema_SingleValue_InfersCorrectType(
        string value,
        ColumnType expectedType,
        bool expectedNullable
    )
    {
        // Arrange
        var json = $"{{\"a\": {value}}}";
        ReadOnlyMemory<byte>[] lines = [Line(json)];

        // Act
        var result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", expectedType, expectedNullable);
    }

    [Theory]
    [InlineData("1", "1.5", ColumnType.FloatingPoint)]
    [InlineData("1", "\"text\"", ColumnType.Text)]
    [InlineData("true", "false", ColumnType.Boolean)]
    [InlineData("1", "true", ColumnType.Text)]
    public void ScanSchema_TypeConflict_ResolvesExpectedColumnType(
        string val1,
        string val2,
        ColumnType expectedType
    )
    {
        // Arrange
        var json1 = $"{{\"a\": {val1}}}";
        var json2 = $"{{\"a\": {val2}}}";
        ReadOnlyMemory<byte>[] lines = [Line(json1), Line(json2)];

        // Act
        var result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", expectedType, false);
    }

    [Fact]
    public void ScanSchema_MultiLineConsistentTypes_PreservesTypes()
    {
        // Arrange
        ReadOnlyMemory<byte>[] lines =
        [
            Line("{\"a\": 1, \"b\": \"text\"}"),
            Line("{\"a\": 2, \"b\": \"text2\"}"),
        ];

        // Act
        var result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, false);
        AssertColumn(result.Value, 1, "b", ColumnType.Text, false);
    }

    [Fact]
    public void ScanSchema_MissingKeyInSomeRows_MarksNullable()
    {
        // Arrange
        ReadOnlyMemory<byte>[] lines =
        [
            Line("{\"a\": 1}"),
            Line("{\"a\": 2}"),
            Line("{}"), // 'a' is missing
        ];

        // Act
        var result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, true);
    }

    [Fact]
    public void ScanSchema_NewKeyInLaterRow_AddsNullableColumn()
    {
        // Arrange
        ReadOnlyMemory<byte>[] lines = [Line("{\"a\": 1}"), Line("{\"a\": 2, \"b\": \"text\"}")];

        // Act
        var result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, false);
        AssertColumn(result.Value, 1, "b", ColumnType.Text, true);
    }

    [Fact]
    public void ScanSchema_NullValueInRow_TypeUnchangedAndNullable()
    {
        // Arrange
        ReadOnlyMemory<byte>[] lines = [Line("{\"a\": 1}"), Line("{\"a\": null}")];

        // Act
        var result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, true);
    }

    [Fact]
    public void ScanSchema_EmptyInput_ReturnsFailure()
    {
        // Arrange
        var lines = Array.Empty<ReadOnlyMemory<byte>>();

        // Act
        var result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ScanSchema_AllLinesMalformed_ReturnsFailure()
    {
        // Arrange
        ReadOnlyMemory<byte>[] lines =
        [
            new ReadOnlyMemory<byte>("not json"u8.ToArray()),
            new ReadOnlyMemory<byte>("also not json"u8.ToArray()),
        ];

        // Act
        var result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ScanSchema_NegativeInitialScanCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        ReadOnlyMemory<byte>[] lines = [Line("{\"a\": 1}")];

        // Act
        Action act = () => SchemaScanner.ScanSchema(lines, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
