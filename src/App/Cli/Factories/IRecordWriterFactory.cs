using Refedle.Engine;

namespace Refedle.App.Cli;

internal interface IRecordWriterFactory<TWriter> where TWriter : struct, IRecordWriter
{
    ValueTask<TWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}
