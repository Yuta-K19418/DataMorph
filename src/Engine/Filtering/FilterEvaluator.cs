using System.Globalization;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Engine.Filtering;

/// <summary>
/// Provides stateless, allocation-free filter evaluation for a single cell value
/// against a resolved <see cref="FilterSpec"/>.
/// All methods accept <see cref="ReadOnlySpan{T}"/> to avoid heap allocations on the
/// hot path (invoked once per cell per row during index construction).
/// </summary>
public static class FilterEvaluator
{
    /// <summary>
    /// Evaluates a single filter condition against a raw cell value represented as a
    /// <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
    /// Numeric and timestamp operators parse <paramref name="rawValue"/> and
    /// <see cref="FilterSpec.Value"/>; on parse failure the row is excluded.
    /// Applying a numeric operator to a <see cref="ColumnType.Text"/> column
    /// falls back to returning <see langword="false"/>.
    /// </summary>
    public static bool EvaluateFilter(ReadOnlySpan<char> rawValue, FilterSpec spec)
    {
        var op = spec.Operator;
        var specValue = spec.Value.AsSpan();

        if (IsStringOperator(op))
        {
            return EvaluateStringOperator(rawValue, specValue, op);
        }

        // Numeric/Timestamp comparison operators
        return spec.ColumnType switch
        {
            ColumnType.WholeNumber => EvaluateNumericLong(rawValue, specValue, op),
            ColumnType.FloatingPoint => EvaluateNumericDouble(rawValue, specValue, op),
            ColumnType.Timestamp => EvaluateTimestamp(rawValue, specValue, op),
            // Text or other types: numeric/timestamp operators are not supported; exclude the row
            _ => false,
        };
    }

    private static bool IsStringOperator(FilterOperator op) =>
        op is FilterOperator.Contains or FilterOperator.NotContains
            or FilterOperator.StartsWith or FilterOperator.EndsWith
            or FilterOperator.Equals or FilterOperator.NotEquals;

    private static bool EvaluateStringOperator(
        ReadOnlySpan<char> rawValue,
        ReadOnlySpan<char> specValue,
        FilterOperator op
    )
    {
        var ignoreCase = StringComparison.OrdinalIgnoreCase;
        return op switch
        {
            FilterOperator.Contains => rawValue.Contains(specValue, ignoreCase),
            FilterOperator.NotContains => !rawValue.Contains(specValue, ignoreCase),
            FilterOperator.StartsWith => rawValue.StartsWith(specValue, ignoreCase),
            FilterOperator.EndsWith => rawValue.EndsWith(specValue, ignoreCase),
            FilterOperator.Equals => rawValue.Equals(specValue, ignoreCase),
            FilterOperator.NotEquals => !rawValue.Equals(specValue, ignoreCase),
            // Non-string operators are handled by the numeric/timestamp path; defensive fallback.
            _ => false,
        };
    }

    private static bool EvaluateNumericLong(
        ReadOnlySpan<char> rawValue,
        ReadOnlySpan<char> specValue,
        FilterOperator op
    )
    {
        if (
            !long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv)
            || !long.TryParse(specValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ls)
        )
        {
            return false;
        }

        return op switch
        {
            FilterOperator.GreaterThan => lv > ls,
            FilterOperator.LessThan => lv < ls,
            FilterOperator.GreaterThanOrEqual => lv >= ls,
            FilterOperator.LessThanOrEqual => lv <= ls,
            _ => false,
        };
    }

    private static bool EvaluateNumericDouble(
        ReadOnlySpan<char> rawValue,
        ReadOnlySpan<char> specValue,
        FilterOperator op
    )
    {
        if (
            !double.TryParse(
                rawValue,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var dv
            )
            || !double.TryParse(
                specValue,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var ds
            )
        )
        {
            return false;
        }

        return op switch
        {
            FilterOperator.GreaterThan => dv > ds,
            FilterOperator.LessThan => dv < ds,
            FilterOperator.GreaterThanOrEqual => dv >= ds,
            FilterOperator.LessThanOrEqual => dv <= ds,
            _ => false,
        };
    }

    private static bool EvaluateTimestamp(
        ReadOnlySpan<char> rawValue,
        ReadOnlySpan<char> specValue,
        FilterOperator op
    )
    {
        if (
            !DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tv)
            || !DateTime.TryParse(specValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts)
        )
        {
            return false;
        }

        return op switch
        {
            FilterOperator.GreaterThan => tv > ts,
            FilterOperator.LessThan => tv < ts,
            FilterOperator.GreaterThanOrEqual => tv >= ts,
            FilterOperator.LessThanOrEqual => tv <= ts,
            _ => false,
        };
    }
}
