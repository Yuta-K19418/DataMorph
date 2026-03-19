using DataMorph.Engine;
using DataMorph.Engine.Types;

namespace DataMorph.App.Cli;

[RecordWriter(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordWriterFactory : IRecordWriterFactory<JsonLinesRecordWriter>
{
    private const int StreamBufferSize = 1024 * 64; // 64 KB
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership is transferred to the caller.")]
    public ValueTask<JsonLinesRecordWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        FileStream stream = new(args.OutputFile, FileMode.Create, FileAccess.Write, FileShare.Read, StreamBufferSize, useAsync: true);
        return new(new JsonLinesRecordWriter(stream, outputSchema));
    }
}
