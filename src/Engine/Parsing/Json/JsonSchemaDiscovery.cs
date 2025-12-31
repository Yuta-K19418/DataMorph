using System.Buffers;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.Parsing.Json;

/// <summary>
/// Orchestrates JSON schema discovery using the high-performance <see cref="Utf8JsonScanner"/>.
/// Produces a <see cref="TableSchema"/> from JSON array data.
/// </summary>
public sealed class JsonSchemaDiscovery
{
    private readonly int _maxRecordsToScan;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaDiscovery"/> class.
    /// </summary>
    /// <param name="maxRecordsToScan">Maximum number of records to scan for schema discovery (default: 1000, 0 = unlimited).</param>
    public JsonSchemaDiscovery(int maxRecordsToScan = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRecordsToScan);
        _maxRecordsToScan = maxRecordsToScan;
    }

    /// <summary>
    /// Discovers the schema from a JSON array in a UTF-8 byte sequence.
    /// </summary>
    /// <param name="jsonData">The JSON data as a UTF-8 byte sequence.</param>
    /// <returns>A result containing the discovered table schema or an error message.</returns>
    public Result<TableSchema> DiscoverSchema(ReadOnlySequence<byte> jsonData)
    {
        var scanner = new Utf8JsonScanner();
        var scanResult = scanner.ScanJsonArray(jsonData, _maxRecordsToScan);

        if (scanResult.IsFailure)
        {
            return Results.Failure<TableSchema>(scanResult.Error);
        }

        return BuildTableSchema(scanner);
    }

    /// <summary>
    /// Discovers the schema from a JSON array in a UTF-8 byte span.
    /// </summary>
    /// <param name="jsonData">The JSON data as a UTF-8 byte span.</param>
    /// <returns>A result containing the discovered table schema or an error message.</returns>
    public Result<TableSchema> DiscoverSchema(ReadOnlySpan<byte> jsonData)
    {
        var scanner = new Utf8JsonScanner();
        var scanResult = scanner.ScanJsonArray(jsonData, _maxRecordsToScan);

        if (scanResult.IsFailure)
        {
            return Results.Failure<TableSchema>(scanResult.Error);
        }

        return BuildTableSchema(scanner);
    }

    /// <summary>
    /// Builds a <see cref="TableSchema"/> from the scanned column information.
    /// </summary>
    private static Result<TableSchema> BuildTableSchema(Utf8JsonScanner scanner)
    {
        var discoveredColumns = scanner.DiscoveredColumns;

        if (discoveredColumns.Count == 0)
        {
            return Results.Failure<TableSchema>("No columns discovered in JSON data.");
        }

        // Sort columns alphabetically for consistent ordering
        var sortedColumns = discoveredColumns
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        // Build ColumnSchema objects
        var columns = new List<ColumnSchema>(sortedColumns.Count);
        for (int i = 0; i < sortedColumns.Count; i++)
        {
            var (columnName, columnType) = sortedColumns[i];

            // Check if column is nullable (if it was ever seen as Null)
            bool isNullable = scanner.HasNullValues.TryGetValue(columnName, out bool hasNull) && hasNull;

            // If the column type is Null only, default to Text
            var finalType = columnType == ColumnType.Null ? ColumnType.Text : columnType;

            columns.Add(new ColumnSchema
            {
                Name = columnName,
                Type = finalType,
                IsNullable = isNullable,
                ColumnIndex = i
            });
        }

        var schema = new TableSchema
        {
            Columns = columns,
            RowCount = scanner.RecordCount,
            SourceFormat = DataFormat.Json
        };

        return Results.Success(schema);
    }
}
