using DataMorph.App.Cli;

namespace DataMorph.Tests.App.Cli;

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

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask WriteHeaderAsync(CancellationToken ct)
        {
            WriteHeaderCallback?.Invoke();
            return ValueTask.CompletedTask;
        }

        public ValueTask WriteStartRecordAsync(CancellationToken ct)
        {
            _cells.Clear();
            return ValueTask.CompletedTask;
        }

        public void WriteCellSpan(int outputColumnIndex, ReadOnlySpan<char> value)
        {
            _cells.Add(value.ToString());
        }

        public ValueTask WriteEndRecordAsync(CancellationToken ct)
        {
            WriteCellCallback?.Invoke([.. _cells]);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }
}
