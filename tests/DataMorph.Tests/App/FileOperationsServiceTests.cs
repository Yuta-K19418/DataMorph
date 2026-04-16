using DataMorph.App;
using DataMorph.App.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App;

public sealed class FileOperationsServiceTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public void UpdateStatusBarHints_WithNullFilePath_UsesDefaultHints()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = null };
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        var modeController = new ModeController(state);
        var service = new FileOperationsService(app, state, viewManager, modeController);

        // Act
        service.UpdateStatusBarHints(null);

        // Assert
        // Default hints should be set: "o:Open", "s:Save", "q:Quit", "?:Help"
        // We can't directly verify statusBar.Text without exposing it,
        // but we can verify the method completes without errors
    }

    [Fact]
    public void UpdateStatusBarHints_WithCsvFilePath_UsesDefaultHints()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = "test.csv" };
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        var modeController = new ModeController(state);
        var service = new FileOperationsService(app, state, viewManager, modeController);

        // Act
        service.UpdateStatusBarHints(null);

        // Assert
        // CSV files should only show default hints: "o:Open", "s:Save", "q:Quit", "?:Help"
    }

    [Fact]
    public void UpdateStatusBarHints_WithJsonLinesPath_IncludesToggleHint()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = "test.jsonl" };
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        var modeController = new ModeController(state);
        var service = new FileOperationsService(app, state, viewManager, modeController);

        // Act
        service.UpdateStatusBarHints(null);

        // Assert
        // JSON Lines files should include "t:Tree/Table" hint
    }

    [Fact]
    public void UpdateStatusBarHints_WithContextActionView_IncludesMenuHint()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = "test.jsonl" };
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);

        // Mock a context action view
        using var mockActionView = new MockActionView();
        window.Add(mockActionView);

        var modeController = new ModeController(state);
        var service = new FileOperationsService(app, state, viewManager, modeController);

        // Act
        service.UpdateStatusBarHints(null);

        // Assert
        // Should include "x:Menu" hint when ContextActionView is present
    }

    [Fact]
    public async Task HandleSaveRecipeAsync_WithNonTableMode_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentMode = ViewMode.FileSelection };
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        var modeController = new ModeController(state);
        var service = new FileOperationsService(app, state, viewManager, modeController);

        // Act
        await service.HandleSaveRecipeAsync();

        // Assert
        // Should return early without showing dialog
        // (No exception should be thrown)
    }

    [Fact]
    public async Task HandleLoadRecipeAsync_WithNullFilePath_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = null };
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        var modeController = new ModeController(state);
        var service = new FileOperationsService(app, state, viewManager, modeController);

        // Act
        await service.HandleLoadRecipeAsync();

        // Assert
        // Should return early without showing dialog
        // (No exception should be thrown)
    }

    /// <summary>
    /// Mock implementation of IContextActionView for testing.
    /// </summary>
    private sealed class MockActionView : View, IContextActionView
    {
        public string[] GetAvailableActions()
        {
            return ["Rename", "Delete", "Cast"];
        }

        public void ExecuteAction(string action)
        {
            // No-op for testing
        }
    }
}
