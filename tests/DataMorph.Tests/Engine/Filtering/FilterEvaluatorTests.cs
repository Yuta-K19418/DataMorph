using AwesomeAssertions;
using DataMorph.Engine.Filtering;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Engine.Filtering;

public sealed class FilterEvaluatorTests
{
    // -------------------------------------------------------------------------
    // Text operators — Equals / NotEquals
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_Equals_MatchingValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Equals,
            Value: "Alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("Alice".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_Equals_NonMatchingValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Equals,
            Value: "Alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("Bob".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_Equals_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Equals,
            Value: "alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("ALICE".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_NotEquals_MatchingValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.NotEquals,
            Value: "Alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("Alice".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_NotEquals_NonMatchingValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.NotEquals,
            Value: "Alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("Bob".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Text operators — Contains / NotContains
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_Contains_ValuePresent_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Contains,
            Value: "app"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_Contains_ValueAbsent_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Contains,
            Value: "xyz"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_Contains_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Contains,
            Value: "app"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("APPLE".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_NotContains_ValuePresent_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.NotContains,
            Value: "app"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_NotContains_ValueAbsent_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.NotContains,
            Value: "xyz"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Text operators — StartsWith / EndsWith
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_StartsWith_MatchingPrefix_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.StartsWith,
            Value: "app"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_StartsWith_NonMatchingPrefix_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.StartsWith,
            Value: "xyz"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_EndsWith_MatchingSuffix_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.EndsWith,
            Value: "ple"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_EndsWith_NonMatchingSuffix_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.EndsWith,
            Value: "xyz"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Numeric operators — WholeNumber
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_LargerValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("50".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_SmallerValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("10".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_LessThan_WholeNumber_SmallerValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.LessThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("10".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_GreaterThanOrEqual_WholeNumber_EqualValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThanOrEqual,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("20".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_LessThanOrEqual_WholeNumber_EqualValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.LessThanOrEqual,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("20".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_InvalidRawValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("abc".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Numeric operators — FloatingPoint
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_GreaterThan_FloatingPoint_LargerValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.GreaterThan,
            Value: "2.0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("3.14".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_LessThan_FloatingPoint_SmallerValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.LessThan,
            Value: "2.0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("1.5".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Timestamp operators
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_GreaterThan_Timestamp_LaterDate_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.GreaterThan,
            Value: "2024-01-01"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("2024-06-01".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_LessThan_Timestamp_EarlierDate_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.LessThan,
            Value: "2024-06-01"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("2024-01-01".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_Timestamp_InvalidRawValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.GreaterThan,
            Value: "2024-01-01"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("not-a-date".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Text column with numeric operators — fallback to false
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_GreaterThan_TextColumn_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.GreaterThan,
            Value: "hello"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("world".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_LessThan_TextColumn_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.LessThan,
            Value: "hello"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("world".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }
}
