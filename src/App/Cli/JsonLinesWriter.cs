using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DataMorph.Engine;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;
using nietras.SeparatedValues;

namespace DataMorph.App.Cli;

/// <summary>
/// Writes output in JSON Lines format from CSV or JSON Lines input.
/// </summary>
internal static class JsonLinesWriter
{
    /// <summary>
    /// Writes JSON Lines output from a CSV input file.
    /// </summary>
    internal static async ValueTask<int> WriteJsonLinesFromCsvAsync(
        Arguments args,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct
    )
    {
        using var reader = await Sep.New(',').Reader().FromFileAsync(args.InputFile, ct).ConfigureAwait(false);

        var header = reader.Header;
        if (header.ColNames.Count == 0)
        {
            await logger.WriteErrorAsync("Input file has no columns");
            return 1;
        }

        var sourceNameToIndex = header.ColNames
            .Select((name, idx) => (name, idx))
            .ToDictionary(x => x.name, x => x.idx, StringComparer.Ordinal);

        using var writer = new StreamWriter(args.OutputFile, append: false, Encoding.UTF8);
        var buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            var ms = new MemoryStream(buffer);
            using var jsonWriter = new Utf8JsonWriter(ms, new JsonWriterOptions { SkipValidation = false });

            while (await reader.MoveNextAsync(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                var record = reader.Current;
                if (!FilterEvaluator.EvaluateCsvFilters(record, outputSchema.Filters))
                {
                    continue;
                }

                ms.SetLength(0);
                jsonWriter.Reset(ms);

                jsonWriter.WriteStartObject();
                for (var i = 0; i < outputSchema.Columns.Count; i++)
                {
                    var col = outputSchema.Columns[i];
                    var value = "<null>";
                    if (sourceNameToIndex.TryGetValue(col.SourceName, out var sourceIndex) && sourceIndex < record.ColCount)
                    {
                        var span = record[sourceIndex].Span;
                        value = span.IsEmpty ? string.Empty : span.ToString();
                    }

                    WriteJsonProperty(jsonWriter, col.OutputName, value);
                }
                jsonWriter.WriteEndObject();
                await jsonWriter.FlushAsync(ct).ConfigureAwait(false);

                var jsonLine = Encoding.UTF8.GetString(buffer, 0, (int)ms.Position);
                await writer.WriteLineAsync(jsonLine.AsMemory(), ct).ConfigureAwait(false);
            }

            await writer.FlushAsync(ct).ConfigureAwait(false);
            return 0;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Writes JSON Lines output from a JSON Lines input file.
    /// </summary>
    internal static async ValueTask<int> WriteJsonLinesFromJsonLinesAsync(
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
        var buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            var ms = new MemoryStream(buffer);
            using var jsonWriter = new Utf8JsonWriter(ms, new JsonWriterOptions { SkipValidation = false });

            var batchStart = 0L;
            while (batchStart < rowIndexer.TotalRows)
            {
                ct.ThrowIfCancellationRequested();

                var (byteOffset, rowOffset) = rowIndexer.GetCheckPoint(batchStart);
                var linesToRead = (int)Math.Min(1000, rowIndexer.TotalRows - batchStart);
                var lines = rowReader.ReadLineBytes(byteOffset, rowOffset, linesToRead);

                foreach (var lineBytes in lines)
                {
                    if (lineBytes.IsEmpty || FilterEvaluator.IsWhiteSpace(lineBytes.Span))
                    {
                        continue;
                    }

                    if (!FilterEvaluator.EvaluateJsonFilters(lineBytes, outputSchema.Filters, filterIndexToNameBytes))
                    {
                        continue;
                    }

                    await WriteJsonLineAsync(
                        writer,
                        lineBytes,
                        outputSchema.Columns,
                        columnNameUtf8Bytes,
                        buffer,
                        ms,
                        jsonWriter,
                        ct
                    ).ConfigureAwait(false);
                }

                batchStart += linesToRead;
            }

            await writer.FlushAsync(ct).ConfigureAwait(false);
            return 0;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async ValueTask WriteJsonLineAsync(
        StreamWriter writer,
        ReadOnlyMemory<byte> lineBytes,
        IReadOnlyList<BatchOutputColumn> columns,
        Memory<byte>[] columnNameUtf8Bytes,
        byte[] buffer,
        MemoryStream ms,
        Utf8JsonWriter jsonWriter,
        CancellationToken ct
    )
    {
        ms.SetLength(0);
        jsonWriter.Reset(ms);

        jsonWriter.WriteStartObject();
        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var value = CellExtractor.ExtractCell(lineBytes.Span, columnNameUtf8Bytes[i].Span);
            WriteJsonProperty(jsonWriter, col.OutputName, value);
        }
        jsonWriter.WriteEndObject();
        await jsonWriter.FlushAsync(ct).ConfigureAwait(false);

        var jsonLine = Encoding.UTF8.GetString(buffer, 0, (int)ms.Position);
        await writer.WriteLineAsync(jsonLine.AsMemory(), ct).ConfigureAwait(false);
    }

    private static void WriteJsonProperty(Utf8JsonWriter writer, string name, string value)
    {
        writer.WritePropertyName(name);
        WriteJsonValue(writer, value);
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string value)
    {
        if (value == "<null>")
        {
            writer.WriteNullValue();
            return;
        }

        if (value == "<error>")
        {
            writer.WriteStringValue(string.Empty);
            return;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            writer.WriteBooleanValue(boolValue);
            return;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            writer.WriteNumberValue(longValue);
            return;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
        {
            writer.WriteNumberValue(doubleValue);
            return;
        }

        writer.WriteStringValue(value);
    }
}
