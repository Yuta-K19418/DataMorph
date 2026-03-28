using DataMorph.App.Views.Dialogs;

namespace DataMorph.Tests.App.Views.Dialogs;

public sealed class FormatTimestampDialogTests
{
    [Fact]
    public void Constructor_SetsTitle_ToFormatTimestamp()
    {
        // Arrange
        var columnName = "created_at";

        // Act
        using var dialog = new FormatTimestampDialog(columnName);

        // Assert
        throw new NotImplementedException();
    }

    [Fact]
    public void TargetFormat_BeforeInteraction_IsEmptyString()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FormatTimestampDialog(columnName);

        // Assert
        throw new NotImplementedException();
    }

    [Fact]
    public void Confirmed_BeforeInteraction_IsFalse()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FormatTimestampDialog(columnName);

        // Assert
        throw new NotImplementedException();
    }
}
