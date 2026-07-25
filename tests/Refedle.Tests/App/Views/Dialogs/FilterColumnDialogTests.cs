using AwesomeAssertions;
using DataMorph.App.Views.Dialogs;

namespace DataMorph.Tests.App.Views.Dialogs;

public sealed class FilterColumnDialogTests
{
    [Fact]
    public void Constructor_SetsTitle_ToFilterColumn()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.Title.Should().Be("Filter Column");
    }

    [Fact]
    public void SelectedOperator_BeforeInteraction_IsNull()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.SelectedOperator.Should().BeNull();
    }

    [Fact]
    public void Value_BeforeInteraction_IsNull()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.Value.Should().BeNull();
    }

    [Fact]
    public void Confirmed_BeforeInteraction_IsFalse()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.Confirmed.Should().BeFalse();
    }
}
