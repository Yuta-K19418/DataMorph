using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DataMorph.Tests.Engine.IO.JsonArray;

[SimpleJob(RuntimeMoniker.NativeAot80)]
[MemoryDiagnoser]
public class RowIndexerBenchmarks
{
    private readonly string _tempFilePath;

    public RowIndexerBenchmarks()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"jsonarray_benchmark_{Guid.NewGuid()}.json");
    }

    [GlobalSetup]
    public void Setup()
    {
        throw new NotImplementedException();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        throw new NotImplementedException();
    }

    [Benchmark]
    public void BuildIndex_1kElements()
    {
        throw new NotImplementedException();
    }

    [Benchmark]
    public void BuildIndex_100kElements()
    {
        throw new NotImplementedException();
    }

    [Benchmark]
    public void BuildIndex_1mElements()
    {
        throw new NotImplementedException();
    }

    [Benchmark]
    public void GetCheckPoint_First()
    {
        throw new NotImplementedException();
    }

    [Benchmark]
    public void GetCheckPoint_Middle()
    {
        throw new NotImplementedException();
    }

    [Benchmark]
    public void GetCheckPoint_Last()
    {
        throw new NotImplementedException();
    }
}
