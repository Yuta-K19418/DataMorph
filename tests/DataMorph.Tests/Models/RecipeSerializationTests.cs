using System.Text.Json;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;
using FluentAssertions;

namespace DataMorph.Tests.Models;

public sealed class RecipeSerializationTests
{
    [Fact]
    public void RenameColumnAction_SerializesAndDeserializes_Correctly()
    {
        // Arrange
        var action = new RenameColumnAction
        {
            OldName = "old_column",
            NewName = "new_column"
        };

        // Act
        var json = JsonSerializer.Serialize(action, DataMorphJsonContext.Default.RenameColumnAction);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.RenameColumnAction);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(action);
        deserialized.Description.Should().Be("Rename column 'old_column' to 'new_column'");
    }

    [Fact]
    public void DeleteColumnAction_SerializesAndDeserializes_Correctly()
    {
        // Arrange
        var action = new DeleteColumnAction
        {
            ColumnName = "unwanted_column"
        };

        // Act
        var json = JsonSerializer.Serialize(action, DataMorphJsonContext.Default.DeleteColumnAction);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.DeleteColumnAction);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(action);
        deserialized.Description.Should().Be("Delete column 'unwanted_column'");
    }

    [Fact]
    public void CastColumnAction_SerializesAndDeserializes_Correctly()
    {
        // Arrange
        var action = new CastColumnAction
        {
            ColumnName = "price",
            TargetType = ColumnType.FloatingPoint
        };

        // Act
        var json = JsonSerializer.Serialize(action, DataMorphJsonContext.Default.CastColumnAction);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.CastColumnAction);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(action);
        deserialized.Description.Should().Be("Cast column 'price' to FloatingPoint");
    }

    [Fact]
    public void Recipe_WithMultipleActions_SerializesAndDeserializes_Correctly()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "Clean User Data",
            Description = "Standardize user data format",
            Actions = new List<MorphAction>
            {
                new RenameColumnAction { OldName = "user_name", NewName = "username" },
                new DeleteColumnAction { ColumnName = "temp_field" },
                new CastColumnAction { ColumnName = "age", TargetType = ColumnType.WholeNumber }
            },
            LastModified = new DateTimeOffset(2025, 12, 30, 12, 0, 0, TimeSpan.Zero)
        };

        // Act
        var json = JsonSerializer.Serialize(recipe, DataMorphJsonContext.Default.Recipe);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.Recipe);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Name.Should().Be("Clean User Data");
        deserialized.Description.Should().Be("Standardize user data format");
        deserialized.Actions.Should().HaveCount(3);
        deserialized.IsEmpty.Should().BeFalse();
        deserialized.LastModified.Should().Be(new DateTimeOffset(2025, 12, 30, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Recipe_EmptyActions_HasIsEmptyTrue()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "Empty Recipe",
            Actions = []
        };

        // Assert
        recipe.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void MorphAction_PolymorphicSerialization_PreservesType()
    {
        // Arrange
        var actions = new List<MorphAction>
        {
            new RenameColumnAction { OldName = "a", NewName = "b" },
            new DeleteColumnAction { ColumnName = "c" },
            new CastColumnAction { ColumnName = "d", TargetType = ColumnType.Text }
        };

        // Act
        var json = JsonSerializer.Serialize(actions, DataMorphJsonContext.Default.ListMorphAction);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.ListMorphAction);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().HaveCount(3);
        deserialized[0].Should().BeOfType<RenameColumnAction>();
        deserialized[1].Should().BeOfType<DeleteColumnAction>();
        deserialized[2].Should().BeOfType<CastColumnAction>();
    }

    [Fact]
    public void Recipe_SerializedJson_UsesCamelCaseNaming()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "Test",
            Actions = new List<MorphAction>
            {
                new RenameColumnAction { OldName = "old", NewName = "new" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(recipe, DataMorphJsonContext.Default.Recipe);

        // Assert
        json.Should().Contain("\"name\":");
        json.Should().Contain("\"actions\":");
        json.Should().NotContain("\"Name\":");
        json.Should().NotContain("\"Actions\":");
    }

    [Fact]
    public void Recipe_WithNullDescription_OmitsPropertyInJson()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "Test",
            Description = null,
            Actions = Array.Empty<MorphAction>()
        };

        // Act
        var json = JsonSerializer.Serialize(recipe, DataMorphJsonContext.Default.Recipe);

        // Assert
        json.Should().NotContain("description");
    }

    [Fact]
    public void Recipe_DeserializeWithoutDescription_DefaultsToNull()
    {
        // Arrange
        var json = """
        {
            "name": "Test Recipe",
            "actions": []
        }
        """;

        // Act
        var recipe = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.Recipe);

        // Assert
        recipe.Should().NotBeNull();
        recipe.Description.Should().BeNull();
        recipe.Name.Should().Be("Test Recipe");
        recipe.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Recipe_DeserializeInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJson = """{ "name": "Test", "actions": "not-an-array" }""";

        // Act
        var act = () => JsonSerializer.Deserialize(invalidJson, DataMorphJsonContext.Default.Recipe);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Recipe_DeserializeMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var malformedJson = """{ "name": "Test", "actions": [ }""";

        // Act
        var act = () => JsonSerializer.Deserialize(malformedJson, DataMorphJsonContext.Default.Recipe);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Recipe_DeserializeWithMissingRequiredField_ThrowsJsonException()
    {
        // Arrange
        var jsonWithoutName = """{ "actions": [] }""";

        // Act
        var act = () => JsonSerializer.Deserialize(jsonWithoutName, DataMorphJsonContext.Default.Recipe);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void MorphAction_DeserializeUnknownActionType_ThrowsJsonException()
    {
        // Arrange
        var jsonWithUnknownType = """
        {
            "name": "Test Recipe",
            "actions": [
                {
                    "type": "unknown_action",
                    "columnName": "test"
                }
            ]
        }
        """;

        // Act
        var act = () => JsonSerializer.Deserialize(jsonWithUnknownType, DataMorphJsonContext.Default.Recipe);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void CastColumnAction_DeserializeWithMissingRequiredProperty_ThrowsJsonException()
    {
        // Arrange
        var jsonWithMissingProperty = """
        {
            "name": "Test Recipe",
            "actions": [
                {
                    "type": "cast",
                    "columnName": "test"
                }
            ]
        }
        """;

        // Act
        var act = () => JsonSerializer.Deserialize(jsonWithMissingProperty, DataMorphJsonContext.Default.Recipe);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Recipe_LastModified_PreservesTimezoneInformation()
    {
        // Arrange - Use JST (UTC+9)
        var jstOffset = TimeSpan.FromHours(9);
        var expectedTimestamp = new DateTimeOffset(2025, 12, 30, 21, 0, 0, jstOffset);
        var recipe = new Recipe
        {
            Name = "Timezone Test",
            Actions = Array.Empty<MorphAction>(),
            LastModified = expectedTimestamp
        };

        // Act
        var json = JsonSerializer.Serialize(recipe, DataMorphJsonContext.Default.Recipe);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.Recipe);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.LastModified.Should().HaveValue();
        deserialized.LastModified.Should().Be(expectedTimestamp);
    }

    [Fact]
    public void Recipe_LastModified_UtcAndLocalTimeAreEquivalent()
    {
        // Arrange - Same moment in time, different timezones
        var utcTime = new DateTimeOffset(2025, 12, 30, 12, 0, 0, TimeSpan.Zero);
        var jstTime = new DateTimeOffset(2025, 12, 30, 21, 0, 0, TimeSpan.FromHours(9));

        var recipe1 = new Recipe { Name = "UTC", Actions = Array.Empty<MorphAction>(), LastModified = utcTime };
        var recipe2 = new Recipe { Name = "JST", Actions = Array.Empty<MorphAction>(), LastModified = jstTime };

        // Assert - Should represent the same moment in time
        recipe1.LastModified.Should().Be(recipe2.LastModified);
        recipe1.LastModified.Should().HaveValue();
        recipe2.LastModified.Should().HaveValue();
        recipe1.LastModified.Value.UtcDateTime.Should().Be(recipe2.LastModified.Value.UtcDateTime);
    }

    [Fact]
    public void Recipe_WithNullName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Recipe
        {
            Name = null!,
            Actions = Array.Empty<MorphAction>()
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Recipe_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Recipe
        {
            Name = string.Empty,
            Actions = Array.Empty<MorphAction>()
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Recipe_WithWhiteSpaceName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Recipe
        {
            Name = "   ",
            Actions = Array.Empty<MorphAction>()
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
