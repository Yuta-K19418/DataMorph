using AwesomeAssertions;
using DataMorph.Engine.Filtering;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.App.Cli;

public sealed class FilterEvaluatorTests
{
    // -------------------------------------------------------------------------
    // EvaluateFilter — Text operators
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

    // -------------------------------------------------------------------------
    // EvaluateFilter — Numeric operators (WholeNumber)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("19", false)]   // below threshold
    [InlineData("20", false)]   // at threshold
    [InlineData("21", true)]    // above threshold
    public void EvaluateFilter_GreaterThan_WholeNumber_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("19", true)]    // below threshold
    [InlineData("20", true)]    // at threshold
    [InlineData("21", false)]   // above threshold
    public void EvaluateFilter_LessThanOrEqual_WholeNumber_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.LessThanOrEqual,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
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

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_WhitespaceValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(" 15 ".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_NegativeValues_WorksCorrectly()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "-10"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("-5".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_ZeroBoundary_WorksCorrectly()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("0".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // EvaluateFilter — Numeric operators (FloatingPoint)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1.9", false)]   // below threshold
    [InlineData("2.0", false)]   // at threshold
    [InlineData("2.1", true)]    // above threshold
    public void EvaluateFilter_GreaterThan_FloatingPoint_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.GreaterThan,
            Value: "2.0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_FloatingPoint_NegativeValues_WorksCorrectly()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.GreaterThan,
            Value: "-10.5"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("-5.3".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_FloatingPoint_ScientificNotation_WorksCorrectly()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.GreaterThan,
            Value: "1.5"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("2.5e2".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // EvaluateFilter — Timestamp operators
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("2024-06-14", false)]   // day before threshold
    [InlineData("2024-06-15", false)]   // at threshold
    [InlineData("2024-06-16", true)]    // day after threshold
    public void EvaluateFilter_GreaterThan_Timestamp_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.GreaterThan,
            Value: "2024-06-15"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
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
    // EvaluateFilter — Text column with numeric operators (fallback to false)
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

    // -------------------------------------------------------------------------
    // EvaluateFilter — Multiple filters in sequence
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_WithMultipleFilters_AllPass_ReturnsTrueForAll()
    {
        // Arrange
        var spec1 = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Contains,
            Value: "apple"
        );
        var spec2 = new FilterSpec(
            SourceColumnIndex: 1,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "10"
        );

        // Act
        var result1 = FilterEvaluator.EvaluateFilter("apple pie".AsSpan(), spec1);
        var result2 = FilterEvaluator.EvaluateFilter("15".AsSpan(), spec2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_WithMultipleFilters_OneFails_ReturnsFalse()
    {
        // Arrange
        var spec1 = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Contains,
            Value: "apple"
        );
        var spec2 = new FilterSpec(
            SourceColumnIndex: 1,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "10"
        );

        // Act
        var result1 = FilterEvaluator.EvaluateFilter("apple pie".AsSpan(), spec1);
        var result2 = FilterEvaluator.EvaluateFilter("5".AsSpan(), spec2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();
    }

}
