using AwesomeAssertions;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Recipes;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Engine.Recipes;

public sealed class RecipeYamlSerializerTests
{
    [Fact]
    public void Serialize_EmptyActions_ProducesActionsEmptyListLine()
    {
        // Arrange
        var recipe = new Recipe { Name = "test", Actions = [] };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("actions: []");
    }

    [Fact]
    public void Serialize_WithRenameAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "old", NewName = "new" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: rename");
        yaml.Should().Contain("    oldName: \"old\"");
        yaml.Should().Contain("    newName: \"new\"");
    }

    [Fact]
    public void Serialize_WithDeleteAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new DeleteColumnAction { ColumnName = "temp_field" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: delete");
        yaml.Should().Contain("    columnName: \"temp_field\"");
    }

    [Fact]
    public void Serialize_WithCastAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new CastColumnAction { ColumnName = "age", TargetType = ColumnType.WholeNumber }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: cast");
        yaml.Should().Contain("    columnName: \"age\"");
        yaml.Should().Contain("    targetType: WholeNumber");
    }

    [Fact]
    public void Serialize_WithFilterAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new FilterAction { ColumnName = "status", Operator = FilterOperator.Equals, Value = "active" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: filter");
        yaml.Should().Contain("    columnName: \"status\"");
        yaml.Should().Contain("    operator: Equals");
        yaml.Should().Contain("    value: \"active\"");
    }

    [Fact]
    public void Serialize_NullDescription_OmitsDescriptionField()
    {
        // Arrange
        var recipe = new Recipe { Name = "test", Actions = [], Description = null };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().NotContain("description:");
    }

    [Fact]
    public void Serialize_NullLastModified_OmitsLastModifiedField()
    {
        // Arrange
        var recipe = new Recipe { Name = "test", Actions = [], LastModified = null };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().NotContain("lastModified:");
    }

    [Fact]
    public void Serialize_StringValueWithDoubleQuote_EscapesQuoteCharacter()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col\"name", NewName = "new" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("    oldName: \"col\\\"name\"");
    }

    [Fact]
    public void Serialize_StringValueWithBackslash_EscapesBackslash()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = @"C:\data", NewName = "output" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain(@"    oldName: ""C:\\data""");
    }

    [Fact]
    public void Serialize_StringValueWithBackslashAndQuote_EscapesBoth()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col\\\"name", NewName = "output" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("    oldName: \"col\\\\\\\"name\"");
    }

    [Fact]
    public void Serialize_FieldOrder_NameFirstActionsLast()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Description = "desc",
            LastModified = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Actions = [],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        var nameIdx = yaml.IndexOf("name:", StringComparison.Ordinal);
        var descIdx = yaml.IndexOf("description:", StringComparison.Ordinal);
        var lastModIdx = yaml.IndexOf("lastModified:", StringComparison.Ordinal);
        var actionsIdx = yaml.IndexOf("actions:", StringComparison.Ordinal);
        nameIdx.Should().BeGreaterThanOrEqualTo(0, "name field should be present");
        descIdx.Should().BeGreaterThanOrEqualTo(0, "description field should be present");
        lastModIdx.Should().BeGreaterThanOrEqualTo(0, "lastModified field should be present");
        actionsIdx.Should().BeGreaterThanOrEqualTo(0, "actions field should be present");
        nameIdx.Should().BeLessThan(descIdx);
        descIdx.Should().BeLessThan(lastModIdx);
        lastModIdx.Should().BeLessThan(actionsIdx);
    }
}
