using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.Engine.IO;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class RowIndexerBenchmarks : IDisposable
{
    private readonly string _smallFilePath;
    private readonly string _mediumFilePath;
    private readonly string _largeFilePath;
    private readonly MmapService _smallMmap;
    private readonly MmapService _mediumMmap;
    private readonly MmapService _largeMmap;

    public RowIndexerBenchmarks()
    {
        // Small file: 1,000 lines (~50 KB)
        _smallFilePath = Path.Combine(Path.GetTempPath(), $"bench_small_{Guid.NewGuid()}.txt");
        var smallLines = Enumerable.Range(0, 1_000).Select(i => $"Line {i:D6} with some additional text content");
        File.WriteAllLines(_smallFilePath, smallLines);
        _smallMmap = MmapService.Open(_smallFilePath).Value;

        // Medium file: 100,000 lines (~5 MB)
        _mediumFilePath = Path.Combine(Path.GetTempPath(), $"bench_medium_{Guid.NewGuid()}.txt");
        var mediumLines = Enumerable.Range(0, 100_000).Select(i => $"Line {i:D6} with some additional text content");
        File.WriteAllLines(_mediumFilePath, mediumLines);
        _mediumMmap = MmapService.Open(_mediumFilePath).Value;

        // Large file: 1,000,000 lines (~50 MB)
        _largeFilePath = Path.Combine(Path.GetTempPath(), $"bench_large_{Guid.NewGuid()}.txt");
        var largeLines = Enumerable.Range(0, 1_000_000).Select(i => $"Line {i:D6} with some additional text content");
        File.WriteAllLines(_largeFilePath, largeLines);
        _largeMmap = MmapService.Open(_largeFilePath).Value;
    }

    [Benchmark(Description = "Small file (1K lines, ~50KB)")]
    public void IndexSmallFile()
    {
        using var indexer = RowIndexer.Build(_smallMmap).Value;
    }

    [Benchmark(Description = "Medium file (100K lines, ~5MB)")]
    public void IndexMediumFile()
    {
        using var indexer = RowIndexer.Build(_mediumMmap).Value;
    }

    [Benchmark(Description = "Large file (1M lines, ~50MB)")]
    public void IndexLargeFile()
    {
        using var indexer = RowIndexer.Build(_largeMmap).Value;
    }

    [Benchmark(Description = "Small file with 4KB chunks")]
    public void IndexSmallFileWithSmallChunks()
    {
        using var indexer = RowIndexer.Build(_smallMmap, chunkSize: 4096).Value;
    }

    [Benchmark(Description = "Medium file with 64KB chunks")]
    public void IndexMediumFileWith64KbChunks()
    {
        using var indexer = RowIndexer.Build(_mediumMmap, chunkSize: 64 * 1024).Value;
    }

    [Benchmark(Description = "Large file with 1MB chunks")]
    public void IndexLargeFileWith1MbChunks()
    {
        using var indexer = RowIndexer.Build(_largeMmap, chunkSize: 1024 * 1024).Value;
    }

    [Benchmark(Description = "Large file with 4MB chunks")]
    public void IndexLargeFileWith4MbChunks()
    {
        using var indexer = RowIndexer.Build(_largeMmap, chunkSize: 4 * 1024 * 1024).Value;
    }

    public void Dispose()
    {
        _smallMmap.Dispose();
        _mediumMmap.Dispose();
        _largeMmap.Dispose();

        if (File.Exists(_smallFilePath))
        {
            File.Delete(_smallFilePath);
        }

        if (File.Exists(_mediumFilePath))
        {
            File.Delete(_mediumFilePath);
        }

        if (File.Exists(_largeFilePath))
        {
            File.Delete(_largeFilePath);
        }

        GC.SuppressFinalize(this);
    }
}
