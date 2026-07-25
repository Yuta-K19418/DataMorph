using AwesomeAssertions;
using DataMorph.App;
using DataMorph.App.Views;
using DataMorph.Engine.IO.DrillDown;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.App.Views;

public sealed class FocusedTableSourceTests
{
    private static readonly IReadOnlyList<FocusedTableRow> DefaultRows =
    [
        new FocusedTableRow("{\"name\": \"Alice\", \"age\": 30}"u8.ToArray(), "[0]"),
        new FocusedTableRow("{\"name\": \"Bob\", \"age\": 25}"u8.ToArray(), "[1]"),
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
        IReadOnlyList<FocusedTableRow>? rows = null,
        TableSchema? schema = null) =>
        new(rows ?? DefaultRows, schema ?? DefaultSchema, ViewMode.JsonLinesTree);

    [Fact]
    public void Constructor_NullDrillDownState_ThrowsArgumentNullException()
    {
        // Arrange / Act
        var act = () => new FocusedTableSource(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rows_ReturnsRowCount()
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
    [InlineData(0, "[0]")]
    [InlineData(1, "[1]")]
    public void Indexer_HashColumn_ReturnsRowHashValue(int row, string expected)
    {
        // Arrange
        var source = new FocusedTableSource(CreateState());

        // Act
        var hashCell = source[row, 0];

        // Assert
        hashCell.Should().Be(expected);
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
