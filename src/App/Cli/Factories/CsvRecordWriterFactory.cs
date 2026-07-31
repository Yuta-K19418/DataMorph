using System.Text;
using Refedle.Engine;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Factories;

[RecordWriter(DataFormat.Csv)]
internal readonly struct CsvRecordWriterFactory : IRecordWriterFactory<CsvRecordWriter>
{
    public ValueTask<CsvRecordWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        StreamWriter writer = new(args.OutputFile, append: false, Encoding.UTF8);
        return new(new CsvRecordWriter(writer, outputSchema));
    }
}
