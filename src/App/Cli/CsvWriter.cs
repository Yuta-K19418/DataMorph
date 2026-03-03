using System.Text;
using DataMorph.Engine;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;
using nietras.SeparatedValues;

namespace DataMorph.App.Cli;

/// <summary>
/// Writes output in CSV format from CSV or JSON Lines input.
/// </summary>
internal static class CsvWriter
{
    /// <summary>
    /// Writes CSV output from a CSV input file.
    /// </summary>
    internal static async ValueTask<int> WriteCsvFromCsvAsync(
        Arguments args,
        BatchOutputSchema outputSchema,
        CancellationToken ct
    )
    {
        using var reader = await Sep.New(',').Reader().FromFileAsync(args.InputFile, ct).ConfigureAwait(false);

        var header = reader.Header;
        if (header.ColNames.Count == 0)
        {
            await Console.Error.WriteLineAsync("Input file has no columns");
            return 1;
        }

        var sourceNameToIndex = header.ColNames
            .Select((name, idx) => (name, idx))
            .ToDictionary(x => x.name, x => x.idx, StringComparer.Ordinal);

        using var writer = new StreamWriter(args.OutputFile, append: false, Encoding.UTF8);
        await WriteHeaderAsync(writer, outputSchema, ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        while (await reader.MoveNextAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var record = reader.Current;
            if (!FilterEvaluator.EvaluateCsvFilters(record, outputSchema.Filters))
            {
                continue;
            }

            sb.Clear();
            for (var i = 0; i < outputSchema.Columns.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var col = outputSchema.Columns[i];
                if (sourceNameToIndex.TryGetValue(col.SourceName, out var sourceIndex) && sourceIndex < record.ColCount)
                {
                    var valueSpan = record[sourceIndex].Span;
                    if (!valueSpan.IsEmpty)
                    {
                        CsvEscaper.EscapeCsvValueToBuilder(valueSpan, sb);
                    }
                }
            }

            await writer.WriteLineAsync(sb.ToString().AsMemory(), ct).ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// Writes CSV output from a JSON Lines input file.
    /// </summary>
    internal static async ValueTask<int> WriteCsvFromJsonLinesAsync(
        Arguments args,
        TableSchema inputSchema,
        BatchOutputSchema outputSchema,
        CancellationToken ct
    )
    {
        var rowIndexer = new RowIndexer(args.InputFile);
        rowIndexer.BuildIndex();
        using var rowReader = new RowReader(args.InputFile);

        var columnNameUtf8Bytes = outputSchema.Columns
            .Select(c => Encoding.UTF8.GetBytes(c.SourceName).AsMemory())
            .ToArray();

        // Pre-build inverted lookup: source column index → UTF-8 column name bytes (zero-alloc per row)
        var filterIndexToNameBytes = inputSchema.Columns
            .ToDictionary(c => c.ColumnIndex, c => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(c.Name));

        using var writer = new StreamWriter(args.OutputFile, append: false, Encoding.UTF8);
        await WriteHeaderAsync(writer, outputSchema, ct).ConfigureAwait(false);

        var batchStart = 0L;
        while (batchStart < rowIndexer.TotalRows)
        {
            ct.ThrowIfCancellationRequested();

            var (byteOffset, rowOffset) = rowIndexer.GetCheckPoint(batchStart);
            var linesToRead = (int)Math.Min(1000, rowIndexer.TotalRows - batchStart);
            var lines = rowReader.ReadLineBytes(byteOffset, rowOffset, linesToRead);

            await ProcessJsonLinesBatchAsync(
                writer,
                lines,
                outputSchema.Columns,
                columnNameUtf8Bytes,
                outputSchema.Filters,
                filterIndexToNameBytes,
                ct
            ).ConfigureAwait(false);

            batchStart += linesToRead;
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
        return 0;
    }

    private static async ValueTask ProcessJsonLinesBatchAsync(
        StreamWriter writer,
        IReadOnlyList<ReadOnlyMemory<byte>> lines,
        IReadOnlyList<BatchOutputColumn> columns,
        Memory<byte>[] columnNameUtf8Bytes,
        IReadOnlyList<Engine.Filtering.FilterSpec> filters,
        Dictionary<int, ReadOnlyMemory<byte>> filterIndexToNameBytes,
        CancellationToken ct
    )
    {
        foreach (var lineBytes in lines)
        {
            if (lineBytes.IsEmpty || FilterEvaluator.IsWhiteSpace(lineBytes.Span))
            {
                continue;
            }

            if (!FilterEvaluator.EvaluateJsonFilters(lineBytes, (IReadOnlyList<Engine.Filtering.FilterSpec>)filters, filterIndexToNameBytes))
            {
                continue;
            }

            for (var i = 0; i < columns.Count; i++)
            {
                if (i > 0)
                {
                    await writer.WriteAsync(",".AsMemory(), ct).ConfigureAwait(false);
                }

                var value = CellExtractor.ExtractCell(lineBytes.Span, columnNameUtf8Bytes[i].Span);
                var escaped = value is "<null>" or "<error>" ? string.Empty : CsvEscaper.EscapeCsvValue(value.AsSpan());
                await writer.WriteAsync(escaped.AsMemory(), ct).ConfigureAwait(false);
            }

            await writer.WriteLineAsync(string.Empty.AsMemory(), ct).ConfigureAwait(false);
        }
    }



    private static async ValueTask WriteHeaderAsync(
        StreamWriter writer,
        BatchOutputSchema outputSchema,
        CancellationToken ct
    )
    {
        for (var i = 0; i < outputSchema.Columns.Count; i++)
        {
            if (i > 0)
            {
                await writer.WriteAsync(",".AsMemory(), ct).ConfigureAwait(false);
            }

            var col = outputSchema.Columns[i];
            var escaped = CsvEscaper.EscapeCsvValue(col.OutputName);
            await writer.WriteAsync(escaped.AsMemory(), ct).ConfigureAwait(false);
        }

        await writer.WriteLineAsync(string.Empty.AsMemory(), ct).ConfigureAwait(false);
    }


}
