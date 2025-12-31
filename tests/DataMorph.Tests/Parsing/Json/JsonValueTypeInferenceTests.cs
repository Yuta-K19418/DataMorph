using System.Text;
using System.Text.Json;
using DataMorph.Engine.Parsing.Json;
using DataMorph.Engine.Types;
using FluentAssertions;

namespace DataMorph.Tests.Parsing.Json;

public sealed class JsonValueTypeInferenceTests
{
    [Fact]
    public void InferType_Null_ReturnsNull()
    {
        // Arrange
        var tokenType = JsonTokenType.Null;

        // Act
        var result = JsonValueTypeInference.InferType(tokenType);

        // Assert
        result.Should().Be(ColumnType.Null);
    }

    [Fact]
    public void InferType_True_ReturnsBoolean()
    {
        // Arrange
        var tokenType = JsonTokenType.True;

        // Act
        var result = JsonValueTypeInference.InferType(tokenType);

        // Assert
        result.Should().Be(ColumnType.Boolean);
    }

    [Fact]
    public void InferType_False_ReturnsBoolean()
    {
        // Arrange
        var tokenType = JsonTokenType.False;

        // Act
        var result = JsonValueTypeInference.InferType(tokenType);

        // Assert
        result.Should().Be(ColumnType.Boolean);
    }

    [Fact]
    public void InferType_WholeNumber_ReturnsWholeNumber()
    {
        // Arrange
        var tokenType = JsonTokenType.Number;
        var valueBytes = Encoding.UTF8.GetBytes("42");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void InferType_NegativeWholeNumber_ReturnsWholeNumber()
    {
        // Arrange
        var tokenType = JsonTokenType.Number;
        var valueBytes = Encoding.UTF8.GetBytes("-123");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void InferType_FloatingPointWithDecimal_ReturnsFloatingPoint()
    {
        // Arrange
        var tokenType = JsonTokenType.Number;
        var valueBytes = Encoding.UTF8.GetBytes("42.5");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.FloatingPoint);
    }

    [Fact]
    public void InferType_FloatingPointWithExponent_ReturnsFloatingPoint()
    {
        // Arrange
        var tokenType = JsonTokenType.Number;
        var valueBytes = Encoding.UTF8.GetBytes("1.5e10");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.FloatingPoint);
    }

    [Fact]
    public void InferType_FloatingPointWithUpperCaseExponent_ReturnsFloatingPoint()
    {
        // Arrange
        var tokenType = JsonTokenType.Number;
        var valueBytes = Encoding.UTF8.GetBytes("2E-5");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.FloatingPoint);
    }

    [Fact]
    public void InferType_StringWithText_ReturnsText()
    {
        // Arrange
        var tokenType = JsonTokenType.String;
        var valueBytes = Encoding.UTF8.GetBytes("hello world");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.Text);
    }

    [Fact]
    public void InferType_StringWithIso8601DateTime_ReturnsTimestamp()
    {
        // Arrange
        var tokenType = JsonTokenType.String;
        var valueBytes = Encoding.UTF8.GetBytes("2024-01-15T10:30:00Z");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.Timestamp);
    }

    [Fact]
    public void InferType_StringWithIso8601DateTimeOffset_ReturnsTimestamp()
    {
        // Arrange
        var tokenType = JsonTokenType.String;
        var valueBytes = Encoding.UTF8.GetBytes("2024-01-15T10:30:00+09:00");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.Timestamp);
    }

    [Fact]
    public void InferType_StringWithDate_ReturnsTimestamp()
    {
        // Arrange
        var tokenType = JsonTokenType.String;
        var valueBytes = Encoding.UTF8.GetBytes("2024-01-15");

        // Act
        var result = JsonValueTypeInference.InferType(tokenType, valueBytes);

        // Assert
        result.Should().Be(ColumnType.Timestamp);
    }

    [Fact]
    public void CombineTypes_SameTypes_ReturnsSameType()
    {
        // Arrange & Act
        var result = JsonValueTypeInference.CombineTypes(ColumnType.WholeNumber, ColumnType.WholeNumber);

        // Assert
        result.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void CombineTypes_NullWithOtherType_ReturnsOtherType()
    {
        // Arrange & Act
        var result1 = JsonValueTypeInference.CombineTypes(ColumnType.Null, ColumnType.Text);
        var result2 = JsonValueTypeInference.CombineTypes(ColumnType.WholeNumber, ColumnType.Null);

        // Assert
        result1.Should().Be(ColumnType.Text);
        result2.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void CombineTypes_WholeNumberAndFloatingPoint_ReturnsFloatingPoint()
    {
        // Arrange & Act
        var result1 = JsonValueTypeInference.CombineTypes(ColumnType.WholeNumber, ColumnType.FloatingPoint);
        var result2 = JsonValueTypeInference.CombineTypes(ColumnType.FloatingPoint, ColumnType.WholeNumber);

        // Assert
        result1.Should().Be(ColumnType.FloatingPoint);
        result2.Should().Be(ColumnType.FloatingPoint);
    }

    [Fact]
    public void CombineTypes_IncompatibleTypes_ReturnsText()
    {
        // Arrange & Act
        var result = JsonValueTypeInference.CombineTypes(ColumnType.Boolean, ColumnType.WholeNumber);

        // Assert
        result.Should().Be(ColumnType.Text);
    }

    [Fact]
    public void CombineTypes_TextWithAnyOtherType_ReturnsText()
    {
        // Arrange & Act
        var result1 = JsonValueTypeInference.CombineTypes(ColumnType.Text, ColumnType.WholeNumber);
        var result2 = JsonValueTypeInference.CombineTypes(ColumnType.Timestamp, ColumnType.Text);

        // Assert
        result1.Should().Be(ColumnType.Text);
        result2.Should().Be(ColumnType.Text);
    }
}
