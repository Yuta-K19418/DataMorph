using AwesomeAssertions;
using DataMorph.Engine;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Engine;

public sealed class ActionApplierTests
{
    [Fact]
    public void BuildOutputSchema_WithNoActions_ReturnsAllColumnsAndNoFilters()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, []);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("A");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithRenameAction_UpdatesOutputName()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
                new ColumnSchema { Name = "B", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 1 },
            ],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new RenameColumnAction { OldName = "A", NewName = "RenamedA" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(2);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("RenamedA");
        result.Columns[1].SourceName.Should().Be("B");
        result.Columns[1].OutputName.Should().Be("B");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithRenameAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new RenameColumnAction { OldName = "NonExistent", NewName = "NewName" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("A");
    }

    [Fact]
    public void BuildOutputSchema_WithDeleteAction_RemovesColumn()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
                new ColumnSchema { Name = "B", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 1 },
            ],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new DeleteColumnAction { ColumnName = "A" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("B");
        result.Columns[0].OutputName.Should().Be("B");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithDeleteAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
                new ColumnSchema { Name = "B", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 1 },
            ],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new DeleteColumnAction { ColumnName = "NonExistent" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(2);
    }

    [Fact]
    public void BuildOutputSchema_WithFilterAction_AddsFilterSpec()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new FilterAction { ColumnName = "A", Operator = FilterOperator.Equals, Value = "test" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Filters.Should().HaveCount(1);
        result.Filters[0].SourceColumnIndex.Should().Be(0);
        result.Filters[0].ColumnType.Should().Be(ColumnType.Text);
        result.Filters[0].Operator.Should().Be(FilterOperator.Equals);
        result.Filters[0].Value.Should().Be("test");
    }

    [Fact]
    public void BuildOutputSchema_WithFilterAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new FilterAction { ColumnName = "NonExistent", Operator = FilterOperator.Equals, Value = "test" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithFilterOnDeletedColumn_SkipsSilently()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
                new ColumnSchema { Name = "B", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 1 },
            ],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new DeleteColumnAction { ColumnName = "B" },
            new FilterAction { ColumnName = "B", Operator = FilterOperator.Equals, Value = "test" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithCastAction_DoesNotAffectColumnInclusion()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("A");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithCastThenFilterOnSameColumn_UsesPostCastType()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
            new FilterAction { ColumnName = "A", Operator = FilterOperator.GreaterThan, Value = "10" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Filters.Should().HaveCount(1);
        result.Filters[0].SourceColumnIndex.Should().Be(0);
        result.Filters[0].ColumnType.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void BuildOutputSchema_PreservesColumnOrder()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
                new ColumnSchema { Name = "B", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 1 },
                new ColumnSchema { Name = "C", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 2 },
            ],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new DeleteColumnAction { ColumnName = "B" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(2);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[1].SourceName.Should().Be("C");
    }

    [Fact]
    public void BuildOutputSchema_WithCastAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new CastColumnAction { ColumnName = "NonExistent", TargetType = ColumnType.WholeNumber }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithMultipleFilterActions_AddsAllFilterSpecs()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
                new ColumnSchema { Name = "B", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 1 },
            ],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new FilterAction { ColumnName = "A", Operator = FilterOperator.Equals, Value = "value1" },
            new FilterAction { ColumnName = "B", Operator = FilterOperator.Contains, Value = "value2" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Filters.Should().HaveCount(2);
        result.Filters[0].SourceColumnIndex.Should().Be(0);
        result.Filters[1].SourceColumnIndex.Should().Be(1);
    }

    [Fact]
    public void BuildOutputSchema_WithChainedRenameActions_AppliesAllRenamesInOrder()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "B" },
            new RenameColumnAction { OldName = "B", NewName = "C" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("C");
    }

    [Fact]
    public void BuildOutputSchema_WithNullSchema_ThrowsArgumentNullException()
    {
        // Arrange
        MorphAction[] actions = [];

        // Act
        var act = () => ActionApplier.BuildOutputSchema(null!, actions);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildOutputSchema_WithNullActions_ThrowsArgumentNullException()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };

        // Act
        var act = () => ActionApplier.BuildOutputSchema(schema, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildOutputSchema_WithAllColumnsDeleted_ReturnsEmptyColumnsAndNoFilters()
    {
        // Arrange — delete the only column; output schema should have no columns and no filters
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new DeleteColumnAction { ColumnName = "A" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().BeEmpty();
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithCastRenameThenFilter_UsesPostCastTypeAndRenamedName()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
                new ColumnSchema { Name = "B", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 1 },
            ],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
            new RenameColumnAction { OldName = "A", NewName = "RenamedA" },
            new FilterAction { ColumnName = "RenamedA", Operator = FilterOperator.GreaterThan, Value = "10" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(2);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("RenamedA");
        result.Filters.Should().HaveCount(1);
        result.Filters[0].ColumnType.Should().Be(ColumnType.WholeNumber);
    }

    // -------------------------------------------------------------------------
    // FillColumnAction
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildOutputSchema_WithFillAction_SingleColumn_AttachesTransformToColumn()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "Email", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new FillColumnAction { ColumnName = "Email", Value = "REDACTED" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("Email");
        result.Columns[0].OutputName.Should().Be("Email");
        result.Columns[0].Transform.Should().BeOfType<FillSpec>().Which.Value.Should().Be("REDACTED");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithFillAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions = [new FillColumnAction { ColumnName = "NonExistent", Value = "FILL" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].Transform.Should().BeNull();
    }

    [Fact]
    public void BuildOutputSchema_WithMultipleActions_IncludingFill_AppliesAllCorrectly()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { Name = "Name", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 },
                new ColumnSchema { Name = "Email", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 1 },
                new ColumnSchema { Name = "Age", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 2 },
            ],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new RenameColumnAction { OldName = "Name", NewName = "FullName" },
            new FillColumnAction { ColumnName = "Email", Value = "REDACTED" },
            new DeleteColumnAction { ColumnName = "Age" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(2);
        result.Columns[0].SourceName.Should().Be("Name");
        result.Columns[0].OutputName.Should().Be("FullName");
        result.Columns[0].Transform.Should().BeNull();
        result.Columns[1].SourceName.Should().Be("Email");
        result.Columns[1].Transform.Should().BeOfType<FillSpec>().Which.Value.Should().Be("REDACTED");
    }

    [Fact]
    public void BuildOutputSchema_WithRenameAndFill_FillTargetsRenamedColumn()
    {
        // Arrange — rename first, then fill using the new name; transform must be attached
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "Email", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new RenameColumnAction { OldName = "Email", NewName = "EmailAddress" },
            new FillColumnAction { ColumnName = "EmailAddress", Value = "REDACTED" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("Email");
        result.Columns[0].OutputName.Should().Be("EmailAddress");
        result.Columns[0].Transform.Should().BeOfType<FillSpec>().Which.Value.Should().Be("REDACTED");
    }

    [Fact]
    public void BuildOutputSchema_WithTwoFillsOnSameColumn_LastFillWins()
    {
        // Arrange — two consecutive fills on the same column; second value must win
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "Status", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new FillColumnAction { ColumnName = "Status", Value = "FIRST" },
            new FillColumnAction { ColumnName = "Status", Value = "SECOND" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].Transform.Should().BeOfType<FillSpec>().Which.Value.Should().Be("SECOND");
    }

    [Fact]
    public void BuildOutputSchema_WithFillAction_OnDeletedColumn_SkipsSilently()
    {
        // Arrange — delete the column first; subsequent fill on the same (now removed) column is a no-op
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "Email", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new DeleteColumnAction { ColumnName = "Email" },
            new FillColumnAction { ColumnName = "Email", Value = "REDACTED" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().BeEmpty();
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithFillThenDelete_ColumnIsRemoved()
    {
        // Arrange — fill column, then delete it; column should not appear in output
        var schema = new TableSchema
        {
            Columns = [new ColumnSchema { Name = "A", Type = ColumnType.Text, IsNullable = false, ColumnIndex = 0 }],
            SourceFormat = DataFormat.Csv,
        };
        MorphAction[] actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "FILLED" },
            new DeleteColumnAction { ColumnName = "A" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(schema, actions);

        // Assert
        result.Columns.Should().BeEmpty();
        result.Filters.Should().BeEmpty();
    }
}
