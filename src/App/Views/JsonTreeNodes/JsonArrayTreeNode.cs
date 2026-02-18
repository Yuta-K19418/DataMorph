using System.Text.Json;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonTreeNodes;

/// <summary>
/// Represents a JSON array node in the tree.
/// Supports lazy loading of children on first access.
/// </summary>
internal sealed class JsonArrayTreeNode : TreeNode
{
    private readonly ReadOnlyMemory<byte> _rawJson;
    private bool _childrenLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArrayTreeNode"/> class.
    /// </summary>
    /// <param name="rawJson">The raw JSON bytes representing this array.</param>
    public JsonArrayTreeNode(ReadOnlyMemory<byte> rawJson)
    {
        _rawJson = rawJson;
        Text = FormatDisplayText();
    }

    /// <inheritdoc/>
    public override IList<ITreeNode> Children
    {
        get
        {
            if (!_childrenLoaded)
            {
                LoadChildren();
                _childrenLoaded = true;
            }

            return base.Children;
        }
        set => base.Children = value;
    }

    private void LoadChildren()
    {
        List<ITreeNode> children = [];

        var reader = new Utf8JsonReader(_rawJson.Span);

        bool isValidArrayStart;
        try
        {
            isValidArrayStart = reader.Read() && reader.TokenType == JsonTokenType.StartArray;
        }
        catch (JsonException)
        {
            isValidArrayStart = false;
        }

        if (!isValidArrayStart)
        {
            children.Add(
                new JsonValueTreeNode("[Invalid JSON Array]")
                {
                    ValueKind = JsonValueKind.Undefined,
                }
            );
            base.Children = children;
            return;
        }

        var elementIndex = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            var elementNode = JsonTreeNodeHelper.CreateChildNode(
                ref reader,
                $"[{elementIndex}]",
                _rawJson
            );
            if (elementNode is not null)
            {
                children.Add(elementNode);
            }
            elementIndex++;
        }

        base.Children = children;
    }

    private string FormatDisplayText()
    {
        var reader = new Utf8JsonReader(_rawJson.Span);

        bool isValidArrayStart;
        try
        {
            isValidArrayStart = reader.Read() && reader.TokenType == JsonTokenType.StartArray;
        }
        catch (JsonException)
        {
            isValidArrayStart = false;
        }

        if (!isValidArrayStart)
        {
            return "[Invalid Array]";
        }

        var elementCount = 0;
        var depth = 1;

        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
            {
                depth++;
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject)
            {
                depth--;
            }

            if (depth == 0)
            {
                break;
            }

            if (depth == 1)
            {
                elementCount++;
            }
        }

        return $"[Array: {elementCount} items]";
    }
}
