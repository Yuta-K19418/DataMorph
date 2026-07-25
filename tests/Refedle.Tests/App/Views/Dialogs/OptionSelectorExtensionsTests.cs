using AwesomeAssertions;
using DataMorph.App.Views.Dialogs;
using DataMorph.Engine.Models.Actions;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App.Views.Dialogs;

public sealed class OptionSelectorExtensionsTests
{
    [Fact]
    public void EnableAutoSelectAndVimKeys_DoesNotThrow()
    {
        // Arrange
        using var selector = new OptionSelector<FilterOperator>();

        // Act
        var exception = Record.Exception(() => selector.EnableAutoSelectAndVimKeys());

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void EnableAutoSelectAndVimKeys_WithFreshSelector_DoesNotMutateValue()
    {
        // Arrange
        using var selector = new OptionSelector<FilterOperator>();

        // Act
        selector.EnableAutoSelectAndVimKeys();

        // Assert
        var initialOperator = selector.Value;
        initialOperator.Should().Be(FilterOperator.Equals);
    }
}
