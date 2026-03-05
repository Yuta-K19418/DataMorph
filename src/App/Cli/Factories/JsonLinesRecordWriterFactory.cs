using System.Text;
using System.Text.Json;
using DataMorph.Engine;
using DataMorph.Engine.Types;

namespace DataMorph.App.Cli;

[RecordWriter(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordWriterFactory : IRecordWriterFactory<JsonLinesRecordWriter>
{
    public ValueTask<JsonLinesRecordWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
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
