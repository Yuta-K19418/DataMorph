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
}
