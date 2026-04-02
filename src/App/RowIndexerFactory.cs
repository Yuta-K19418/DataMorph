using DataMorph.Engine.IO;
using DataMorph.Engine.IO.Csv;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Types;

namespace DataMorph.App;

/// <summary>
/// A factory for creating the appropriate <see cref="IRowIndexer"/> based on the data format.
/// </summary>
internal sealed class RowIndexerFactory
{
    /// <summary>
    /// Creates a row indexer for the specified format and file path.
    /// </summary>
    /// <param name="format">The data format of the file.</param>
    /// <param name="filePath">The path to the file to index.</param>
    /// <returns>An <see cref="IRowIndexer"/> implementation for the file.</returns>
    public static IRowIndexer Create(DataFormat format, string filePath)
    {
        return format switch
        {
            DataFormat.Csv => new DataRowIndexer(filePath),
            DataFormat.JsonLines => new RowIndexer(filePath),
            _ => throw new NotSupportedException($"Unsupported format: {format}"),
        };
    }
}
