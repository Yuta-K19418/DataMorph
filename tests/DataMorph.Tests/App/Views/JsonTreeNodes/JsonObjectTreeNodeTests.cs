using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DataMorph.App.Views.JsonTreeNodes;

namespace DataMorph.Tests.App.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="JsonObjectTreeNode"/> class.
/// </summary>
public sealed class JsonObjectTreeNodeTests
{
    [Fact]
    public void Constructor_WithValidJsonObject_SetsCorrectDisplayText()
    {
        // Arrange
        var json = "{\"key\": \"value\"}";
        var rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        var node = new JsonObjectTreeNode(rawJson);

        // Assert
        node.Text.Should().Be("{Object: 1 properties}");
    }

    [Fact]
    public void Constructor_WithEmptyJsonObject_SetsCorrectDisplayTextForZeroProperties()
    {
        // Arrange
        var json = "{}";
        var rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        var node = new JsonObjectTreeNode(rawJson);

        // Assert
        node.Text.Should().Be("{Object: 0 properties}");
    }

    [Fact]
    public void Constructor_WithInvalidJson_SetsInvalidObjectDisplayText()
    {
        // Arrange
        var json = "not an object";
        var rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        var node = new JsonObjectTreeNode(rawJson);

        // Assert
        node.Text.Should().Be("{Invalid Object}");
    }

    [Fact]
    public void LineNumber_WhenInitialized_StoresCorrectValue()
    {
        // Arrange
        var json = "{}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var expectedLineNumber = 42;

        // Act
        var node = new JsonObjectTreeNode(rawJson) { LineNumber = expectedLineNumber };

        // Assert
        node.LineNumber.Should().Be(expectedLineNumber);
    }

    [Fact]
    public void Children_OnFirstAccess_LoadsChildNodes()
    {
        // Arrange
        var json = "{\"a\": 1, \"b\": 2}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(2);
    }

    [Fact]
    public void Children_OnSubsequentAccess_DoesNotReloadChildNodes()
    {
        // Arrange
        var json = "{\"a\": 1}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var firstAccess = node.Children;
        var secondAccess = node.Children;

        // Assert
        firstAccess.Should().BeSameAs(secondAccess);
    }

    [Fact]
    public void Children_ForEmptyObject_ReturnsEmptyList()
    {
        // Arrange
        var json = "{}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().BeEmpty();
    }

    [Fact]
    public void Children_ForObjectWithPrimitiveProperties_CreatesCorrectValueNodes()
    {
        // Arrange
        var json = "{\"str\": \"text\", \"num\": 123, \"bool\": true, \"null\": null}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(4);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].As<JsonValueTreeNode>().Text.Should().Contain("str");
        children[0].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.String);

        children[1].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Number);
        children[2].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.True);
        children[3].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Children_ForObjectWithNestedObject_CreatesJsonObjectNode()
    {
        // Arrange
        var json = "{\"nested\": {}}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>();
    }

    [Fact]
    public void Children_ForObjectWithNestedArray_CreatesJsonArrayNode()
    {
        // Arrange
        var json = "{\"arr\": []}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>();
    }

    [Fact]
    public void Children_ForObjectWithMixedProperties_CreatesCorrectNodes()
    {
        // Arrange
        var json = "{\"a\": 1, \"b\": {}, \"c\": []}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(3);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[1].Should().BeOfType<JsonObjectTreeNode>();
        children[2].Should().BeOfType<JsonArrayTreeNode>();
    }

    [Fact]
    public void Children_ForInvalidJsonObject_ReturnsSingleErrorNode()
    {
        // Arrange
        var json = "invalid";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].As<JsonValueTreeNode>().Text.Should().Be("[Invalid JSON Object]");
        children[0].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Fact]
    public void Children_ForJsonObjectWithMissingValue_HandlesGracefully()
    {
        // Arrange
        var json = "{\"key\": null}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonObjectTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].As<JsonValueTreeNode>().Text.Should().Contain("key");
        children[0].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Null);
    }
}
