using Refedle.Engine;

namespace Refedle.App.Cli.Factories;

internal interface IRecordWriterFactory<TWriter> where TWriter : struct, IRecordWriter
{
    ValueTask<TWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}
