using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.Engine.Models.Actions;

public sealed class FillColumnActionTests
{
    // -------------------------------------------------------------------------
    // Description property
    // -------------------------------------------------------------------------

    [Fact]
    public void Description_ReturnsExpectedFormat()
    {
        // Arrange
        var action = new FillColumnAction { ColumnName = "Email", Value = "REDACTED" };

        // Act
        var description = action.Description;

        // Assert
        description.Should().Be("Fill column 'Email' with 'REDACTED'");
    }

    // -------------------------------------------------------------------------
    // JSON serialization — round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        // Arrange
        var action = new FillColumnAction { ColumnName = "Phone", Value = "***" };

        // Act
        var json = JsonSerializer.Serialize(action, JsonContext.Default.FillColumnAction);
        var deserialized = JsonSerializer.Deserialize(json, JsonContext.Default.FillColumnAction);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.ColumnName.Should().Be("Phone");
        deserialized.Value.Should().Be("***");
    }

    // -------------------------------------------------------------------------
    // JSON serialization — type discriminator
    // -------------------------------------------------------------------------

    [Fact]
    public void JsonSerialization_TypeDiscriminator_IsFill()
    {
        // Arrange
        var action = new FillColumnAction { ColumnName = "Name", Value = "ANON" };

        // Act
        var json = JsonSerializer.Serialize(
            (MorphAction)action,
            JsonContext.Default.MorphAction
        );

        // Assert
        json.Should().Contain("\"type\": \"fill\"");
    }
}
