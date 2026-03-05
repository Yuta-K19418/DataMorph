using DataMorph.Engine.Filtering;
using DataMorph.Engine.IO.JsonLines;
using nietras.SeparatedValues;
using EngineFilterEvaluator = DataMorph.Engine.Filtering.FilterEvaluator;

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
        return EngineFilterEvaluator.EvaluateFilter(value, spec);
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
}
