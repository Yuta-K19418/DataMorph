using System.Text.Json;
using AwesomeAssertions;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;

namespace DataMorph.Tests.Engine.Models.Actions;

public sealed class FilterActionTests
{
    // -------------------------------------------------------------------------
    // Description property
    // -------------------------------------------------------------------------

    [Fact]
    public void Description_ReturnsExpectedFormat()
    {
        // Arrange
        var action = new FilterAction
        {
            ColumnName = "Price",
            Operator = FilterOperator.GreaterThan,
            Value = "100",
        };

        // Act
        var description = action.Description;

        // Assert
        description.Should().Be("Filter 'Price' GreaterThan '100'");
    }

    // -------------------------------------------------------------------------
    // JSON serialization — round-trip
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(FilterOperator.Equals)]
    [InlineData(FilterOperator.NotEquals)]
    [InlineData(FilterOperator.GreaterThan)]
    [InlineData(FilterOperator.LessThan)]
    [InlineData(FilterOperator.GreaterThanOrEqual)]
    [InlineData(FilterOperator.LessThanOrEqual)]
    [InlineData(FilterOperator.Contains)]
    [InlineData(FilterOperator.NotContains)]
    [InlineData(FilterOperator.StartsWith)]
    [InlineData(FilterOperator.EndsWith)]
    public void JsonRoundTrip_AllOperators_PreservesAllProperties(FilterOperator op)
    {
        // Arrange
        var action = new FilterAction
        {
            ColumnName = "SomeColumn",
            Operator = op,
            Value = "someValue",
        };

        // Act
        var json = JsonSerializer.Serialize(action, DataMorphJsonContext.Default.FilterAction);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.FilterAction);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.ColumnName.Should().Be("SomeColumn");
        deserialized.Operator.Should().Be(op);
        deserialized.Value.Should().Be("someValue");
    }

    [Fact]
    public void JsonSerialization_TypeDiscriminator_IsFilter()
    {
        // Arrange
        var action = new FilterAction
        {
            ColumnName = "Status",
            Operator = FilterOperator.Equals,
            Value = "active",
        };

        // Act
        var json = JsonSerializer.Serialize(
            (MorphAction)action,
            DataMorphJsonContext.Default.MorphAction
        );

        // Assert
        json.Should().Contain("\"type\": \"filter\"");
    }

    // -------------------------------------------------------------------------
    // JSON serialization — FilterOperator string values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(FilterOperator.Equals, "Equals")]
    [InlineData(FilterOperator.NotEquals, "NotEquals")]
    [InlineData(FilterOperator.GreaterThan, "GreaterThan")]
    [InlineData(FilterOperator.LessThan, "LessThan")]
    [InlineData(FilterOperator.GreaterThanOrEqual, "GreaterThanOrEqual")]
    [InlineData(FilterOperator.LessThanOrEqual, "LessThanOrEqual")]
    [InlineData(FilterOperator.Contains, "Contains")]
    [InlineData(FilterOperator.NotContains, "NotContains")]
    [InlineData(FilterOperator.StartsWith, "StartsWith")]
    [InlineData(FilterOperator.EndsWith, "EndsWith")]
    public void JsonSerialization_FilterOperator_SerializesAsStringName(FilterOperator op, string expectedJsonValue)
    {
        // Arrange
        var action = new FilterAction
        {
            ColumnName = "Col",
            Operator = op,
            Value = "val",
        };

        // Act
        var json = JsonSerializer.Serialize(action, DataMorphJsonContext.Default.FilterAction);

        // Assert
        json.Should().Contain($"\"{expectedJsonValue}\"");
    }
}
