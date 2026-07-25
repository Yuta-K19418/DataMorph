using System.Buffers;
using System.Text.Json;
using Refedle.Engine.IO.Json;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Stateless helpers that traverse a KeyPath through a single record's bytes and collect the
/// leaf row(s) reached, accumulating schema information along the way. Shared implementation
/// detail of <see cref="FullAggregationScanner"/>, split out to keep both classes under the
/// project's per-class line limit.
/// </summary>
internal static class KeyPathTraverser
{
    /// <summary>
    /// Traverses <paramref name="keyPath"/> starting from <paramref name="recordBytes"/> and
    /// collects the row(s) reached at the leaf, if any. Records where the path is absent or the
    /// token type mismatches a segment are silently skipped (no rows added).
    /// </summary>
    public static void ExtractRows(
        JsonRawBytes recordBytes,
        IReadOnlyList<KeyPathSegment> keyPath,
        string posHash,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        TraverseKeyPath(
            recordBytes, keyPath, 0, posHash, colName, colNameUtf8,
            rows, keyOrder, keySet, columnTypes, keyObservedCount);
    }

    /// <summary>
    /// Returns the last non-index segment of <paramref name="keyPath"/> — the column name used
    /// for a scalar primitive leaf. Falls back to <c>"value"</c> when every segment is an index
    /// segment (e.g. an empty or all-<c>[n]</c> path).
    /// </summary>
    public static string LastKeySegment(IReadOnlyList<KeyPathSegment> keyPath)
    {
        for (var i = keyPath.Count - 1; i >= 0; i--)
        {
            if (keyPath[i].Kind == KeyPathSegmentKind.Key)
            {
                return keyPath[i].Value;
            }
        }

        return "value";
    }

    private static void TraverseKeyPath(
        JsonRawBytes currentBytes,
        IReadOnlyList<KeyPathSegment> keyPath,
        int segmentIndex,
        string posHash,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        if (segmentIndex == keyPath.Count)
        {
            CollectLeafRows(currentBytes, posHash, colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
            return;
        }

        var segment = keyPath[segmentIndex];

        if (segment.Kind == KeyPathSegmentKind.Index)
        {
            var reader = new Utf8JsonReader(currentBytes.Span);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                return; // Wrong type at this path position — skip record silently.
            }

            if (segmentIndex == keyPath.Count - 1)
            {
                // A trailing index segment expands the same array that would be reached by
                // selecting it directly as the leaf (e.g. "tags" and "tags[0]" must produce
                // identical output, including the "value" column for primitive elements).
                CollectArrayLeafRows(currentBytes, posHash, rows, keyOrder, keySet, columnTypes, keyObservedCount);
                return;
            }

            var elementIndex = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.CurrentDepth != 1)
                {
                    continue;
                }

                var elementBytes = ExtractElementBytes(ref reader, currentBytes);
                TraverseKeyPath(
                    elementBytes, keyPath, segmentIndex + 1, $"{posHash}:{elementIndex}", colName, colNameUtf8,
                    rows, keyOrder, keySet, columnTypes, keyObservedCount);
                elementIndex++;
            }

            return;
        }

        var valueBytes = FindValueByKey(currentBytes, segment.Value);
        if (valueBytes is null)
        {
            return; // Key absent, or current value is not an object — skip record silently.
        }

        TraverseKeyPath(
            valueBytes.Value, keyPath, segmentIndex + 1, posHash, colName, colNameUtf8,
            rows, keyOrder, keySet, columnTypes, keyObservedCount);
    }

    private static void CollectLeafRows(
        JsonRawBytes leafBytes,
        string posHash,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        var reader = new Utf8JsonReader(leafBytes.Span);
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            rows.Add(new FocusedTableRow(leafBytes, posHash));
            var observedKeys = new HashSet<string>(StringComparer.Ordinal);
            SchemaScanner.ScanObject(leafBytes.Span, keyOrder, keySet, columnTypes, observedKeys);
            SchemaScanner.IncrementObservationCounts(observedKeys, keyObservedCount);
            return;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            CollectArrayLeafRows(leafBytes, posHash, rows, keyOrder, keySet, columnTypes, keyObservedCount);
            return;
        }

        // Primitive leaf (including null) — synthesize a single-key object so
        // JsonObjectCellExtractor can extract it without modification.
        // Note: ScanObject is NOT called here, so no type inference is performed;
        // the synthesized column always receives ColumnType.Text (Phase 2 limitation).
        var synthBytes = SynthesizeObject(colNameUtf8, leafBytes.Span);
        rows.Add(new FocusedTableRow(synthBytes, posHash));
        SchemaScanner.RegisterKeyIfNew(colName, keyOrder, keySet);
        SchemaScanner.IncrementObservationCounts([colName], keyObservedCount);
    }

    private static void CollectArrayLeafRows(
        JsonRawBytes leafBytes,
        string posHash,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        var reader = new Utf8JsonReader(leafBytes.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return;
        }

        var elementIndex = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.CurrentDepth != 1)
            {
                continue;
            }

            var isObjectElement = reader.TokenType == JsonTokenType.StartObject;
            var elementBytes = ExtractElementBytes(ref reader, leafBytes);
            var elementHash = $"{posHash}:{elementIndex}";

            if (isObjectElement)
            {
                rows.Add(new FocusedTableRow(elementBytes, elementHash));
                var observedKeys = new HashSet<string>(StringComparer.Ordinal);
                SchemaScanner.ScanObject(elementBytes.Span, keyOrder, keySet, columnTypes, observedKeys);
                SchemaScanner.IncrementObservationCounts(observedKeys, keyObservedCount);
                elementIndex++;
                continue;
            }

            // Primitive element (including null) — synthesize {"value": element}.
            var synthBytes = SynthesizeObject("value"u8, elementBytes.Span);
            rows.Add(new FocusedTableRow(synthBytes, elementHash));
            SchemaScanner.RegisterKeyIfNew("value", keyOrder, keySet);
            SchemaScanner.IncrementObservationCounts(["value"], keyObservedCount);
            elementIndex++;
        }
    }

    private static JsonRawBytes ExtractElementBytes(ref Utf8JsonReader reader, JsonRawBytes containingBytes)
    {
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            return JsonByteExtractor.ExtractNestedBytes(ref reader, containingBytes);
        }

        var start = (int)reader.TokenStartIndex;
        var end = (int)reader.BytesConsumed;
        return containingBytes.Slice(start, end - start);
    }

    private static JsonRawBytes? FindValueByKey(JsonRawBytes objectBytes, string key)
    {
        var reader = new Utf8JsonReader(objectBytes.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (!reader.ValueTextEquals(key))
            {
                reader.Skip();
                continue;
            }

            if (!reader.Read())
            {
                return null;
            }

            return ExtractElementBytes(ref reader, objectBytes);
        }

        return null;
    }

    private static JsonRawBytes SynthesizeObject(ReadOnlySpan<byte> keyUtf8, ReadOnlySpan<byte> valueBytes)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WritePropertyName(keyUtf8);
        writer.WriteRawValue(valueBytes, skipInputValidation: true);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenMemory;
    }
}
