using AwesomeAssertions;
using DataMorph.App;
using DataMorph.App.Views;
using DataMorph.Engine.IO;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App;

public sealed class FileDialogHandlerTests : IDisposable
{
    private readonly string _testFile;

    public FileDialogHandlerTests()
    {
        _testFile = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        // Add enough data to ensure we can control the flow
        File.WriteAllText(_testFile, "{\"id\":1}\n");
    }

    public void Dispose()
    {
        if (File.Exists(_testFile))
        {
            File.Delete(_testFile);
        }
    }

    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);

        // Act
        Action act = () =>
        {
            _ = new FileDialogHandler(app, state, viewManager, _ => { });
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonLinesFile_SwitchesToTreeViewAfterFirstCheckpoint()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);

        IRowIndexer? capturedIndexer = null;
        var handler = new FileDialogHandler(app, state, viewManager, indexer =>
        {
            capturedIndexer = indexer;
            // Simulate indexing start
            Task.Run(() => indexer.BuildIndex());
        });

        // Act
        await handler.HandleFileSelectedAsync(_testFile);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        viewManager.GetCurrentView().Should().BeOfType<JsonLinesTreeView>();
        Assert.NotNull(capturedIndexer);
        capturedIndexer.TotalRows.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonLinesFileBeforeFirstCheckpoint_DoesNotSwitchToTreeView()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);
        viewManager.SwitchToFileSelection(); // Ensure initial view is not null

        var tcs = new TaskCompletionSource();
        var handler = new FileDialogHandler(app, state, viewManager, _ =>
        {
            // Do NOT start indexing yet, so FirstCheckpointReached won't fire
            tcs.TrySetResult();
        });

        // Act
        var handleTask = handler.HandleFileSelectedAsync(_testFile);
        await tcs.Task; // Wait until _onIndexerStart is called

        // Assert
        state.CurrentMode.Should().NotBe(ViewMode.JsonLinesTree);
        viewManager.GetCurrentView().Should().NotBeOfType<JsonLinesTreeView>();

        // Cleanup: actually start indexing to let the task complete
        Assert.NotNull(state.RowIndexer);
        state.RowIndexer.BuildIndex();
        await handleTask;
    }
}
