using AwesomeAssertions;
using DataMorph.App;
using Terminal.Gui.Drivers;

namespace DataMorph.Tests.App;

public sealed class AppKeyHandlerTests
{
    [Theory]
    [InlineData((KeyCode)'o')]
    [InlineData((KeyCode)'s')]
    [InlineData((KeyCode)'q')]
    [InlineData((KeyCode)'t')]
    [InlineData((KeyCode)'x')]
    [InlineData((KeyCode)'?')]
    public void IsGlobalShortcut_WithGlobalShortcutKeys_ReturnsTrue(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData((KeyCode)'a')]
    [InlineData((KeyCode)'b')]
    [InlineData((KeyCode)'z')]
    [InlineData((KeyCode)'1')]
    public void IsGlobalShortcut_WithNonGlobalShortcutKeys_ReturnsFalse(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData((KeyCode)'o' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'s' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'q' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'t' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'x' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'?' | KeyCode.CtrlMask)]
    public void IsGlobalShortcut_WithModifierKeys_ReturnsTrue(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        // Modifier keys are ignored - only the base character is checked
        result.Should().BeTrue();
    }
}
