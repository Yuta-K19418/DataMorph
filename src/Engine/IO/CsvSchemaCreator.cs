using DataMorph.Engine.Models;
using DataMorph.Engine.Types;
using nietras.SeparatedValues;

namespace DataMorph.Engine.IO;

/// <summary>
/// Creates a TableSchema from CSV header row using nietras.Sep.
/// This is a temporary implementation until proper schema detection is implemented.
/// </summary>
public static class CsvSchemaCreator
{
    /// <summary>
    /// Creates a temporary TableSchema from CSV header row using nietras.Sep.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    /// <returns>A Result containing TableSchema with columns inferred from the header, or an error message.</returns>
    public static Result<TableSchema> CreateSchemaFromCsvHeader(string filePath)
    {
        try
        {
            // Use nietras.Sep to read CSV header
            using var reader = Sep.New(',').Reader().FromFile(filePath);

            if (reader.Header.ColNames.Count == 0)
            {
                return Results.Failure<TableSchema>("CSV file has no header.");
            }

            // Convert header column names to ColumnSchema array
            var columns = new ColumnSchema[reader.Header.ColNames.Count];
            for (var i = 0; i < reader.Header.ColNames.Count; i++)
            {
                var columnName = reader.Header.ColNames[i];
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    columnName = $"Column{i + 1}";
                }

                columns[i] = new ColumnSchema
                {
                    Name = columnName,
                    Type = ColumnType.Text,
                    IsNullable = true,
                    ColumnIndex = i,
                };
            }

            var tableSchema = new TableSchema
            {
                Columns = columns,
                SourceFormat = DataFormat.Csv,
            };

            return Results.Success(tableSchema);
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
        {
            return Results.Failure<TableSchema>($"Error reading CSV header: {ex.Message}");
        }
    }
}
