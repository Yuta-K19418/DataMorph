using AwesomeAssertions;
using DataMorph.App;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.App;

public sealed class AppStateTests
{
    [Fact]
    public void AddMorphAction_SingleAction_AddsToStack()
    {
        // Arrange
        using var state = new AppState();
        var action = new RenameColumnAction { OldName = "foo", NewName = "bar" };

        // Act
        state.AddMorphAction(action);

        // Assert
        state.ActionStack.Should().ContainSingle();
        state.ActionStack[0].Should().Be(action);
    }

    [Fact]
    public void AddMorphAction_MultipleActions_PreservesOrder()
    {
        // Arrange
        using var state = new AppState();
        var action1 = new RenameColumnAction { OldName = "a", NewName = "b" };
        var action2 = new DeleteColumnAction { ColumnName = "c" };
        var action3 = new CastColumnAction { ColumnName = "d", TargetType = ColumnType.WholeNumber };

        // Act
        state.AddMorphAction(action1);
        state.AddMorphAction(action2);
        state.AddMorphAction(action3);

        // Assert
        state.ActionStack.Should().HaveCount(3);
        state.ActionStack[0].Should().Be(action1);
        state.ActionStack[1].Should().Be(action2);
        state.ActionStack[2].Should().Be(action3);
    }

    [Fact]
    public void AddMorphAction_DoesNotMutateOriginalList()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        var originalList = state.ActionStack;

        // Act
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "c" });

        // Assert
        originalList.Should().ContainSingle();
        state.ActionStack.Should().HaveCount(2);
    }

    [Fact]
    public void ClearMorphActions_WithActions_ClearsActionStack()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ClearMorphActions_WithEmptyStack_StackRemainsEmpty()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ClearMorphActions_DoesNotMutatePreviousStackReference()
    {
        // Arrange

        // Act

        // Assert
    }
}
