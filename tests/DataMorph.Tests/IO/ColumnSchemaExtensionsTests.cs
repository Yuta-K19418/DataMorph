using AwesomeAssertions;
using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.IO;

public sealed class ColumnSchemaExtensionsTests
{
    [Theory]
    [InlineData(ColumnType.WholeNumber, ColumnType.WholeNumber, ColumnType.WholeNumber, false, "Same type")]
    [InlineData(ColumnType.Text, ColumnType.Text, ColumnType.Text, false, "Same type")]
    [InlineData(ColumnType.FloatingPoint, ColumnType.FloatingPoint, ColumnType.FloatingPoint, false, "Same type")]
    [InlineData(ColumnType.Boolean, ColumnType.Boolean, ColumnType.Boolean, false, "Same type")]
    [InlineData(ColumnType.Timestamp, ColumnType.Timestamp, ColumnType.Timestamp, false, "Same type")]
    [InlineData(ColumnType.WholeNumber, ColumnType.FloatingPoint, ColumnType.FloatingPoint, false, "WholeNumber -> FloatingPoint")]
    [InlineData(ColumnType.FloatingPoint, ColumnType.WholeNumber, ColumnType.FloatingPoint, false, "FloatingPoint -> WholeNumber (stays FloatingPoint)")]
    [InlineData(ColumnType.Boolean, ColumnType.WholeNumber, ColumnType.Text, false, "Boolean -> WholeNumber -> Text")]
    [InlineData(ColumnType.Boolean, ColumnType.FloatingPoint, ColumnType.Text, false, "Boolean -> FloatingPoint -> Text")]
    [InlineData(ColumnType.Timestamp, ColumnType.FloatingPoint, ColumnType.Text, false, "Timestamp -> FloatingPoint -> Text")]
    [InlineData(ColumnType.Timestamp, ColumnType.Text, ColumnType.Text, false, "Timestamp -> Text")]
    [InlineData(ColumnType.WholeNumber, ColumnType.Text, ColumnType.Text, false, "WholeNumber -> Text")]
    [InlineData(ColumnType.WholeNumber, ColumnType.Boolean, ColumnType.Text, false, "WholeNumber -> Boolean -> Text")]
    [InlineData(ColumnType.Text, ColumnType.WholeNumber, ColumnType.Text, false, "Text -> WholeNumber -> Text")]
    public void UpdateColumnType_VariousConversions_UpdatesCorrectly(
        ColumnType initialType,
        ColumnType newType,
        ColumnType expectedType,
        bool initialIsNullable,
        string description
    )
    {
        // Arrange
        var schema = new ColumnSchema
        {
            Name = "test",
            Type = initialType,
            IsNullable = initialIsNullable,
            ColumnIndex = 0,
        };

        // Act
        schema.UpdateColumnType(newType);

        // Assert
        schema.Type.Should().Be(expectedType, description);
        schema.IsNullable.Should().Be(initialIsNullable, "nullable state should not change");
    }

    [Theory]
    [InlineData(ColumnType.WholeNumber)]
    [InlineData(ColumnType.FloatingPoint)]
    [InlineData(ColumnType.Boolean)]
    [InlineData(ColumnType.Timestamp)]
    [InlineData(ColumnType.Text)]
    public void MarkNullable_SetsIsNullableToTrue(ColumnType columnType)
    {
        // Arrange
        var schema = new ColumnSchema
        {
            Name = "test",
            Type = columnType,
            IsNullable = false,
            ColumnIndex = 0,
        };

        // Act
        schema.MarkNullable();

        // Assert
        schema.IsNullable.Should().BeTrue();
        schema.Type.Should().Be(columnType);
    }

    [Theory]
    [InlineData(ColumnType.WholeNumber)]
    [InlineData(ColumnType.FloatingPoint)]
    [InlineData(ColumnType.Boolean)]
    [InlineData(ColumnType.Timestamp)]
    [InlineData(ColumnType.Text)]
    public void MarkNullable_Idempotent_CallingTwiceStaysTrue(ColumnType columnType)
    {
        // Arrange
        var schema = new ColumnSchema
        {
            Name = "test",
            Type = columnType,
            IsNullable = false,
            ColumnIndex = 0,
        };

        // Act
        schema.MarkNullable();
        var firstCall = schema.IsNullable;
        schema.MarkNullable();
        var secondCall = schema.IsNullable;

        // Assert
        firstCall.Should().BeTrue();
        secondCall.Should().BeTrue();
    }
}
