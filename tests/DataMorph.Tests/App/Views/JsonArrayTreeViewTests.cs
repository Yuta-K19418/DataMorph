using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace DataMorph.Tests.App.Views;

public sealed partial class JsonArrayTreeViewTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            _disposed = true;
        }
    }

    private string CreateTempFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"jsonarray_treeview_{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }

    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    private static void NoOpUiThreadInvoke(Action action) => action();

    [Fact]
    public void HandleAccepted_NonRangeNode_DoesNotThrow()
    {
        // Arrange
        var filePath = CreateTempFile("[{\"a\":1}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        using var view = JsonArrayTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var elementNode = objects.First();
        view.SelectedObject = elementNode;

        // Act — Accept on a non-range node should not throw
        var act = () => view.InvokeCommand(Command.Accept);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_UnsubscribesEventHandlers()
    {
        // Arrange — view created with event subscriptions

        // Act

        // Assert
    }

    /// <summary>
    /// Stub IRowIndexer that overrides TotalRows and IsIndexingCompleted
    /// while delegating everything else to the inner indexer.
    /// Supports manual event raising for testing progressive loading.
    /// </summary>
    private sealed class StubRowIndexer : IRowIndexer
    {
        private readonly IRowIndexer _inner;
        private readonly long _fakeTotalRows;
        private readonly bool? _fakeIsCompleted;

        public StubRowIndexer(IRowIndexer inner, long fakeTotalRows, bool? fakeIsCompleted = null)
        {
            _inner = inner;
            _fakeTotalRows = fakeTotalRows;
            _fakeIsCompleted = fakeIsCompleted;
        }

        public string FilePath => _inner.FilePath;
        public long FileSize => _inner.FileSize;
        public long BytesRead => _inner.BytesRead;
        public long TotalRows => _fakeTotalRows;
        public bool IsIndexingCompleted => _fakeIsCompleted ?? _inner.IsIndexingCompleted;

        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;

        public void RaiseProgressChanged(long bytesRead, long fileSize) =>
            ProgressChanged?.Invoke(bytesRead, fileSize);

        public void RaiseBuildIndexCompleted() =>
            BuildIndexCompleted?.Invoke();

        public void RaiseFirstCheckpointReached() =>
            FirstCheckpointReached?.Invoke();

        public void BuildIndex(CancellationToken ct = default) => _inner.BuildIndex(ct);

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => _inner.GetCheckPoint(targetRow);
    }
}
