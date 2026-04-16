using AwesomeAssertions;
using DataMorph.App.Views.Dialogs;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace DataMorph.Tests.App.Views.Dialogs;

public sealed class ActionMenuDialogTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public void Constructor_WithValidActions_InitializesDialog()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = CreateTestApp();

        // Act
        using var dialog = new ActionMenuDialog(actions);

        // Assert
        dialog.Title.Should().Be("Actions");
        dialog.SelectedAction.Should().BeNull();
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithEmptyActions_InitializesDialog()
    {
        // Arrange
        string[] actions = [];
        using var app = CreateTestApp();

        // Act
        using var dialog = new ActionMenuDialog(actions);

        // Assert
        dialog.Title.Should().Be("Actions");
        dialog.SelectedAction.Should().BeNull();
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithSingleAction_InitializesDialog()
    {
        // Arrange
        string[] actions = ["Rename"];
        using var app = CreateTestApp();

        // Act
        using var dialog = new ActionMenuDialog(actions);

        // Assert
        dialog.Title.Should().Be("Actions");
        dialog.SelectedAction.Should().BeNull();
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullActions_ThrowsArgumentException()
    {
        // Arrange
        string[]? actions = null;
        using var app = CreateTestApp();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new ActionMenuDialog(actions!));

        // Assert
        exception.ParamName.Should().Be("availableActions");
    }

    [Fact]
    public void SelectedAction_WhenConfirmed_ReturnsFirstAction()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions);
        app.Iteration += (_, _) => app.Keyboard.RaiseKeyDownEvent(Key.Enter);

        // Act
        app.Run(dialog);

        // Assert
        dialog.SelectedAction.Should().Be("Rename");
        dialog.Confirmed.Should().BeTrue();
    }

    [Fact]
    public void HandleMenuNavigation_WithEscKey_ClosesDialogWithoutConfirm()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions);

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.Esc);

        // Assert
        handled.Should().BeTrue();
        dialog.SelectedAction.Should().BeNull();
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithXKey_ClosesDialogWithoutConfirm()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions);

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.X);

        // Assert
        handled.Should().BeTrue();
        dialog.SelectedAction.Should().BeNull();
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithJKey_MovesSelectionDown()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions);

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.J);

        // Assert
        handled.Should().BeTrue();
        dialog.SelectedItemIndex.Should().Be(1);
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithKKey_MovesSelectionUp()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions);
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.J);

        // Act
        var handled = dialog.NewKeyDownEvent(Key.K);

        // Assert
        handled.Should().BeTrue();
        dialog.SelectedItemIndex.Should().Be(0);
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void SelectedAction_WhenNotConfirmed_ReturnsNull()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions);

        // Act
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.Esc);

        // Assert
        dialog.SelectedAction.Should().BeNull();
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithKKey_AtFirstItem_StaysAtFirst()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions);

        // Act
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.K);

        // Assert
        dialog.SelectedItemIndex.Should().Be(0);
        dialog.Confirmed.Should().BeFalse();
    }
}
