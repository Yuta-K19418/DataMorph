using AwesomeAssertions;
using DataMorph.App;
using DataMorph.Engine.IO;
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
    public void Dispose_WithoutPriorStart_DoesNotThrow()
    {
        // Arrange
        // CA2000: manager is disposed via act() below; suppress false positive.
#pragma warning disable CA2000
        var manager = new IndexTaskManager();
#pragma warning restore CA2000

        // Act
        var act = () => manager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Start_CalledTwice_CancelsPreviousTask()
    {
        // Arrange
        // Use BlockingIndexer for indexer1 to guarantee it is still running when
        // indexer2 starts, making the cancellation behaviour deterministic.
        var indexer1 = new BlockingIndexer();
        var firstCheckpoint1 = new TaskCompletionSource<bool>();
        var completed1 = new TaskCompletionSource<bool>();
        indexer1.FirstCheckpointReached += () => firstCheckpoint1.TrySetResult(true);
        indexer1.BuildIndexCompleted += () => completed1.TrySetResult(true);

        var lines = Enumerable.Range(0, 500_000).Select(i => $"{{\"id\":{i}}}").ToList();
        await File.WriteAllLinesAsync(_testFilePath, lines);

        var indexer2 = new RowIndexer(_testFilePath);
        var completed2 = new TaskCompletionSource<bool>();
        indexer2.BuildIndexCompleted += () => completed2.TrySetResult(true);

        using var manager = new IndexTaskManager();

        // Act
        manager.Start(indexer1);

        // Wait until indexer1 is blocked inside BuildIndex (WaitOne), ensuring it
        // is still running when we start indexer2.
        await firstCheckpoint1.Task.WaitAsync(TimeSpan.FromSeconds(5));

        manager.Start(indexer2);

        // Assert
        await completed1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await completed2.Task.WaitAsync(TimeSpan.FromSeconds(5));

        indexer1.WasCancelled.Should().BeTrue();
        indexer2.TotalRows.Should().Be(500_000);
    }

    [Fact]
    public async Task Dispose_CancelsIndexing()
    {
        // Arrange
        // Use a controllable fake that blocks until cancelled, making the test
        // deterministic regardless of machine speed.
        var indexer = new BlockingIndexer();
        var firstCheckpoint = new TaskCompletionSource<bool>();
        var completed = new TaskCompletionSource<bool>();
        indexer.FirstCheckpointReached += () => firstCheckpoint.TrySetResult(true);
        indexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        var manager = new IndexTaskManager();

        // Act
        manager.Start(indexer);

        // Wait until the indexer is blocked inside BuildIndex (WaitOne)
        await firstCheckpoint.Task.WaitAsync(TimeSpan.FromSeconds(5));

        manager.Dispose();

        // Assert
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        indexer.WasCancelled.Should().BeTrue();
    }

    /// <summary>
    /// A fake indexer that fires FirstCheckpointReached immediately, then blocks
    /// until the CancellationToken is cancelled. This makes cancellation tests fully
    /// deterministic regardless of machine speed.
    /// </summary>
    private sealed class BlockingIndexer : IRowIndexer
    {
        public bool WasCancelled { get; private set; }

#pragma warning disable CA1003
        public event Action? FirstCheckpointReached;
#pragma warning disable CS0067
        public event Action<long, long>? ProgressChanged;
#pragma warning restore CS0067
        public event Action? BuildIndexCompleted;
#pragma warning restore CA1003

        public long BytesRead => 0;
        public long FileSize => 0;
        public long TotalRows => 0;
        public string FilePath => string.Empty;

        public void BuildIndex(CancellationToken ct = default)
        {
            FirstCheckpointReached?.Invoke();
            while (!ct.IsCancellationRequested)
            {
                Thread.Sleep(1);
            }
            WasCancelled = true;
            BuildIndexCompleted?.Invoke();
        }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }
}
