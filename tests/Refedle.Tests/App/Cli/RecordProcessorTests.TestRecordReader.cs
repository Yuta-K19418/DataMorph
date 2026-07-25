using DataMorph.App.Cli;
using DataMorph.Engine.Filtering;
using CliFilterEvaluator = DataMorph.App.Cli.FilterEvaluator;

namespace DataMorph.Tests.App.Cli;

public sealed partial class RecordProcessorTests
{
    private struct TestRecordReader : IRecordReader
    {
        public string[][] Records;
        public IReadOnlyList<FilterSpec> Filters;
        public CancellationTokenSource? CancellationTokenSource;
        public int CancelAfter;
        private int _currentIndex;
        private int _recordsProcessed;

        public TestRecordReader(
            string[][] records,
            IReadOnlyList<FilterSpec> filters,
            CancellationTokenSource? cancellationTokenSource = null,
            int cancelAfter = -1)
        {
            Records = records;
            Filters = filters;
            CancellationTokenSource = cancellationTokenSource;
            CancelAfter = cancelAfter;
            _currentIndex = -1;
            _recordsProcessed = 0;
        }

        public void Dispose() { }

        public ValueTask<bool> MoveNextAsync(CancellationToken ct)
        {
            _recordsProcessed++;

            if (CancelAfter >= 0 && _recordsProcessed > CancelAfter && CancellationTokenSource != null)
            {
                CancellationTokenSource.Cancel();
            }

            _currentIndex++;

            var hasNext = _currentIndex < Records.Length;

            return ValueTask.FromResult(hasNext);
        }

        public bool EvaluateFilters()
        {
            if (Filters.Count == 0)
            {
                return true;
            }

            var currentRecord = Records[_currentIndex];

            foreach (var filter in Filters)
            {
                if (filter.SourceColumnIndex >= currentRecord.Length)
                {
                    return false;
                }

                var valueSpan = currentRecord[filter.SourceColumnIndex].AsSpan();

                if (!CliFilterEvaluator.EvaluateFilter(valueSpan, filter))
                {
                    return false;
                }
            }

            return true;
        }

        public ReadOnlySpan<char> GetCellSpan(int outputColumnIndex)
        {
            if (_currentIndex < 0 || _currentIndex >= Records.Length)
            {
                return [];
            }

            var currentRecord = Records[_currentIndex];

            if (outputColumnIndex >= currentRecord.Length)
            {
                return [];
            }

            return currentRecord[outputColumnIndex].AsSpan();
        }
    }
}
