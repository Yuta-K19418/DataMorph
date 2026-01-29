using AwesomeAssertions;
using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.IO;

public sealed partial class CsvSchemaScannerTests
{
    [Fact]
    public void RefineSchema_CallsUpdateColumnTypeOnly_WhenNonNullableColumnHasValidValue()
    {
        // Arrange
        var schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                IsNullable = false,
                ColumnIndex = 0,
            },
        ]);

        var row = new[] { "999".AsMemory() };

        // Act
        var result = CsvSchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsSuccess.Should().BeTrue();
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void RefineSchema_CallsMarkNullableOnly_WhenNonNullableColumnHasEmptyValue()
    {
        // Arrange
        var schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                IsNullable = false,
                ColumnIndex = 0,
            },
        ]);

        var row = new[] { "".AsMemory() };

        // Act
        var result = CsvSchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsSuccess.Should().BeTrue();
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void RefineSchema_MismatchedColumnCount_ReturnsFailure()
    {
        // Arrange
        var schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                ColumnIndex = 0,
            },
            new ColumnSchema
            {
                Name = "name",
                Type = ColumnType.Text,
                ColumnIndex = 1,
            },
        ]);

        var row = new[] { "123".AsMemory() };

        // Act
        var result = CsvSchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Row has 1 columns but schema has 2 columns");
    }

    [Fact]
    public void RefineSchema_NullSchema_ThrowsException()
    {
        // Arrange
        var row = new[] { "123".AsMemory() };

        // Act & Assert
        Action action = () => CsvSchemaScanner.RefineSchema(null!, row);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RefineSchema_NullRow_ThrowsException()
    {
        // Arrange
        var schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                ColumnIndex = 0,
            },
        ]);

        // Act & Assert
        Action action = () => CsvSchemaScanner.RefineSchema(schema, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    private static TableSchema CreateTestSchema(ColumnSchema[] columns)
    {
        return new TableSchema
        {
            Columns = columns,
            RowCount = 0,
            SourceFormat = DataFormat.Csv,
        };
    }
}
