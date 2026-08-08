using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed partial class RecordProcessorTests
{
    private struct TestRecordWriter : IRecordWriter
    {
        public Action? WriteHeaderCallback;
        public Action<string[]>? WriteCellCallback;
        private readonly List<string> _cells;

        public TestRecordWriter(
            Action? writeHeaderCallback = null,
            Action<string[]>? writeCellCallback = null)
        {
            WriteHeaderCallback = writeHeaderCallback;
            WriteCellCallback = writeCellCallback;
            _cells = [];
        }

        public readonly void Dispose() { }
        public readonly ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public readonly ValueTask WriteHeaderAsync(CancellationToken ct)
        {
            WriteHeaderCallback?.Invoke();
            return ValueTask.CompletedTask;
        }

        public readonly ValueTask WriteStartRecordAsync(CancellationToken ct)
        {
            _cells.Clear();
            return ValueTask.CompletedTask;
        }

        public readonly void WriteCellData(int outputColumnIndex, CellData cell)
        {
            _cells.Add(cell.Value.ToString());
        }

        public readonly ValueTask WriteEndRecordAsync(CancellationToken ct)
        {
            WriteCellCallback?.Invoke([.. _cells]);
            return ValueTask.CompletedTask;
        }

        public readonly ValueTask FlushAsync(CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }
}
