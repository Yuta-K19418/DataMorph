using System.Text.Json;
using DataMorph.Engine.IO.Json;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.IO.DrillDown;

/// <summary>
/// Parses a selected node's raw bytes in memory and returns the inferred schema
/// and an ordered list of child object bytes.
/// </summary>
public static class DrillDownSchemaExtractor
{
    /// <summary>
    /// Parses <paramref name="nodeBytes"/> as a JSON array whose direct children must all be
    /// JSON Objects. Infers a <see cref="TableSchema"/> (union of top-level keys) and returns
    /// the ordered child value bytes.
    /// Returns <c>Failure</c> when children include non-Objects, the array is empty, or the
    /// JSON is malformed.
    /// </summary>
    public static Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)>
        ExtractFromNode(JsonRawBytes nodeBytes, DataFormat format)
    {
        var childrenResult = ExtractChildren(nodeBytes);
        if (childrenResult.IsFailure)
        {
            return Results.Failure<(TableSchema, IReadOnlyList<JsonRawBytes>)>(childrenResult.Error);
        }

        var schemaResult = BuildSchema(childrenResult.Value, format);
        if (schemaResult.IsFailure)
        {
            return Results.Failure<(TableSchema, IReadOnlyList<JsonRawBytes>)>(schemaResult.Error);
        }

        return Results.Success<(TableSchema, IReadOnlyList<JsonRawBytes>)>(
            (schemaResult.Value, childrenResult.Value));
    }

    private static Result<List<JsonRawBytes>> ExtractChildren(JsonRawBytes nodeBytes)
    {
        try
        {
            return ParseArrayElements(nodeBytes);
        }
        catch (JsonException)
        {
            return Results.Failure<List<JsonRawBytes>>("Malformed JSON.");
        }
    }

    private static Result<List<JsonRawBytes>> ParseArrayElements(JsonRawBytes nodeBytes)
    {
        List<JsonRawBytes> children = [];
        var reader = new Utf8JsonReader(nodeBytes.Span);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return Results.Failure<List<JsonRawBytes>>("Node is not a JSON Array.");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return Results.Failure<List<JsonRawBytes>>("Array contains non-Object elements.");
            }

            // ExtractNestedBytes reads through the object and stops at EndObject.
            // The next Read() advances to the following token, so the loop body only ever sees
            // StartObject or EndArray.
            children.Add(JsonByteExtractor.ExtractNestedBytes(ref reader, nodeBytes));
        }

        if (children.Count == 0)
        {
            return Results.Failure<List<JsonRawBytes>>("Array is empty.");
        }

        return Results.Success(children);
    }

    private static Result<TableSchema> BuildSchema(List<JsonRawBytes> childRawValues, DataFormat format)
    {
        List<string> keyOrder = [];
        var keySet = new HashSet<string>(StringComparer.Ordinal);
        var columnTypes = new Dictionary<string, ColumnType>(StringComparer.Ordinal);
        var keyObservedCount = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var childBytes in childRawValues)
        {
            var observedKeys = new HashSet<string>(StringComparer.Ordinal);
            ScanObject(childBytes.Span, keyOrder, keySet, columnTypes, observedKeys);
            IncrementObservationCounts(observedKeys, keyObservedCount);
        }

        if (keyOrder.Count == 0)
        {
            return Results.Failure<TableSchema>("All child objects have no keys");
        }

        List<ColumnSchema> columns = [];
        for (var i = 0; i < keyOrder.Count; i++)
        {
            var key = keyOrder[i];
            var observedCount = keyObservedCount.GetValueOrDefault(key);
            columns.Add(new ColumnSchema
            {
                Name = key,
                Type = columnTypes.GetValueOrDefault(key, ColumnType.Text),
                IsNullable = observedCount < childRawValues.Count,
                ColumnIndex = i,
            });
        }

        return Results.Success(new TableSchema { Columns = columns, SourceFormat = format });
    }

    private static void IncrementObservationCounts(
        HashSet<string> observedKeys,
        Dictionary<string, int> keyObservedCount)
    {
        foreach (var key in observedKeys)
        {
            keyObservedCount[key] = keyObservedCount.GetValueOrDefault(key) + 1;
        }
    }

    private static void ScanObject(
        ReadOnlySpan<byte> objectBytes,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        HashSet<string> observedKeys)
    {
        var reader = new Utf8JsonReader(objectBytes);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            ScanSingleProperty(ref reader, keyOrder, keySet, columnTypes, observedKeys);
        }
    }

    private static void ScanSingleProperty(
        ref Utf8JsonReader reader,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        HashSet<string> observedKeys)
    {
        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            return;
        }

        var key = reader.GetString() ?? string.Empty;

        if (!reader.Read())
        {
            return;
        }

        var tokenType = reader.TokenType;
        var valueSpan = reader.ValueSpan;

        if (tokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        RegisterKeyIfNew(key, keyOrder, keySet);

        if (TypeInferrer.IsNullToken(tokenType))
        {
            return;
        }

        var inferredType = TypeInferrer.InferType(tokenType, valueSpan);
        observedKeys.Add(key);
        MergeColumnType(key, inferredType, columnTypes);
    }

    private static void MergeColumnType(
        string key,
        ColumnType inferredType,
        Dictionary<string, ColumnType> columnTypes)
    {
        if (!columnTypes.TryGetValue(key, out var existingType))
        {
            columnTypes[key] = inferredType;
            return;
        }

        columnTypes[key] = ColumnTypeResolver.Resolve(existingType, inferredType);
    }

    private static void RegisterKeyIfNew(string key, List<string> keyOrder, HashSet<string> keySet)
    {
        if (keySet.Contains(key))
        {
            return;
        }

        keyOrder.Add(key);
        keySet.Add(key);
    }
}
