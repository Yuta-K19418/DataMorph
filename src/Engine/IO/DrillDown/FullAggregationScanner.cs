using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.IO.DrillDown;

/// <summary>
/// Full-file DrillDown scan: traverses a KeyPath for every record in the file and
/// collects the leaf values reached, building a union <see cref="TableSchema"/> across all
/// collected rows. Supports all leaf types: JSON Object, JSON Array of Object,
/// JSON Array of Primitive, and scalar Primitive.
/// </summary>
public static class FullAggregationScanner
{
    /// <summary>
    /// Scans <paramref name="filePath"/> for all records and traverses <paramref name="keyPath"/>
    /// to collect leaf values of every matching path. Supports all leaf types: JSON Object,
    /// JSON Array of Object, JSON Array of Primitive, and scalar Primitive.
    /// Returns <c>Failure</c> when format is JSON Object, no rows are collected, or
    /// all collected leaf objects have no keys.
    /// </summary>
    public static Result<(TableSchema schema, IReadOnlyList<FocusedTableRow> rows)> Scan(
        string filePath,
        DataFormat format,
        IReadOnlyList<string> keyPath,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private static void ScanLines(
        MmapService mmap,
        IReadOnlyList<string> keyPath,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static void ScanElements(
        MmapService mmap,
        IReadOnlyList<string> keyPath,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static void TryExtractRows(
        JsonRawBytes recordBytes,
        IReadOnlyList<string> keyPath,
        string posHash,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        throw new NotImplementedException();
    }

    private static void TraverseKeyPath(
        JsonRawBytes currentBytes,
        IReadOnlyList<string> keyPath,
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    private static JsonRawBytes? FindValueByKey(JsonRawBytes objectBytes, string key)
    {
        throw new NotImplementedException();
    }

    private static JsonRawBytes SynthesizeObject(ReadOnlySpan<byte> keyUtf8, ReadOnlySpan<byte> valueBytes)
    {
        throw new NotImplementedException();
    }

    private static string LastKeySegment(IReadOnlyList<string> keyPath)
    {
        throw new NotImplementedException();
    }
}
