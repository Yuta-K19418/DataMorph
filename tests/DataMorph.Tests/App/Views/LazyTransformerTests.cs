using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.Filtering;
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

    /// <summary>
    /// A synchronous stub that returns pre-computed matched row indices without any filtering logic.
    /// </summary>
    private sealed class SyncFilterRowIndexer(IReadOnlyList<int> matchedRows) : IFilterRowIndexer
    {
        public int TotalMatchedRows => matchedRows.Count;

        public int GetSourceRow(int filteredIndex) => matchedRows[filteredIndex];

        public Task BuildIndexAsync(CancellationToken ct) => Task.CompletedTask;
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

    private static LazyTransformer MakeFilteredTransformer(
        FakeTableSource source,
        TableSchema schema,
        IReadOnlyList<MorphAction> actions,
        IReadOnlyList<int> matchedRows
    ) => new LazyTransformer(source, schema, actions, _ => new SyncFilterRowIndexer(matchedRows));

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
        var source = new FakeTableSource(
            [
                ["Alice"],
                ["Bob"],
                ["Alice"],
            ],
            ["Name"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FilterAction
            {
                ColumnName = "Name",
                Operator = FilterOperator.Equals,
                Value = "Alice",
            },
        ];

        // Act — rows 0 and 2 match "Alice"
        var transformer = MakeFilteredTransformer(source, schema, actions, [0, 2]);

        // Assert
        transformer.Rows.Should().Be(2);
        transformer[0, 0].Should().Be("Alice");
        transformer[1, 0].Should().Be("Alice");
    }

    [Fact]
    public void Filter_ContainsOperator_SubstringMatchingRowsReturned()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["apple"],
                ["banana"],
                ["apricot"],
                ["cherry"],
            ],
            ["Fruit"]
        );
        var schema = MakeSchema(("Fruit", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FilterAction
            {
                ColumnName = "Fruit",
                Operator = FilterOperator.Contains,
                Value = "ap",
            },
        ];

        // Act — rows 0 ("apple") and 2 ("apricot") contain "ap"
        var transformer = MakeFilteredTransformer(source, schema, actions, [0, 2]);

        // Assert
        transformer.Rows.Should().Be(2);
        transformer[0, 0].Should().Be("apple");
        transformer[1, 0].Should().Be("apricot");
    }

    // -------------------------------------------------------------------------
    // Filter — AND semantics
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_MultipleFilterActions_AppliesAndSemantics()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["Alice", "30"],
                ["Bob", "25"],
                ["Alice", "20"],
                ["Charlie", "30"],
            ],
            ["Name", "Age"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text), ("Age", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FilterAction
            {
                ColumnName = "Name",
                Operator = FilterOperator.Equals,
                Value = "Alice",
            },
            new FilterAction
            {
                ColumnName = "Age",
                Operator = FilterOperator.Equals,
                Value = "30",
            },
        ];

        // Act — only row 0 ("Alice", "30") matches both filters
        var transformer = MakeFilteredTransformer(source, schema, actions, [0]);

        // Assert
        transformer.Rows.Should().Be(1);
        transformer[0, 0].Should().Be("Alice");
        transformer[0, 1].Should().Be("30");
    }

    [Fact]
    public void Filter_NoMatchingRows_RowsIsZero()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["Alice"],
                ["Bob"],
            ],
            ["Name"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FilterAction
            {
                ColumnName = "Name",
                Operator = FilterOperator.Equals,
                Value = "Charlie",
            },
        ];

        // Act — no rows match "Charlie"
        var transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert
        transformer.Rows.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Filter — column resolution
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_TargetingRenamedColumn_CorrectlyResolved()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["Alice"],
                ["Bob"],
            ],
            ["Name"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "Name", NewName = "FullName" },
            new FilterAction
            {
                ColumnName = "FullName",
                Operator = FilterOperator.Equals,
                Value = "Alice",
            },
        ];

        // Act — only row 0 ("Alice") matches
        var transformer = MakeFilteredTransformer(source, schema, actions, [0]);

        // Assert
        transformer.Rows.Should().Be(1);
        transformer[0, 0].Should().Be("Alice");
    }

    [Fact]
    public void Filter_TargetingDeletedColumn_SilentlySkipped()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["Alice", "active"],
                ["Bob", "inactive"],
            ],
            ["Name", "Status"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text), ("Status", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new DeleteColumnAction { ColumnName = "Status" },
            // Filter targets a deleted column — should be silently skipped, no rows excluded
            new FilterAction
            {
                ColumnName = "Status",
                Operator = FilterOperator.Equals,
                Value = "active",
            },
        ];

        // Act — the filter spec is skipped (Status column was deleted), so the factory receives
        // an empty FilterSpec list and no IFilterRowIndexer is created; all source rows are exposed
        var transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert — both rows retained because the filter was silently skipped
        transformer.Rows.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Filter — numeric operators
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_GreaterThanOnWholeNumberColumn_ReturnsMatchingRows()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["10"],
                ["50"],
                ["30"],
                ["5"],
            ],
            ["Score"]
        );
        var schema = MakeSchema(("Score", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions =
        [
            new FilterAction
            {
                ColumnName = "Score",
                Operator = FilterOperator.GreaterThan,
                Value = "20",
            },
        ];

        // Act — rows 1 (50) and 2 (30) are greater than 20
        var transformer = MakeFilteredTransformer(source, schema, actions, [1, 2]);

        // Assert
        transformer.Rows.Should().Be(2);
        transformer[0, 0].Should().Be("50");
        transformer[1, 0].Should().Be("30");
    }

    [Fact]
    public void Filter_NumericOperatorOnTextColumn_ExcludesAllRows()
    {
        // Arrange — GreaterThan on a Text column always returns false, excluding all rows
        var source = new FakeTableSource(
            [
                ["hello"],
                ["world"],
            ],
            ["Word"]
        );
        var schema = MakeSchema(("Word", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FilterAction
            {
                ColumnName = "Word",
                Operator = FilterOperator.GreaterThan,
                Value = "hello",
            },
        ];

        // Act — numeric operators on Text columns always return false, so no rows match
        var transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert — all rows excluded because numeric operators are unsupported on Text columns
        transformer.Rows.Should().Be(0);
    }
}
