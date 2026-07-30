using Refedle.Engine;
using Refedle.Engine.Models;

namespace Refedle.App.Cli.Factories;

internal interface IRecordReaderFactory<TReader> where TReader : struct, IRecordReader
{
    ValueTask<TReader> CreateAsync(Arguments args, TableSchema inputSchema, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}
