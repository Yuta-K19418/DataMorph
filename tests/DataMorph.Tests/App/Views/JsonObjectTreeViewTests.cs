using System.Text;
using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.App.Views.JsonTreeNodes;
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
        Assert.NotNull(_app.Driver);
        _app.Driver.SetScreenSize(80, 25);
    }

    public void Dispose() => _app.Dispose();

    private static JsonRawBytes ToBytes(string json) =>
        Encoding.UTF8.GetBytes(json);

    // --- CreateKeyNode tests ---

    [Theory]
    [InlineData("id", "123")]
    [InlineData("name", "\"x\"")]
    [InlineData("ok", "true")]
    [InlineData("ok", "false")]
    public void CreateKeyNode_PrimitiveValue_ReturnsValueNodeWithLabel(string key, string json)
    {
        // Arrange
        var valueBytes = ToBytes(json);

        // Act
        var node = JsonObjectTreeView.CreateKeyNode(key, valueBytes);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().StartWith($"{key}: ");
    }

    [Fact]
    public void CreateKeyNode_NullValue_ReturnsValueNodeWithLabel()
    {
        // Arrange
        var valueBytes = ToBytes("null");

        // Act
        var node = JsonObjectTreeView.CreateKeyNode("k", valueBytes);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Be("k: <null>");
    }

    [Theory]
    [InlineData("k", "not-json")]
    [InlineData("k", "")]
    public void CreateKeyNode_InvalidInput_ReturnsValueNodeWithErrorText(string key, string rawValue)
    {
        // Arrange
        var valueBytes = ToBytes(rawValue);

        // Act
        var node = JsonObjectTreeView.CreateKeyNode(key, valueBytes);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Be($"{key}: [Invalid JSON]");
    }

    [Fact]
    public void CreateKeyNode_ObjectValue_ReturnsObjectNodeWithLabel()
    {
        // Arrange
        var valueBytes = ToBytes("{\"a\":1}");

        // Act
        var node = JsonObjectTreeView.CreateKeyNode("data", valueBytes);

        // Assert
        node.Should().BeOfType<JsonObjectTreeNode>();
        node.Text.Should().Be("data: {Object: 1 properties}");
    }

    [Fact]
    public void CreateKeyNode_ArrayValue_ReturnsArrayNodeWithLabel()
    {
        // Arrange
        var valueBytes = ToBytes("[1,2]");

        // Act
        var node = JsonObjectTreeView.CreateKeyNode("tags", valueBytes);

        // Assert
        node.Should().BeOfType<JsonArrayTreeNode>();
        node.Text.Should().Be("tags: [Array: 2 items]");
    }

    // --- Create tests ---

    [Fact]
    public void Create_WithNullEntries_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<(string key, JsonRawBytes value)> nullEntries = null!;

        // Act
        var act = () => JsonObjectTreeView.Create(nullEntries, () => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullToggle_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<(string key, JsonRawBytes value)> entries = [];

        // Act
        var act = () => JsonObjectTreeView.Create(entries, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithEmptyEntries_AddsNoObjects()
    {
        // Arrange
        IReadOnlyList<(string key, JsonRawBytes value)> entries = [];

        // Act
        using var view = JsonObjectTreeView.Create(entries, () => { });

        // Assert
        view.Objects.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEntries_AddsOneNodePerKey()
    {
        // Arrange
        IReadOnlyList<(string key, JsonRawBytes value)> entries =
        [
            ("id", ToBytes("1")),
            ("name", ToBytes("\"Alice\"")),
            ("active", ToBytes("true")),
        ];

        // Act
        using var view = JsonObjectTreeView.Create(entries, () => { });

        // Assert
        view.Objects.Should().HaveCount(3);
    }
}
