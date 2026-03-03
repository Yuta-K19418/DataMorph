using DataMorph.Engine.Models;

namespace DataMorph.Engine.Recipes;

/// <summary>
/// Defines the contract for loading and saving recipes to disk.
/// </summary>
public interface IRecipeManager
{
    /// <summary>
    /// Loads a recipe from a YAML file.
    /// </summary>
    ValueTask<Result<Recipe>> LoadAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Saves a recipe to a YAML file.
    /// Overwrites the file if it already exists.
    /// </summary>
    ValueTask<Result> SaveAsync(Recipe recipe, string filePath, CancellationToken ct = default);
}
