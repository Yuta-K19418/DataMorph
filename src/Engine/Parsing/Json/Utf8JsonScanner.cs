using System.Buffers;
using System.Text.Json;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.Parsing.Json;

/// <summary>
/// High-performance JSON scanner that discovers schema without object allocation.
/// Uses <see cref="Utf8JsonReader"/> for zero-allocation streaming parsing.
/// </summary>
/// <remarks>
/// This class is not thread-safe. Each thread should create its own instance.
/// The scanner maintains mutable state during scanning operations.
/// </remarks>
internal sealed class Utf8JsonScanner
{
    private readonly Dictionary<string, ColumnType> _discoveredColumns;
    private readonly Dictionary<string, bool> _hasNullValues;
    private readonly List<string> _propertyPathStack;
    private int _recordCount;

    /// <summary>
    /// Gets the number of records scanned so far.
    /// </summary>
    public int RecordCount => _recordCount;

    /// <summary>
    /// Gets the discovered columns as a read-only dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, ColumnType> DiscoveredColumns => _discoveredColumns;

    /// <summary>
    /// Gets a read-only dictionary indicating which columns have null values.
    /// </summary>
    public IReadOnlyDictionary<string, bool> HasNullValues => _hasNullValues;

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8JsonScanner"/> class.
    /// </summary>
    public Utf8JsonScanner()
    {
        _discoveredColumns = new Dictionary<string, ColumnType>(StringComparer.Ordinal);
        _hasNullValues = new Dictionary<string, bool>(StringComparer.Ordinal);
        _propertyPathStack = new List<string>(capacity: 8); // Pre-allocate for common nesting depth
    }

    /// <summary>
    /// Scans a JSON array from a UTF-8 byte sequence and discovers the schema.
    /// </summary>
    /// <param name="jsonData">The JSON data as a UTF-8 byte sequence.</param>
    /// <param name="maxRecordsToScan">Maximum number of records to scan for schema discovery (0 = unlimited).</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result ScanJsonArray(ReadOnlySequence<byte> jsonData, int maxRecordsToScan = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRecordsToScan);

        var reader = new Utf8JsonReader(jsonData, new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        try
        {
            return ScanJsonArrayCore(ref reader, maxRecordsToScan);
        }
        catch (JsonException ex)
        {
            return Results.Failure($"Invalid JSON format: {ex.Message}");
        }
    }

    /// <summary>
    /// Scans a JSON array from a UTF-8 byte span and discovers the schema.
    /// </summary>
    /// <param name="jsonData">The JSON data as a UTF-8 byte span.</param>
    /// <param name="maxRecordsToScan">Maximum number of records to scan for schema discovery (0 = unlimited).</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result ScanJsonArray(ReadOnlySpan<byte> jsonData, int maxRecordsToScan = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRecordsToScan);

        var reader = new Utf8JsonReader(jsonData, new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        try
        {
            return ScanJsonArrayCore(ref reader, maxRecordsToScan);
        }
        catch (JsonException ex)
        {
            return Results.Failure($"Invalid JSON format: {ex.Message}");
        }
    }

    /// <summary>
    /// Core scanning logic that processes the JSON array.
    /// </summary>
    private Result ScanJsonArrayCore(ref Utf8JsonReader reader, int maxRecordsToScan)
    {
        // Expect the root to be an array
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return Results.Failure("Expected JSON array at root level.");
        }

        // Scan each object in the array
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return Results.Failure($"Expected JSON object in array, but found {reader.TokenType}.");
            }

            var scanResult = ScanObject(ref reader);
            if (scanResult.IsFailure)
            {
                return scanResult;
            }

            _recordCount++;

            // Stop if we've scanned enough records
            if (maxRecordsToScan > 0 && _recordCount >= maxRecordsToScan)
            {
                break;
            }
        }

        if (_recordCount == 0)
        {
            return Results.Failure("JSON array is empty.");
        }

        return Results.Success();
    }

    /// <summary>
    /// Scans a single JSON object and updates the discovered columns.
    /// </summary>
    private Result ScanObject(ref Utf8JsonReader reader)
    {
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.EndObject:
                    return Results.Success();

                case JsonTokenType.PropertyName:
                    string propertyName = reader.GetString() ?? string.Empty;
                    _propertyPathStack.Add(propertyName);

                    // Read the property value
                    if (!reader.Read())
                    {
                        return Results.Failure("Unexpected end of JSON while reading property value.");
                    }

                    var processResult = ProcessPropertyValue(ref reader);
                    if (processResult.IsFailure)
                    {
                        return processResult;
                    }

                    _propertyPathStack.RemoveAt(_propertyPathStack.Count - 1);
                    break;

                default:
                    return Results.Failure($"Unexpected token {reader.TokenType} in JSON object.");
            }
        }

        return Results.Failure("Unexpected end of JSON while scanning object.");
    }

    /// <summary>
    /// Processes a property value and updates the schema.
    /// </summary>
    private Result ProcessPropertyValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                // Nested object - scan recursively
                return ScanObject(ref reader);

            case JsonTokenType.StartArray:
                // Array - skip for now (could be enhanced to analyze array elements)
                return SkipArray(ref reader);

            case JsonTokenType.Null:
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Number:
            case JsonTokenType.String:
                // Scalar value - infer type and record it
                RecordColumnType(reader.TokenType, reader.ValueSpan);
                return Results.Success();

            default:
                return Results.Failure($"Unexpected token type {reader.TokenType} for property value.");
        }
    }

    /// <summary>
    /// Skips an entire JSON array (for now, arrays are not analyzed).
    /// </summary>
    private static Result SkipArray(ref Utf8JsonReader reader)
    {
        int depth = 1;
        while (depth > 0)
        {
            if (!reader.Read())
            {
                return Results.Failure("Unexpected end of JSON while skipping array.");
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                depth++;
            }
            else if (reader.TokenType == JsonTokenType.EndArray)
            {
                depth--;
            }
        }

        return Results.Success();
    }

    /// <summary>
    /// Records a column type, combining with existing type if the column was seen before.
    /// </summary>
    private void RecordColumnType(JsonTokenType tokenType, ReadOnlySpan<byte> valueSpan)
    {
        string columnPath = string.Join('.', _propertyPathStack);
        ColumnType inferredType = JsonValueTypeInference.InferType(tokenType, valueSpan);

        // Track if this column has ever contained a null value
        if (inferredType == ColumnType.Null)
        {
            _hasNullValues[columnPath] = true;
        }

        if (_discoveredColumns.TryGetValue(columnPath, out ColumnType existingType))
        {
            // Combine types to handle cases where different records have different types
            _discoveredColumns[columnPath] = JsonValueTypeInference.CombineTypes(existingType, inferredType);
        }
        else
        {
            _discoveredColumns[columnPath] = inferredType;
        }
    }
}
