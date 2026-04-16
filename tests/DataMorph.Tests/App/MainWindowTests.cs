using AwesomeAssertions;
using DataMorph.App;
using DataMorph.Engine.Models.Actions;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App;

public sealed class MainWindowTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public void KeyDown_WithOKey_HandlesOpen()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.O);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithSKey_HandlesSave()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.S);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_HandlesQuit()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.Q);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQuestionMarkAndShift_HandlesHelp()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        // Simulate '?' with Shift mask (Shift + /)
        var handled = app.Keyboard.RaiseKeyDownEvent((KeyCode)'?' | KeyCode.ShiftMask);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithTKey_WhenNoFileLoaded_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.T);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithXKey_WhenNoFileLoaded_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.X);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithUnrecognizedKey_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.F12);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithHelpKey_ShowsHelpDialog()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        // '?' has implicit cast from char to Key in Terminal.Gui v2
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'?');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithOKey_WhenInputFocused_DoesNotTriggerFileOpen()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        mainWindow.CanFocus = true;
        using var textField = new TextField { CanFocus = true };
        mainWindow.Add(textField);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.Visible = true;
        mainWindow.CanFocus = true;
        textField.CanFocus = true;
        mainWindow.SubscribeKeyHandler();
        app.LayoutAndDraw();
        textField.SetFocus();
        app.LayoutAndDraw();

        var handled = app.Keyboard.RaiseKeyDownEvent(Key.O);

        // Assert
        // Global shortcut 'o' should be ignored by AppKeyHandler,
        // but the key event should be handled by TextField itself (returning true).
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_WhenUnsavedChanges_HandlesKey()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        state.ActionStack = [new RenameColumnAction { OldName = "col1", NewName = "new_col1" }];
        using var mainWindow = new MainWindow(app, state);
        // Auto-dismiss the "unsaved changes" confirmation dialog by pressing Enter (= "Yes").
        app.Iteration += (_, _) => app.Keyboard.RaiseKeyDownEvent(Key.Enter);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }
}
