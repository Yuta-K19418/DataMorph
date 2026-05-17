using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DataMorph.Tests.Engine.IO.JsonArray;

[SimpleJob(RuntimeMoniker.NativeAot80)]
[MemoryDiagnoser]
public sealed class ElementByteCacheBenchmarks : IDisposable
{
    private readonly string _tempFilePath1k;
    private readonly int _elementCount;

    public ElementByteCacheBenchmarks()
    {
        var id = Guid.NewGuid();
        _tempFilePath1k = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_cache_bench_1k_{id}.json"
        );

        // TODO: Generate test file with 1k elements, build RowIndexer, set _elementCount
        _elementCount = 0;
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath1k))
        {
            File.Delete(_tempFilePath1k);
        }
    }

    [Benchmark]
    public void GetRow_SequentialAccess()
    {
        // Arrange — create ElementByteCache from _tempFilePath1k
        ReadOnlyMemory<byte> result = default;

        // Act — sequential GetRow(0.._elementCount) calls

        // Assert (prevent JIT elimination)
        _ = result.Length + _elementCount;
    }

    [Benchmark]
    public void GetRow_RandomAccess()
    {
        // Arrange — create ElementByteCache from _tempFilePath1k
        ReadOnlyMemory<byte> result = default;

        // Act — random GetRow calls within 0.._elementCount

        // Assert (prevent JIT elimination)
        _ = result.Length + _elementCount;
    }
}
