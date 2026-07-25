namespace Refedle.Engine.IO.JsonObject;

/// <summary>A single top-level key/value pair from a JSON Object file, as scanned by <see cref="TopLevelScanner"/>.</summary>
public readonly record struct JsonObjectEntry(string Key, JsonRawBytes Value);
