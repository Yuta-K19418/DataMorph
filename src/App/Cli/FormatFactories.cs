using System.Text;
using System.Text.Json;
using DataMorph.Engine;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;
using nietras.SeparatedValues;

namespace DataMorph.App.Cli;

[RecordReader(DataFormat.Csv)]
internal readonly struct CsvRecordReaderFactory : IRecordReaderFactory<CsvRecordReader>
{
    public async ValueTask<CsvRecordReader> CreateAsync(Arguments args, TableSchema inputSchema, BatchOutputSchema outputSchema, CancellationToken ct)
    {
        var sepReader = await Sep.New(',').Reader().FromFileAsync(args.InputFile, ct).ConfigureAwait(false);
        return new CsvRecordReader(sepReader, outputSchema);
    }
}

[RecordWriter(DataFormat.Csv)]
internal readonly struct CsvRecordWriterFactory : IRecordWriterFactory<CsvRecordWriter>
{
    public ValueTask<CsvRecordWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, CancellationToken ct)
    {
        var writer = new StreamWriter(args.OutputFile, append: false, Encoding.UTF8);
        return new ValueTask<CsvRecordWriter>(new CsvRecordWriter(writer, outputSchema));
    }
}

[RecordReader(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordReaderFactory : IRecordReaderFactory<JsonLinesRecordReader>
{
    public ValueTask<JsonLinesRecordReader> CreateAsync(Arguments args, TableSchema inputSchema, BatchOutputSchema outputSchema, CancellationToken ct)
    {
        var rowIndexer = new Engine.IO.JsonLines.RowIndexer(args.InputFile);
        rowIndexer.BuildIndex();
        var rowReader = new Engine.IO.JsonLines.RowReader(args.InputFile);
        return new ValueTask<JsonLinesRecordReader>(new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema));
    }
}

[RecordWriter(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordWriterFactory : IRecordWriterFactory<JsonLinesRecordWriter>
{
    public ValueTask<JsonLinesRecordWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, CancellationToken ct)
    {
        var writer = new StreamWriter(args.OutputFile, append: false, Encoding.UTF8);
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            var ms = new MemoryStream(buffer);
            var jsonWriter = new Utf8JsonWriter(ms, new JsonWriterOptions { SkipValidation = false });
            return new ValueTask<JsonLinesRecordWriter>(new JsonLinesRecordWriter(writer, buffer, ms, jsonWriter, outputSchema));
        }
        catch
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }
}
