using System.Text;
using DataMorph.Engine.Models;

namespace DataMorph.Engine.Recipes;

/// <summary>
/// Persists and restores <see cref="Recipe"/> objects as human-readable YAML files.
/// </summary>
public sealed class RecipeManager : IRecipeManager
{
    /// <inheritdoc/>
    public async ValueTask<Result<Recipe>> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return Results.Failure<Recipe>($"File not found: {filePath}");
        }

        try
        {
            var yaml = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct).ConfigureAwait(false);
            return RecipeYamlParser.Parse(yaml);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            return Results.Failure<Recipe>(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result> SaveAsync(Recipe recipe, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var yaml = RecipeYamlSerializer.Serialize(recipe);

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                yaml,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                ct
            ).ConfigureAwait(false);
            return Results.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            return Results.Failure(ex.Message);
        }
    }
}
