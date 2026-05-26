using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DataMorph.Tests.Engine.IO.JsonObject;

[SimpleJob(RuntimeMoniker.NativeAot80)]
[MemoryDiagnoser]
public sealed class TopLevelScannerBenchmarks
{
    private const int KeyCount10 = 10;
    private const int KeyCount100 = 100;

    private string? _tempFilePath10Keys;
    private string? _tempFilePath100KeysSmall;
    private string? _tempFilePath100KeysLarge;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var id = Guid.NewGuid();
        _tempFilePath10Keys = Path.Combine(
            Path.GetTempPath(),
            $"jsonobject_bench_10_{id}.json"
        );
        _tempFilePath100KeysSmall = Path.Combine(
            Path.GetTempPath(),
            $"jsonobject_bench_100small_{id}.json"
        );
        _tempFilePath100KeysLarge = Path.Combine(
            Path.GetTempPath(),
            $"jsonobject_bench_100large_{id}.json"
        );

        WriteKeyValues(_tempFilePath10Keys, KeyCount10, static i => $"{i}");
        WriteKeyValues(_tempFilePath100KeysSmall, KeyCount100, static i => $"{i}");
        WriteKeyValues(_tempFilePath100KeysLarge, KeyCount100, static i => GenerateNestedObject(i));
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_tempFilePath10Keys is not null)
        {
            File.Delete(_tempFilePath10Keys);
        }

        if (_tempFilePath100KeysSmall is not null)
        {
            File.Delete(_tempFilePath100KeysSmall);
        }

        if (_tempFilePath100KeysLarge is not null)
        {
            File.Delete(_tempFilePath100KeysLarge);
        }
    }

    [Benchmark]
    public void Scan_10Keys_SmallValues()
    {
        throw new NotImplementedException();
    }

    [Benchmark]
    public void Scan_100Keys_SmallValues()
    {
        throw new NotImplementedException();
    }

    [Benchmark]
    public void Scan_100Keys_LargeNestedValues()
    {
        throw new NotImplementedException();
    }

    private static void WriteKeyValues(string path, int count, Func<int, string> valueFactory)
    {
        using var writer = new StreamWriter(path);
        writer.Write('{');
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write($"\"key{i}\":{valueFactory(i)}");
        }

        writer.Write('}');
    }

    private static string GenerateNestedObject(int seed)
    {
        var fields = string.Join(",", Enumerable.Range(0, 1000).Select(j => $"\"f{j}\":{seed + j}"));
        return $"{{{fields}}}";
    }
}
