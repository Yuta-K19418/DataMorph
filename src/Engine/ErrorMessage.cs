namespace DataMorph.Engine;

/// <summary>
/// Represents an error message in a Result type.
/// This type exists to avoid constructor ambiguity when T is string.
/// </summary>
public readonly record struct ErrorMessage
{
    private readonly string? _value;

    /// <summary>
    /// Gets the error message text.
    /// Returns empty string for default(ErrorMessage), otherwise returns the validated error message.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorMessage"/> struct.
    /// </summary>
    /// <param name="value">The error message text.</param>
    /// <exception cref="ArgumentException">Thrown when value is null, empty, or whitespace.</exception>
    public ErrorMessage(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value;
    }

    /// <summary>
    /// Creates an ErrorMessage from a string.
    /// </summary>
    /// <param name="value">The error message text.</param>
    /// <returns>An ErrorMessage containing the provided text.</returns>
    public static ErrorMessage FromString(string value) => new(value);

    /// <summary>
    /// Returns the error message text.
    /// </summary>
    /// <returns>The error message text.</returns>
    public override string ToString() => Value;
}
