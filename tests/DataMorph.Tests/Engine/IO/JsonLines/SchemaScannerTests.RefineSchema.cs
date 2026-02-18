using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Engine.IO.JsonLines;

public sealed partial class SchemaScannerTests
{
    [Fact]
    public void RefineSchema_NewKeyInLine_AddsNullableColumn()
    {
        // Arrange
        var initialSchema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        var line = Line("{\"a\": 1, \"b\": \"new\"}");

        // Act
        var result = SchemaScanner.RefineSchema(initialSchema, line.Span);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Should().HaveCount(2);
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, false);
        AssertColumn(result.Value, 1, "b", ColumnType.Text, true);
    }

    [Fact]
    public void RefineSchema_ExistingColumnAbsentInLine_MarksNullable()
    {
        // Arrange
        var initialSchema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        var line = Line("{}"); // 'a' is missing

        // Act
        var result = SchemaScanner.RefineSchema(initialSchema, line.Span);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, true);
    }

    [Fact]
    public void RefineSchema_TypeConflict_ResolvesViaColumnTypeResolver()
    {
        // Arrange
        var initialSchema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        var line = Line("{\"a\": 1.5}"); // Conflict: WholeNumber vs FloatingPoint

        // Act
        var result = SchemaScanner.RefineSchema(initialSchema, line.Span);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.FloatingPoint, false);
    }

    [Fact]
    public void RefineSchema_MalformedJsonLine_ReturnsOriginalSchemaUnchanged()
    {
        // Arrange
        var initialSchema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        var line = new ReadOnlyMemory<byte>("malformed"u8.ToArray());

        // Act
        var result = SchemaScanner.RefineSchema(initialSchema, line.Span);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(initialSchema);
    }

    [Fact]
    public void RefineSchema_EmptySpan_ReturnsOriginalSchemaUnchanged()
    {
        // Arrange
        var initialSchema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };

        // Act
        var result = SchemaScanner.RefineSchema(initialSchema, []);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(initialSchema);
    }
}
