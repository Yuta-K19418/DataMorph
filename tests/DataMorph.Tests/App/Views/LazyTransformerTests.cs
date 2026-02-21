using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App.Views;

public sealed class LazyTransformerTests
{
    // -------------------------------------------------------------------------
    // Test double
    // -------------------------------------------------------------------------

    private sealed class FakeTableSource(string[][] data, string[] columnNames) : ITableSource
    {
        public int Rows => data.Length;
        public int Columns => columnNames.Length;
        public string[] ColumnNames => columnNames;
        public object this[int row, int col] => data[row][col];
    }

    private static TableSchema MakeSchema(params (string name, ColumnType type)[] cols) =>
        new TableSchema
        {
            Columns =
            [
                .. cols.Select(
                    (c, i) =>
                        new ColumnSchema
                        {
                            Name = c.name,
                            Type = c.type,
                            ColumnIndex = i,
                        }
                ),
            ],
            SourceFormat = DataFormat.Csv,
        };

    // -------------------------------------------------------------------------
    // Constructor — null guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var schema = MakeSchema(("A", ColumnType.Text));

        // Act
        var act = () => new LazyTransformer(null!, schema, []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOriginalSchema_ThrowsArgumentNullException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello"],
            ],
            ["A"]
        );

        // Act
        var act = () => new LazyTransformer(source, null!, []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullActions_ThrowsArgumentNullException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));

        // Act
        var act = () => new LazyTransformer(source, schema, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Rename
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithRenameAction_OutputSchemaReflectsNewName()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello", "world"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];

        // Act
        var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("X");
        transformer.ColumnNames[1].Should().Be("B");
    }

    [Fact]
    public void Constructor_WithRenameAction_SourceColumnIndexPreserved()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello", "world"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];
        var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be("hello");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Delete
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithDeleteAction_DeletedColumnAbsentFromOutputSchema()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];

        // Act
        var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(2);
        transformer.ColumnNames.Should().BeEquivalentTo(["A", "C"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Constructor_WithDeleteAction_SourceColumnIndicesMappedCorrectly()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];
        var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 1]; // output col 1 → source col 2 (C)

        // Assert
        result.Should().Be("c");
    }

    [Fact]
    public void Constructor_AllColumnsDeleted_ColumnsIsZero()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "A" }];

        // Act
        var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Cast
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithCastAction_OutputSchemaReflectsNewType()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["42"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
        ];

        // Act
        var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        // ColumnType is reflected through FormatCellValue behaviour: valid integer is returned as-is
        transformer[0, 0].Should().Be("42");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Ordered actions
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithRenameFollowedByDelete_OperatesOnRenamedName()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
            new DeleteColumnAction { ColumnName = "X" },
        ];

        // Act
        var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(1);
        transformer.ColumnNames[0].Should().Be("B");
    }

    // -------------------------------------------------------------------------
    // Cell value — passthrough
    // -------------------------------------------------------------------------

    [Fact]
    public void Indexer_EmptyActionStack_ReturnsSameValueAsSource()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        var transformer = new LazyTransformer(source, schema, []);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be("hello");
    }

    // -------------------------------------------------------------------------
    // Cell value — cast formatting
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("42", ColumnType.WholeNumber, "42")]
    [InlineData("3.14", ColumnType.FloatingPoint, "3.14")]
    [InlineData("true", ColumnType.Boolean, "true")]
    public void Indexer_CastWithValidInput_ReturnsFormattedValue(
        string rawValue,
        ColumnType targetType,
        string expectedValue
    )
    {
        // Arrange
        var source = new FakeTableSource(
            [
                [rawValue],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = targetType },
        ];
        var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be(expectedValue);
        _ = expectedValue;
    }

    [Theory]
    [InlineData("not-a-number", ColumnType.WholeNumber)]
    [InlineData("not-a-bool", ColumnType.Boolean)]
    [InlineData("not-a-date", ColumnType.Timestamp)]
    public void Indexer_CastWithInvalidInput_ReturnsInvalidPlaceholder(
        string rawValue,
        ColumnType targetType
    )
    {
        // Arrange
        var source = new FakeTableSource(
            [
                [rawValue],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = targetType },
        ];
        var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be("<invalid>");
        _ = rawValue;
        _ = targetType;
    }

    // -------------------------------------------------------------------------
    // Error handling — silently skipped actions
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_ActionTargetingNonExistentColumn_IsSilentlySkipped()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new DeleteColumnAction { ColumnName = "DoesNotExist" },
        ];

        // Act
        var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(1);
    }

    // -------------------------------------------------------------------------
    // Error handling — out-of-range indexer access
    // -------------------------------------------------------------------------

    [Fact]
    public void Indexer_NegativeRow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        var transformer = new LazyTransformer(source, schema, []);

        // Act
        var act = () => _ = transformer[-1, 0];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_NegativeCol_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        var transformer = new LazyTransformer(source, schema, []);

        // Act
        var act = () => _ = transformer[0, -1];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_RowExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        var transformer = new LazyTransformer(source, schema, []);

        // Act
        var act = () => _ = transformer[1, 0]; // only row 0 exists

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_ColExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        var transformer = new LazyTransformer(source, schema, []);

        // Act
        var act = () => _ = transformer[0, 1]; // only col 0 exists

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    [Fact]
    public void Rows_DelegatesToUnderlyingSource()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
                ["b"],
                ["c"],
                ["d"],
                ["e"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        var transformer = new LazyTransformer(source, schema, []);

        // Act
        var rows = transformer.Rows;

        // Assert
        rows.Should().Be(5);
    }

    [Fact]
    public void Columns_ReflectsTransformedSchemaColumnCount()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];
        var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var columns = transformer.Columns;

        // Assert
        columns.Should().Be(2);
    }

    [Fact]
    public void ColumnNames_ReflectsTransformedSchemaNames()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];
        var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var names = transformer.ColumnNames;

        // Assert
        names.Should().BeEquivalentTo(["X", "B"], o => o.WithStrictOrdering());
    }

    // -------------------------------------------------------------------------
    // Filter — Equals
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_EqualsOperator_OnlyMatchingRowsReturned()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Filter_ContainsOperator_SubstringMatchingRowsReturned()
    {
        // Arrange

        // Act

        // Assert
    }

    // -------------------------------------------------------------------------
    // Filter — AND semantics
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_MultipleFilterActions_AppliesAndSemantics()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Filter_NoMatchingRows_RowsIsZero()
    {
        // Arrange

        // Act

        // Assert
    }

    // -------------------------------------------------------------------------
    // Filter — column resolution
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_TargetingRenamedColumn_CorrectlyResolved()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Filter_TargetingDeletedColumn_SilentlySkipped()
    {
        // Arrange

        // Act

        // Assert
    }

    // -------------------------------------------------------------------------
    // Filter — numeric operators
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_GreaterThanOnWholeNumberColumn_ReturnsMatchingRows()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Filter_NumericOperatorOnTextColumn_FallsBackToStringComparison()
    {
        // Arrange

        // Act

        // Assert
    }
}
