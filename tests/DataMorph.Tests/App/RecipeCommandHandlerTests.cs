using DataMorph.App;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App;

public sealed class RecipeCommandHandlerTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public async Task SaveAsync_WithNonTableMode_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentMode = ViewMode.FileSelection };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);
        var handler = new RecipeCommandHandler(app, state, viewManager);

        // Act
        await handler.SaveAsync();

        // Assert
        // Should return early without showing dialog
        // (No exception should be thrown)
    }

    [Fact]
    public async Task LoadAsync_WithNoFilePath_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = string.Empty };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);
        var handler = new RecipeCommandHandler(app, state, viewManager);

        // Act
        await handler.LoadAsync();

        // Assert
        // Should return early without showing dialog
        // (No exception should be thrown)
    }
}
