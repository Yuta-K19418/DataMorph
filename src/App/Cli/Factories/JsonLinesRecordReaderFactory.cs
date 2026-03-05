using DataMorph.Engine;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.App.Cli;

[RecordReader(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordReaderFactory : IRecordReaderFactory<JsonLinesRecordReader>
{
    public ValueTask<JsonLinesRecordReader> CreateAsync(Arguments args, TableSchema inputSchema, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        var rowIndexer = new Engine.IO.JsonLines.RowIndexer(args.InputFile);
        rowIndexer.BuildIndex();
        var rowReader = new Engine.IO.JsonLines.RowReader(args.InputFile);
        return new ValueTask<JsonLinesRecordReader>(new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema));
    }
}
