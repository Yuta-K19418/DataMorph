using System.Globalization;
using System.Text;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;

namespace DataMorph.Engine.Recipes;

/// <summary>
/// Parses YAML text into <see cref="Recipe"/> objects.
/// AOT-safe: no reflection is used.
/// </summary>
internal sealed class RecipeYamlParser
{
    private sealed record RootParseState(string Name, string? Description, DateTimeOffset? LastModified, ParseState ParseState);

    /// <summary>
    /// Parses a YAML string into a recipe.
    /// Returns a failure result for any parse or validation error.
    /// </summary>
    public static Result<Recipe> Parse(string yaml)
    {
        var rootState = new RootParseState(string.Empty, null, null, ParseState.Root);
        List<MorphAction> actions = [];
        Dictionary<string, string> currentAction = [];

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (IsSkippable(line))
            {
                continue;
            }

            if (rootState.ParseState == ParseState.Root)
            {
                var result = ProcessRootLine(line, rootState);
                if (result.IsFailure)
                {
                    return Results.Failure<Recipe>(result.Error);
                }

                rootState = result.Value;
                continue;
            }

            if (line.StartsWith("  - type: ", StringComparison.Ordinal))
            {
                var startResult = StartNewAction(line, currentAction);
                if (startResult.IsFailure)
                {
                    return Results.Failure<Recipe>(startResult.Error);
                }

                var (newCurrentAction, completedAction) = startResult.Value;
                currentAction = newCurrentAction;
                rootState = rootState with { ParseState = ParseState.ActionItem };
                if (completedAction is not null)
                {
                    actions.Add(completedAction);
                }

                continue;
            }

            if (rootState.ParseState != ParseState.ActionItem || !line.StartsWith("    ", StringComparison.Ordinal))
            {
                return Results.Failure<Recipe>($"Unexpected line in actions context: '{line}'");
            }

            var fieldResult = ParseActionField(line);
            if (fieldResult.IsFailure)
            {
                return Results.Failure<Recipe>(fieldResult.Error);
            }

            var (fieldKey, fieldValue) = fieldResult.Value;
            currentAction[fieldKey] = fieldValue;
        }

        if (currentAction.ContainsKey("type"))
        {
            var buildResult = MorphActionParser.ParseAction(currentAction);
            if (buildResult.IsFailure)
            {
                return Results.Failure<Recipe>(buildResult.Error);
            }

            actions.Add(buildResult.Value);
        }

        return string.IsNullOrEmpty(rootState.Name)
            ? Results.Failure<Recipe>("Missing required field: 'name'")
            : Results.Success(new Recipe
            {
                Name = rootState.Name,
                Description = rootState.Description,
                LastModified = rootState.LastModified,
                Actions = actions.AsReadOnly(),
            });
    }

    private static bool IsSkippable(string line)
        => string.IsNullOrWhiteSpace(line) || line.AsSpan().TrimStart().StartsWith("#", StringComparison.Ordinal);

    private static Result<RootParseState> ProcessRootLine(string line, RootParseState state)
    {
        if (line == "actions: []")
        {
            return Results.Success(state);
        }

        if (line == "actions:")
        {
            return Results.Success(state with { ParseState = ParseState.Actions });
        }

        var colonIdx = line.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            return Results.Failure<RootParseState>($"Malformed root-level line: '{line}'");
        }

        var key = line[..colonIdx];
        var value = line[(colonIdx + 2)..];

        return key switch
        {
            "name" => !string.IsNullOrEmpty(state.Name)
                ? Results.Failure<RootParseState>("Duplicate root-level key: 'name'")
                : Results.Success(state with { Name = UnquoteString(value) }),
            "description" => state.Description is not null
                ? Results.Failure<RootParseState>("Duplicate root-level key: 'description'")
                : Results.Success(state with { Description = UnquoteString(value) }),
            "lastModified" => state.LastModified is not null
                ? Results.Failure<RootParseState>("Duplicate root-level key: 'lastModified'")
                : ParseLastModifiedField(value, state),
            _ => Results.Failure<RootParseState>($"Unknown root-level key: '{key}'"),
        };
    }

    private static Result<RootParseState> ParseLastModifiedField(string value, RootParseState state)
    {
        var parseResult = TryParseLastModified(value);
        if (parseResult.IsFailure)
        {
            return Results.Failure<RootParseState>(parseResult.Error);
        }

        return Results.Success(state with { LastModified = parseResult.Value });
    }

    private static Result<DateTimeOffset> TryParseLastModified(string value)
    {
        if (!DateTimeOffset.TryParse(UnquoteString(value), null, DateTimeStyles.RoundtripKind, out var dt))
        {
            return Results.Failure<DateTimeOffset>($"Invalid lastModified value: '{value}'");
        }

        return Results.Success(dt);
    }

    private static Result<(Dictionary<string, string> newAction, MorphAction? completedAction)> StartNewAction(
        string line,
        Dictionary<string, string> currentAction)
    {
        MorphAction? completedAction = null;
        if (currentAction.ContainsKey("type"))
        {
            var parseResult = MorphActionParser.ParseAction(currentAction);
            if (parseResult.IsFailure)
            {
                return Results.Failure<(Dictionary<string, string> newAction, MorphAction? completedAction)>(parseResult.Error);
            }

            completedAction = parseResult.Value;
        }

        var newAction = new Dictionary<string, string> { ["type"] = line["  - type: ".Length..] };
        return Results.Success<(Dictionary<string, string> newAction, MorphAction? completedAction)>((newAction, completedAction));
    }

    private static Result<(string key, string value)> ParseActionField(string line)
    {
        var fieldContent = line[4..];
        var colonIdx = fieldContent.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            return Results.Failure<(string key, string value)>($"Malformed action field: '{line}'");
        }

        return Results.Success((fieldContent[..colonIdx], UnquoteString(fieldContent[(colonIdx + 2)..])));
    }

    private static string UnquoteString(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            var inner = value.AsSpan(1, value.Length - 2);
            if (inner.IndexOf('\\') < 0)
            {
                return inner.ToString();
            }

            var sb = new StringBuilder(inner.Length);
            for (var i = 0; i < inner.Length; i++)
            {
                if (inner[i] == '\\' && i + 1 < inner.Length)
                {
                    sb.Append(inner[i + 1] switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        var c => c,
                    });
                    i++;
                    continue;
                }

                sb.Append(inner[i]);
            }

            return sb.ToString();
        }

        return value;
    }
}
