using DataMorph.Engine;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.IO;

/// <summary>
/// Scans CSV data to infer schema (header + column types).
/// Analyzes a single row to determine column types.
/// </summary>
public static class CsvSchemaScanner
{
    /// <summary>
    /// Scans a single CSV row and returns the inferred schema.
    /// </summary>
    /// <param name="columnNames">Column names from header.</param>
    /// <param name="row">The single row to analyze for type inference.</param>
    /// <param name="totalRowCount">Total number of rows in the CSV.</param>
    /// <returns>Result containing TableSchema or error message.</returns>
    public static Result<TableSchema> ScanSchema(
        IReadOnlyList<string> columnNames,
        CsvDataRow row,
        long totalRowCount
    )
    {
        // Validate parameters
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentOutOfRangeException.ThrowIfNegative(totalRowCount);

        if (columnNames.Count == 0)
        {
            return Results.Failure<TableSchema>("CSV has no columns");
        }

        // Ensure row has same number of columns as column names
        if (row.Count != columnNames.Count)
        {
            return Results.Failure<TableSchema>(
                $"Row has {row.Count} columns but header has {columnNames.Count} columns"
            );
        }

        // Infer column types from the single row
        var columnSchemas = InferColumnTypes(columnNames, row);

        // Build the table schema
        var tableSchema = new TableSchema
        {
            Columns = columnSchemas,
            RowCount = totalRowCount,
            SourceFormat = DataFormat.Csv,
        };

        return Results.Success(tableSchema);
    }

    /// <summary>
    /// Infers column types from a single row.
    /// </summary>
    /// <param name="columnNames">Column names.</param>
    /// <param name="row">Single row to analyze.</param>
    /// <returns>List of ColumnSchema with inferred types.</returns>
    private static ColumnSchema[] InferColumnTypes(
        IReadOnlyList<string> columnNames,
        CsvDataRow row
    )
    {
        var columnCount = columnNames.Count;
        var columnSchema = new ColumnSchema[columnCount];

        for (var i = 0; i < columnCount; i++)
        {
            var columnName = columnNames[i];
            var columnValue = row[i];
            var valueSpan = columnValue.Span;

            // Determine nullable status (if value is empty or whitespace-only)
            var isNullable = CsvTypeInferrer.IsEmptyOrWhitespace(valueSpan);

            // Infer type from the value
            var columnType = CsvTypeInferrer.InferType(valueSpan);

            // Generate column name if empty
            var finalColumnName = string.IsNullOrWhiteSpace(columnName)
                ? $"Column{i + 1}"
                : columnName;

            columnSchema[i] = new ColumnSchema
            {
                Name = finalColumnName,
                Type = columnType,
                IsNullable = isNullable,
                ColumnIndex = i,
            };
        }

        return columnSchema;
    }

    /// <summary>
    /// Refines an existing schema by analyzing additional rows.
    /// Updates column types and nullable status based on observed values.
    /// </summary>
    /// <param name="schema">The existing schema to refine.</param>
    /// <param name="row">The row to analyze for type refinement.</param>
    /// <returns>Result indicating success or failure.</returns>
    public static Result RefineSchema(TableSchema schema, CsvDataRow row)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(row);

        // Validate row has same number of columns as schema
        if (schema.Columns.Count != row.Count)
        {
            return Results.Failure(
                $"Row has {row.Count} columns but schema has {schema.Columns.Count} columns"
            );
        }

        // Process each column
        for (var i = 0; i < schema.Columns.Count; i++)
        {
            var columnSchema = schema.Columns[i];
            var columnValue = row[i];
            var valueSpan = columnValue.Span;

            // Update nullable status for empty or whitespace values
            if (CsvTypeInferrer.IsEmptyOrWhitespace(valueSpan))
            {
                columnSchema.MarkNullable();
                continue;
            }

            // Infer type for this value
            var inferredType = CsvTypeInferrer.InferType(valueSpan);

            // Update column type (will resolve type conflicts if needed)
            columnSchema.UpdateColumnType(inferredType);
        }

        return Results.Success();
    }
}
