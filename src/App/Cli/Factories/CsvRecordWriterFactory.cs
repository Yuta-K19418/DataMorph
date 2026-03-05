using System.Text;
using DataMorph.Engine;
using DataMorph.Engine.Types;

namespace DataMorph.App.Cli;

[RecordWriter(DataFormat.Csv)]
internal readonly struct CsvRecordWriterFactory : IRecordWriterFactory<CsvRecordWriter>
{
    public ValueTask<CsvRecordWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        var writer = new StreamWriter(args.OutputFile, append: false, Encoding.UTF8);
        return new ValueTask<CsvRecordWriter>(new CsvRecordWriter(writer, outputSchema));
    }
}
