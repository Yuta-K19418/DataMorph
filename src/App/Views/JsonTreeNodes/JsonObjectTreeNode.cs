using System.Text.Json;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonTreeNodes;

/// <summary>
/// Represents a JSON object node in the tree.
/// Supports lazy loading of children on first access.
/// </summary>
internal sealed class JsonObjectTreeNode : TreeNode
{
    private readonly ReadOnlyMemory<byte> _rawJson;
    private bool _childrenLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectTreeNode"/> class.
    /// </summary>
    /// <param name="rawJson">The raw JSON bytes representing this object.</param>
    public JsonObjectTreeNode(ReadOnlyMemory<byte> rawJson)
    {
        _rawJson = rawJson;
        Text = FormatDisplayText();
    }

    /// <summary>
    /// Gets or initializes the line number of this root-level JSON object.
    /// </summary>
    public int? LineNumber { get; init; }

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

        bool isValidObjectStart;
        try
        {
            isValidObjectStart = reader.Read() && reader.TokenType == JsonTokenType.StartObject;
        }
        catch (JsonException)
        {
            isValidObjectStart = false;
        }

        if (!isValidObjectStart)
        {
            children.Add(
                new JsonValueTreeNode("[Invalid JSON Object]")
                {
                    ValueKind = JsonValueKind.Undefined,
                }
            );
            base.Children = children;
            return;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString() ?? string.Empty;

            if (!reader.Read())
            {
                break;
            }

            var childNode = JsonTreeNodeHelper.CreateChildNode(ref reader, propertyName, _rawJson);
            if (childNode is not null)
            {
                children.Add(childNode);
            }
        }

        base.Children = children;
    }

    private string FormatDisplayText()
    {
        var reader = new Utf8JsonReader(_rawJson.Span);

        bool isValidObjectStart;
        try
        {
            isValidObjectStart = reader.Read() && reader.TokenType == JsonTokenType.StartObject;
        }
        catch (JsonException)
        {
            isValidObjectStart = false;
        }

        if (!isValidObjectStart)
        {
            return "{Invalid Object}";
        }

        var propertyCount = 0;
        var depth = 1;

        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                depth++;
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
            }

            if (depth == 0)
            {
                break;
            }

            if (depth == 1 && reader.TokenType == JsonTokenType.PropertyName)
            {
                propertyCount++;
            }
        }

        return $"{{Object: {propertyCount} properties}}";
    }
}
