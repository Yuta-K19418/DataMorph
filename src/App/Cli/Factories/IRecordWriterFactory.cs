using DataMorph.Engine;

namespace DataMorph.App.Cli;

internal interface IRecordWriterFactory<TWriter> where TWriter : struct, IRecordWriter
{
    ValueTask<TWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}
