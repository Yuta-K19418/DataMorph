using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace DataMorph.Tests.App.Views;

public sealed class JsonObjectTreeViewTests : IDisposable
{
    private readonly IApplication _app;

    public JsonObjectTreeViewTests()
    {
        _app = Application.Create();
        _app.Init(DriverRegistry.Names.ANSI);
    }

    public void Dispose() => _app.Dispose();

    // --- CreateKeyNode tests ---

#pragma warning disable xUnit1026
    [Theory]
    [InlineData("id", "123")]
    [InlineData("name", "\"x\"")]
    [InlineData("ok", "true")]
    [InlineData("ok", "false")]
    [InlineData("k", "null")]
    public void CreateKeyNode_PrimitiveValue_ReturnsValueNodeWithLabel(string key, string json)
    {
        // Arrange

        // Act

        // Assert
    }

    [Theory]
    [InlineData("k", "not-json")]
    [InlineData("k", "")]
    public void CreateKeyNode_InvalidInput_ReturnsValueNodeWithErrorText(string key, string rawValue)
    {
        // Arrange

        // Act

        // Assert
    }
#pragma warning restore xUnit1026

    [Fact]
    public void CreateKeyNode_ObjectValue_ReturnsObjectNodeWithLabel()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void CreateKeyNode_ArrayValue_ReturnsArrayNodeWithLabel()
    {
        // Arrange

        // Act

        // Assert
    }

    // --- Create tests ---

    [Fact]
    public void Create_WithNullEntries_ThrowsArgumentNullException()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Create_WithNullToggle_ThrowsArgumentNullException()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Create_WithEmptyEntries_AddsNoObjects()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Create_WithEntries_AddsOneNodePerKey()
    {
        // Arrange

        // Act

        // Assert
    }
}
