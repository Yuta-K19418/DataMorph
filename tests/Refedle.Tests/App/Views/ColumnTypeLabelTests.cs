using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.App.Views;

public sealed class ColumnTypeLabelTests
{
    [Theory]
    [InlineData(ColumnType.Text, "text")]
    [InlineData(ColumnType.WholeNumber, "number")]
    [InlineData(ColumnType.FloatingPoint, "float")]
    [InlineData(ColumnType.Boolean, "bool")]
    [InlineData(ColumnType.Timestamp, "datetime")]
    [InlineData(ColumnType.JsonObject, "object")]
    [InlineData(ColumnType.JsonArray, "array")]
    public void ToLabel_VariousTypes_ReturnsCorrectLabel(ColumnType type, string expected)
    {
        // Arrange
        // Act
        var result = ColumnTypeLabel.ToLabel(type);
        // Assert
        result.Should().Be(expected);
    }
}
