using DataMorph.Engine;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.App.Cli;

[RecordReader(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordReaderFactory : IRecordReaderFactory<JsonLinesRecordReader>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership is transferred to the caller.")]
    public ValueTask<JsonLinesRecordReader> CreateAsync(Arguments args, TableSchema inputSchema, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        Engine.IO.JsonLines.RowIndexer rowIndexer = new(args.InputFile);
        rowIndexer.BuildIndex(CancellationToken.None);
        Engine.IO.JsonLines.RowReader rowReader = new(args.InputFile);
        return new(new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema));
    }
}
