using System.Text.Json;
using AwesomeAssertions;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;

namespace DataMorph.Tests.Engine.Models.Actions;

public sealed class FormatTimestampActionTests
{
    [Fact]
    public void Description_Property_Returns_Correct_String()
    {
        // Arrange
        var action = new FormatTimestampAction { ColumnName = "CreatedAt", TargetFormat = "yyyy-MM-dd" };

        // Act
        var description = action.Description;

        // Assert
        description.Should().Be("Format timestamp column 'CreatedAt' → \"yyyy-MM-dd\"");
    }

    [Fact]
    public void JSON_Round_Trip_Preserves_Properties_And_Type_Discriminator()
    {
        // Arrange
        var action = new FormatTimestampAction { ColumnName = "CreatedAt", TargetFormat = "yyyy/MM/dd" };

        // Act
        var json = JsonSerializer.Serialize(
            (MorphAction)action,
            DataMorphJsonContext.Default.MorphAction
        );
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.MorphAction);

        // Assert
        json.Should().Contain("\"type\": \"format_timestamp\"");
        var typed = deserialized.Should().BeOfType<FormatTimestampAction>().Subject;
        typed.ColumnName.Should().Be("CreatedAt");
        typed.TargetFormat.Should().Be("yyyy/MM/dd");
    }
}
