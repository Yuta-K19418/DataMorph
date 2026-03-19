using System.Text;
using DataMorph.Engine;
using DataMorph.Engine.Types;

namespace DataMorph.App.Cli;

[RecordWriter(DataFormat.Csv)]
internal readonly struct CsvRecordWriterFactory : IRecordWriterFactory<CsvRecordWriter>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership is transferred to the caller.")]
    public ValueTask<CsvRecordWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        StreamWriter writer = new(args.OutputFile, append: false, Encoding.UTF8);
        return new(new CsvRecordWriter(writer, outputSchema));
    }
}
