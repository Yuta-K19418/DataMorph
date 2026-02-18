using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DataMorph.App.Views.JsonTreeNodes;

namespace DataMorph.Tests.App.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="DataMorph.App.Views.JsonTreeNodes.JsonTreeNodeHelper"/> class.
/// </summary>
public sealed class JsonTreeNodeHelperTests
{
    [Fact]
    public void CreateChildNode_WithStartObjectToken_ReturnsJsonObjectTreeNode()
    {
        // Arrange
        var json = "{\"key\": \"value\"}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<JsonObjectTreeNode>();
        node.Text.Should().Be("obj: {...}");
    }

    [Fact]
    public void CreateChildNode_WithStartArrayToken_ReturnsJsonArrayTreeNode()
    {
        // Arrange
        var json = "[1, 2, 3]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<JsonArrayTreeNode>();
        node.Text.Should().Be("arr: [...]");
    }

    [Fact]
    public void CreateChildNode_WithStringToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "\"hello\"";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to String

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "str", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("str: \"hello\"");
        valueNode.ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public void CreateChildNode_WithNumberToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "123";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to Number

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "num", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("num: 123");
        valueNode.ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public void CreateChildNode_WithTrueToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "true";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to True

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "bool", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("bool: true");
        valueNode.ValueKind.Should().Be(JsonValueKind.True);
    }

    [Fact]
    public void CreateChildNode_WithFalseToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "false";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to False

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "bool", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("bool: false");
        valueNode.ValueKind.Should().Be(JsonValueKind.False);
    }

    [Fact]
    public void CreateChildNode_WithNullToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "null";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to Null

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "nul", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("nul: <null>");
        valueNode.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void CreateChildNode_WithUnrecognizedToken_ReturnsUndefinedJsonValueTreeNode()
    {
        // Arrange
        var json = "{}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject
        reader.Read(); // Move to EndObject (unhandled in switch)

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "unk", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("unk: <unknown>");
        valueNode.ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Fact]
    public void ExtractNestedBytes_ForSimpleObject_ReturnsCorrectByteSlice()
    {
        // Arrange
        var json = "{\"a\": 1}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var slice = JsonTreeNodeHelper.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_ForSimpleArray_ReturnsCorrectByteSlice()
    {
        // Arrange
        var json = "[1, 2]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        var slice = JsonTreeNodeHelper.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_ForComplexNestedStructure_ReturnsCorrectByteSlice()
    {
        // Arrange
        var json = "{\"a\": [1, 2], \"b\": {\"c\": 3}}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var slice = JsonTreeNodeHelper.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_WithLargerContext_ReturnsOnlyTargetStructure()
    {
        // Arrange
        var fullJson = "{\"root\": {\"id\": 1, \"value\": \"test\"}, \"other\": 2}";
        var rawJson = Encoding.UTF8.GetBytes(fullJson);
        var reader = new Utf8JsonReader(rawJson);

        reader.Read(); // StartObject (root)
        reader.Read(); // PropertyName "root"
        reader.Read(); // StartObject (target nested object)

        var expectedNestedJson = "{\"id\": 1, \"value\": \"test\"}";
        var expectedBytes = Encoding.UTF8.GetBytes(expectedNestedJson);

        // Act
        var slice = JsonTreeNodeHelper.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(expectedBytes);
    }

    [Theory]
    [InlineData("Hello World", "Hello World")]
    [InlineData("Line 1\nLine 2", "Line 1\\nLine 2")]
    [InlineData("Value with\rreturn", "Value with\\rreturn")]
    [InlineData("Indented\ttext", "Indented\\ttext")]
    [InlineData("A \"quoted\" string", "A \\\"quoted\\\" string")]
    [InlineData("All\r\n\t\"special\"\r\nchars", "All\\r\\n\\t\\\"special\\\"\\r\\nchars")]
    public void EscapeString_WithVariousInputs_ReturnsCorrectlyEscapedString(
        string input,
        string expected
    )
    {
        // Arrange

        // Act
        var result = JsonTreeNodeHelper.EscapeString(input);

        // Assert
        result.Should().Be(expected);
    }
}
