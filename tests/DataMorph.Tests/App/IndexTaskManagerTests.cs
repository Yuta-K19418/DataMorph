using AwesomeAssertions;
using DataMorph.App;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.App;

public sealed class IndexTaskManagerTests : IDisposable
{
    private readonly string _testFilePath;

    public IndexTaskManagerTests()
    {
        _testFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task Start_StartsIndexing()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "{\"id\":1}\n{\"id\":2}");
        var indexer = new RowIndexer(_testFilePath);
        using var manager = new IndexTaskManager();
        var tcs = new TaskCompletionSource<bool>();
        indexer.BuildIndexCompleted += () => tcs.TrySetResult(true);

        // Act
        manager.Start(indexer);

        // Assert
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        indexer.TotalRows.Should().Be(2);
    }

    [Fact]
    public void Start_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new IndexTaskManager();

        // Act
        var act = () => manager.Start(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var manager = new IndexTaskManager();
        manager.Dispose();

        // Act
        var act = () => manager.Start(new RowIndexer(_testFilePath));

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var manager = new IndexTaskManager();

        // Act
        manager.Dispose();
        var act = () => manager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Start_CalledTwice_CancelsPreviousTask()
    {
        // Arrange
        // 500k lines should be enough to ensure it doesn't finish instantly
        // and gives enough time for cancellation to be processed.
        var lines = Enumerable.Range(0, 500_000).Select(i => $"{{\"id\":{i}}}").ToList();
        await File.WriteAllLinesAsync(_testFilePath, lines);

        var indexer1 = new RowIndexer(_testFilePath);
        var firstCheckpoint1 = new TaskCompletionSource<bool>();
        var completed1 = new TaskCompletionSource<bool>();
        indexer1.FirstCheckpointReached += () => firstCheckpoint1.TrySetResult(true);
        indexer1.BuildIndexCompleted += () => completed1.TrySetResult(true);

        var indexer2 = new RowIndexer(_testFilePath);
        var completed2 = new TaskCompletionSource<bool>();
        indexer2.BuildIndexCompleted += () => completed2.TrySetResult(true);

        using var manager = new IndexTaskManager();

        // Act
        manager.Start(indexer1);

        // Wait for it to actually start working and reach first checkpoint (1000 rows)
        await firstCheckpoint1.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Give it a tiny bit more time to process a few more checkpoints
        await Task.Delay(10);

        manager.Start(indexer2);

        // Assert
        await completed1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await completed2.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // indexer1 should have been cancelled before finishing all 500k rows
        indexer1.TotalRows.Should().BeLessThan(500_000);
        indexer2.TotalRows.Should().Be(500_000);
    }

    [Fact]
    public async Task Dispose_CancelsIndexing()
    {
        // Arrange
        var lines = Enumerable.Range(0, 500_000).Select(i => $"{{\"id\":{i}}}").ToList();
        await File.WriteAllLinesAsync(_testFilePath, lines);

        var indexer = new RowIndexer(_testFilePath);
        var firstCheckpoint = new TaskCompletionSource<bool>();
        var completed = new TaskCompletionSource<bool>();
        indexer.FirstCheckpointReached += () => firstCheckpoint.TrySetResult(true);
        indexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        var manager = new IndexTaskManager();

        // Act
        manager.Start(indexer);

        // Wait for it to start working
        await firstCheckpoint.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Give it a tiny bit more time
        await Task.Delay(10);

        manager.Dispose();

        // Assert
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // indexer should have been cancelled before finishing all 500k rows
        indexer.TotalRows.Should().BeLessThan(500_000);
    }
}
