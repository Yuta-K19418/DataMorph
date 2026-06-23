using AwesomeAssertions;
using DataMorph.App;
using DataMorph.App.Views;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.App.Views;

public sealed class FocusedTableSourceTests
{
    private static readonly IReadOnlyList<JsonRawBytes> DefaultChildRawValues =
    [
        "{\"name\": \"Alice\", \"age\": 30}"u8.ToArray(),
        "{\"name\": \"Bob\", \"age\": 25}"u8.ToArray(),
    ];

    private static readonly TableSchema DefaultSchema = new()
    {
        Columns =
        [
            new ColumnSchema { Name = "name", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
            new ColumnSchema { Name = "age", Type = ColumnType.WholeNumber, IsNullable = false, ColumnIndex = 1 },
        ],
        SourceFormat = DataFormat.JsonLines,
    };

    private static DrillDownState CreateState(
        IReadOnlyList<JsonRawBytes>? childRawValues = null,
        TableSchema? schema = null,
        DataFormat format = DataFormat.JsonLines,
        long? recordPosition = 22) =>
        new(childRawValues ?? DefaultChildRawValues, schema ?? DefaultSchema, format, recordPosition);

    [Fact]
    public void Constructor_NullDrillDownState_ThrowsArgumentNullException()
    {
        // Arrange / Act
        var act = () => new FocusedTableSource(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rows_ReturnsChildCount()
    {
        // Arrange
        var source = new FocusedTableSource(CreateState());

        // Act
        var rows = source.Rows;

        // Assert
        rows.Should().Be(2);
    }

    [Fact]
    public void Columns_ReturnsSchemaColumnCountPlusOne()
    {
        // Arrange
        var source = new FocusedTableSource(CreateState());

        // Act
        var columns = source.Columns;

        // Assert
        columns.Should().Be(3);
    }

    [Fact]
    public void ColumnNames_ReturnsHashFollowedBySchemaColumnNames()
    {
        // Arrange
        var source = new FocusedTableSource(CreateState());

        // Act
        var columnNames = source.ColumnNames;

        // Assert
        columnNames.Should().Equal("#", "name", "age");
    }

    [Theory]
    [InlineData(0, "22:0")]
    [InlineData(1, "22:1")]
    public void Indexer_JsonLines_HashColumnFormatsAsLineNumberColonIndex(int row, string expected)
    {
        // Arrange
        var source = new FocusedTableSource(CreateState(format: DataFormat.JsonLines, recordPosition: 22));

        // Act
        var hashCell = source[row, 0];

        // Assert
        hashCell.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "5:0")]
    [InlineData(1, "5:1")]
    public void Indexer_JsonArray_HashColumnFormatsAsElementIndexColonIndex(int row, string expected)
    {
        // Arrange
        var source = new FocusedTableSource(CreateState(format: DataFormat.JsonArray, recordPosition: 5));

        // Act
        var hashCell = source[row, 0];

        // Assert
        hashCell.Should().Be(expected);
    }

    [Fact]
    public void Indexer_JsonObject_HashColumnFormatsAsBracketIndex()
    {
        // Arrange
        var source = new FocusedTableSource(CreateState(format: DataFormat.JsonObject, recordPosition: null));

        // Act
        var hashCell = source[0, 0];

        // Assert
        hashCell.Should().Be("[0]");
    }

    [Fact]
    public void Indexer_NonHashColumn_DelegatesToJsonObjectCellExtractor()
    {
        // Arrange
        var source = new FocusedTableSource(CreateState());

        // Act
        var cell = source[0, 1];

        // Assert
        cell.Should().Be("Alice");
    }

    [Fact]
    public void Indexer_NonHashColumn_SecondColumn_ReturnsCorrectValue()
    {
        // Arrange
        var source = new FocusedTableSource(CreateState());

        // Act
        var cell = source[0, 2];

        // Assert
        cell.Should().Be("30");
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 3)]
    public void Indexer_OutOfBounds_ThrowsArgumentOutOfRangeException(int row, int col)
    {
        // Arrange
        var source = new FocusedTableSource(CreateState());

        // Act
        var act = () => source[row, col];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
