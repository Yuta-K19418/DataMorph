namespace DataMorph.App.Cli;

internal static class RecordProcessor
{
    public static async ValueTask<int> ProcessAsync<TReader, TWriter>(
        TReader reader,
        TWriter writer,
        int outputColumnCount,
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

            for (var i = 0; i < outputColumnCount; i++)
            {
                var span = reader.GetCellSpan(i);
                writer.WriteCellSpan(i, span);
            }

            await writer.WriteEndRecordAsync(ct).ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
        return 0;
    }
}
