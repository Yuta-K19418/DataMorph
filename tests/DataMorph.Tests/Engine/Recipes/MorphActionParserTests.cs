using AwesomeAssertions;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Recipes;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Engine.Recipes;

public sealed class MorphActionParserTests
{
    [Fact]
    public void ParseAction_WithMissingTypeField_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "columnName", "Age" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Missing action type");
    }

    [Fact]
    public void ParseAction_WithUnknownType_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "explode" },
            { "columnName", "Age" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unknown action type: 'explode'");
    }

    [Fact]
    public void ParseAction_RenameAction_WithMissingOldName_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "rename" },
            { "newName", "NewAge" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'oldName'");
    }

    [Fact]
    public void ParseAction_RenameAction_WithMissingNewName_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "rename" },
            { "oldName", "Age" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'newName'");
    }

    [Fact]
    public void ParseAction_ValidRenameAction_ReturnsSuccess()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "rename" },
            { "oldName", "Age" },
            { "newName", "years" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("Age");
        action.NewName.Should().Be("years");
    }

    [Fact]
    public void ParseAction_ValidDeleteAction_ReturnsSuccess()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "delete" },
            { "columnName", "Age" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Should().BeOfType<DeleteColumnAction>().Subject;
        action.ColumnName.Should().Be("Age");
    }

    [Fact]
    public void ParseAction_DeleteAction_WithMissingColumnName_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string> { { "type", "delete" } };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'columnName'");
    }

    [Fact]
    public void ParseAction_ValidCastAction_ReturnsSuccess()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "cast" },
            { "columnName", "Age" },
            { "targetType", "WholeNumber" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Should().BeOfType<CastColumnAction>().Subject;
        action.ColumnName.Should().Be("Age");
        action.TargetType.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void ParseAction_CastAction_WithInvalidTargetType_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "cast" },
            { "columnName", "Age" },
            { "targetType", "NotAType" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for targetType");
    }

    [Fact]
    public void ParseAction_CastAction_WithWrongCaseTargetType_ReturnsFailure()
    {
        // Arrange — targetType is case-sensitive; "wholenumber" is not a valid value
        var fields = new Dictionary<string, string>
        {
            { "type", "cast" },
            { "columnName", "Age" },
            { "targetType", "wholenumber" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for targetType");
    }

    [Theory]
    [InlineData("Contains", FilterOperator.Contains)]
    [InlineData("NotContains", FilterOperator.NotContains)]
    [InlineData("StartsWith", FilterOperator.StartsWith)]
    [InlineData("EndsWith", FilterOperator.EndsWith)]
    [InlineData("Equals", FilterOperator.Equals)]
    [InlineData("NotEquals", FilterOperator.NotEquals)]
    [InlineData("GreaterThan", FilterOperator.GreaterThan)]
    [InlineData("LessThan", FilterOperator.LessThan)]
    [InlineData("contains", FilterOperator.Contains)]  // operator is case-insensitive
    [InlineData("GreaterThanOrEqual", FilterOperator.GreaterThanOrEqual)]
    [InlineData("LessThanOrEqual", FilterOperator.LessThanOrEqual)]
    public void ParseAction_ValidFilterAction_ReturnsSuccess(string operatorStr, FilterOperator expectedOp)
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "filter" },
            { "columnName", "Age" },
            { "operator", operatorStr },
            { "value", "30" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Should().BeOfType<FilterAction>().Subject;
        action.ColumnName.Should().Be("Age");
        action.Operator.Should().Be(expectedOp);
        action.Value.Should().Be("30");
    }

    [Fact]
    public void ParseAction_FilterAction_WithInvalidOperator_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "filter" },
            { "columnName", "Age" },
            { "operator", "EXPLODE" },
            { "value", "30" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for operator");
    }

    [Fact]
    public void ParseAction_FilterAction_WithMissingValue_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "filter" },
            { "columnName", "Age" },
            { "operator", "Equals" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'value'");
    }

    [Fact]
    public void ParseAction_ValidFillAction_ReturnsSuccess()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "fill" },
            { "columnName", "Email" },
            { "value", "REDACTED" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Should().BeOfType<FillColumnAction>().Subject;
        action.ColumnName.Should().Be("Email");
        action.Value.Should().Be("REDACTED");
    }

    [Fact]
    public void ParseAction_FillAction_WithMissingColumnName_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "fill" },
            { "value", "REDACTED" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'columnName'");
    }

    [Fact]
    public void ParseAction_FillAction_WithMissingValue_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "fill" },
            { "columnName", "Email" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'value'");
    }

    [Fact]
    public void ParseAction_FillAction_WithEmptyValue_ReturnsSuccess()
    {
        // Arrange — empty string is a valid fill value (e.g., blank-out a column)
        var fields = new Dictionary<string, string>
        {
            { "type", "fill" },
            { "columnName", "Email" },
            { "value", "" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Should().BeOfType<FillColumnAction>().Subject;
        action.Value.Should().Be("");
    }

    [Fact]
    public void ParseAction_ValidFormatTimestampAction_ReturnsSuccess()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "format_timestamp" },
            { "columnName", "CreatedAt" },
            { "targetFormat", "yyyy/MM/dd" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Should().BeOfType<FormatTimestampAction>().Subject;
        action.ColumnName.Should().Be("CreatedAt");
        action.TargetFormat.Should().Be("yyyy/MM/dd");
    }

    [Fact]
    public void ParseAction_FormatTimestampAction_WithMissingColumnName_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "format_timestamp" },
            { "targetFormat", "yyyy/MM/dd" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'columnName'");
    }

    [Fact]
    public void ParseAction_FormatTimestampAction_WithMissingTargetFormat_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "format_timestamp" },
            { "columnName", "CreatedAt" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'targetFormat'");
    }

    [Fact]
    public void ParseAction_FormatTimestampAction_WithEmptyTargetFormat_ReturnsFailure()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "type", "format_timestamp" },
            { "columnName", "CreatedAt" },
            { "targetFormat", "" },
        };

        // Act
        var result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("targetFormat");
        result.Error.Should().Contain("empty");
    }
}
