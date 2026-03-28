using System.Diagnostics;
using DataMorph.Engine;
using DataMorph.Engine.Models;

namespace DataMorph.App.Cli;

internal static class RecordProcessor
{
    public static async ValueTask<ExitCode> ProcessAsync<TReader, TWriter>(
        TReader reader,
        TWriter writer,
        IReadOnlyList<BatchOutputColumn> columns,
        CancellationToken ct)
        where TReader : struct, IRecordReader
        where TWriter : struct, IRecordWriter
    {
        await writer.WriteHeaderAsync(ct).ConfigureAwait(false);

        while (await reader.MoveNextAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            if (!reader.EvaluateFilters())
            {
                continue;
            }

            await writer.WriteStartRecordAsync(ct).ConfigureAwait(false);

            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].Transform is not { } transform)
                {
                    writer.WriteCellSpan(i, reader.GetCellSpan(i));
                    continue;
                }
                var span = transform switch
                {
                    FillSpec fill => fill.Value.AsSpan(),
                    TimestampFormatSpec => throw new NotImplementedException(),
                    _ => throw new UnreachableException($"Unhandled CellTransformSpec: {transform.GetType().Name}"),
                };
                writer.WriteCellSpan(i, span);
            }

            await writer.WriteEndRecordAsync(ct).ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
        return ExitCode.Success;
    }

    private static string ApplyTimestampFormat(ReadOnlySpan<char> raw, TimestampFormatSpec fmt)
    {
        throw new NotImplementedException();
    }
}
