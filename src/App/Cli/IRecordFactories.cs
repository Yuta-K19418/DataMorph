using DataMorph.Engine;
using DataMorph.Engine.Models;

namespace DataMorph.App.Cli;

internal interface IRecordReaderFactory<TReader> where TReader : struct, IRecordReader
{
    ValueTask<TReader> CreateAsync(Arguments args, TableSchema inputSchema, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}

internal interface IRecordWriterFactory<TWriter> where TWriter : struct, IRecordWriter
{
    ValueTask<TWriter> CreateAsync(Arguments args, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}
