using System.Globalization;
using Refedle.Engine;
using Refedle.Engine.Models;

namespace Refedle.App.Cli;

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
                if (columns[i].Transform is null)
                {
                    writer.WriteCellData(i, reader.GetCellData(i));
                    continue;
                }

                // Step 2: format via the transform (FillSpec/TimestampFormatSpec), classify the
                // result via CellEncodingClassifier, and wrap it in a CellData for WriteCellData.
                throw new NotImplementedException();
            }

            await writer.WriteEndRecordAsync(ct).ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
        return ExitCode.Success;
    }

#pragma warning disable IDE0051 // Called from Step 2 transform wiring; restored then.
    private static string ApplyTimestampFormat(ReadOnlySpan<char> raw, TimestampFormatSpec fmt)
    {
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new FormatException($"Could not parse timestamp value '{raw}'.");
        }

        return parsed.ToString(fmt.TargetFormat, CultureInfo.InvariantCulture);
    }
#pragma warning restore IDE0051
}
