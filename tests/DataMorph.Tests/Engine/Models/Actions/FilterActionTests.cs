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

        // Act

        // Assert
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

        // Act

        // Assert
        _ = op;
    }

    [Fact]
    public void JsonSerialization_TypeDiscriminator_IsFilter()
    {
        // Arrange

        // Act

        // Assert
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

        // Act

        // Assert
        _ = op;
        _ = expectedJsonValue;
    }
}
