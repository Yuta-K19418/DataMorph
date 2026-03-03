using System.Globalization;
using DataMorph.Engine.Filtering;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;
using nietras.SeparatedValues;

namespace DataMorph.App.Cli;

/// <summary>
/// Evaluates filter specifications against CSV rows and JSON Lines records.
/// </summary>
internal static class FilterEvaluator
{
    /// <summary>
    /// Returns <c>true</c> if the CSV row passes all filter specs.
    /// </summary>
    internal static bool EvaluateCsvFilters(
        in SepReader.Row record,
        IReadOnlyList<FilterSpec> filters
    )
    {
        foreach (var filter in filters)
        {
            if (filter.SourceColumnIndex >= record.ColCount)
            {
                return false;
            }

            var valueSpan = record[filter.SourceColumnIndex].Span;
            if (!EvaluateFilter(valueSpan, filter))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if the JSON Lines record passes all filter specs.
    /// </summary>
    /// <param name="lineBytes">Raw UTF-8 bytes of the JSON line.</param>
    /// <param name="filters">Filter specs to evaluate.</param>
    /// <param name="indexToNameBytes">Pre-built map of source column index to UTF-8-encoded column name bytes.</param>
    internal static bool EvaluateJsonFilters(
        ReadOnlyMemory<byte> lineBytes,
        IReadOnlyList<FilterSpec> filters,
        IReadOnlyDictionary<int, ReadOnlyMemory<byte>> indexToNameBytes
    )
    {
        foreach (var filter in filters)
        {
            if (!indexToNameBytes.TryGetValue(filter.SourceColumnIndex, out var sourceColNameBytes))
            {
                continue;
            }

            var value = CellExtractor.ExtractCell(lineBytes.Span, sourceColNameBytes.Span);

            if (value == "<null>" || value == "<error>")
            {
                return false;
            }

            if (!EvaluateFilter(value.AsSpan(), filter))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="value"/> satisfies the filter spec.
    /// </summary>
    internal static bool EvaluateFilter(ReadOnlySpan<char> value, FilterSpec spec)
    {
        var op = spec.Operator;

        if (op == FilterOperator.Contains)
        {
            return value.Contains(spec.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        if (op == FilterOperator.NotContains)
        {
            return !value.Contains(spec.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        if (op == FilterOperator.StartsWith)
        {
            return value.StartsWith(spec.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        if (op == FilterOperator.EndsWith)
        {
            return value.EndsWith(spec.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        if (op == FilterOperator.Equals)
        {
            return value.Equals(spec.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        if (op == FilterOperator.NotEquals)
        {
            return !value.Equals(spec.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        // Numeric/Timestamp comparison operators
        return spec.ColumnType switch
        {
            ColumnType.WholeNumber => EvaluateNumericLong(value, spec.Value.AsSpan(), op),
            ColumnType.FloatingPoint => EvaluateNumericDouble(value, spec.Value.AsSpan(), op),
            ColumnType.Timestamp => EvaluateTimestamp(value, spec.Value.AsSpan(), op),
            // Text or other types: numeric/timestamp operators not supported; exclude row
            _ => false,
        };
    }

    /// <summary>Returns <c>true</c> if whitespace-only bytes.</summary>
    internal static bool IsWhiteSpace(ReadOnlySpan<byte> span)
    {
        foreach (var b in span)
        {
            if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
            {
                return false;
            }
        }

        return true;
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
            !double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv)
            || !double.TryParse(specValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var ds)
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
