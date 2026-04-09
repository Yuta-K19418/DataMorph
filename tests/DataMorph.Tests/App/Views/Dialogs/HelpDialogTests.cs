using AwesomeAssertions;
using DataMorph.App.Views.Dialogs;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace DataMorph.Tests.App.Views.Dialogs;

public sealed class HelpDialogTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public void OnKeyDown_WithEscKey_CallsRequestStop()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.Esc);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithQKey_CallsRequestStop()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.Q);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithLowercaseQKey_CallsRequestStop()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithQuestionMarkKey_CallsRequestStop()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        // '?' has implicit cast from char to Key in Terminal.Gui v2
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent((Key)'?');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_InitializesDialogCorrectly()
    {
        // Arrange
        using var app = CreateTestApp();

        // Act
        using var dialog = new HelpDialog();

        // Assert
        dialog.Title.Should().Be("Help - Key Bindings");
    }

    [Fact]
    public void OnKeyDown_WithOtherKey_DoesNotCallRequestStop()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.A);

        // Assert
        handled.Should().BeFalse();
    }
}
