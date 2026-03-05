using DataMorph.Engine;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;
using nietras.SeparatedValues;

namespace DataMorph.App.Cli;

[RecordReader(DataFormat.Csv)]
internal readonly struct CsvRecordReaderFactory : IRecordReaderFactory<CsvRecordReader>
{
    public async ValueTask<CsvRecordReader> CreateAsync(Arguments args, TableSchema inputSchema, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        var sepReader = await Sep.New(',').Reader().FromFileAsync(args.InputFile, ct).ConfigureAwait(false);
        return new CsvRecordReader(sepReader, outputSchema);
    }
}
