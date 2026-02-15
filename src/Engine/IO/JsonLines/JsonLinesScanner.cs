using System.Buffers;

namespace DataMorph.Engine.IO.JsonLines;

/// <summary>
/// Provides efficient scanning for JSON Lines format, taking into account JSON escaping rules.
///
/// This implementation uses a custom state machine instead of Utf8JsonReader for the following reasons:
///
/// 1. Performance Optimization:
///    - Utf8JsonReader performs full JSON parsing (tokenization, validation, structure analysis) which is unnecessary for line detection.
///    - Our custom scanner focuses solely on finding newline positions with minimal operations per byte.
///
/// 2. State Management Efficiency:
///    - Utf8JsonReader requires preserving a large state structure (JsonReaderState) across buffer boundaries.
///    - Our scanner maintains only two boolean flags (inQuotes, escaped), minimizing state preservation overhead.
///
/// 3. Performance Characteristics:
///    - Utf8JsonReader performs full JSON tokenization which is unnecessary for line detection.
///    - Our scanner uses a highly optimized search for newline and quote characters with minimal per-byte processing.
///
/// 4. Separation of Concerns:
///    - Line detection is logically separate from JSON validation.
///    - This scanner only identifies line boundaries; validation is handled later by Utf8JsonReader.
///
/// 5. Algorithmic Efficiency:
///    - We use vectorized SearchValues for initial byte scanning and minimal branching in hot paths.
///    - The state machine is optimized for the common case (no escapes) while handling edge cases correctly.
/// </summary>
public ref struct JsonLinesScanner
{
    private static readonly SearchValues<byte> _newlineAndQuote = SearchValues.Create("\n\""u8);

    private bool _inQuotes;
    private bool _escaped;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLinesScanner"/> struct.
    /// The scanner starts in a non-quoted state with no escape sequences.
    /// </summary>
    public JsonLinesScanner()
    {
        _inQuotes = false;
        _escaped = false;
    }

    /// <summary>
    /// Finds the next JSON Line within the provided span.
    /// </summary>
    /// <param name="span">The span to search within.</param>
    /// <returns>
    /// A tuple (lineCompleted, bytesConsumed).
    /// - lineCompleted: true if a complete line (ending with an unescaped newline) was found
    /// - bytesConsumed: number of bytes consumed from the span (including the newline if found)
    /// Returns (false, 0) if the span is empty.
    /// </returns>
    public (bool lineCompleted, int bytesConsumed) FindNextLineLength(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
        {
            // For an empty span, returning (false,0) is the correct sentinel value
            return (false, 0);
        }

        var start = 0;

        while (start < span.Length)
        {
            // Find the next newline or quote character efficiently
            var slice = span[start..];
            var index = slice.IndexOfAny(_newlineAndQuote);
            if (index == -1)
            {
                // No more newlines or quotes in the remaining span
                // Update state for all remaining characters
                UpdateStateForSpan(span[start..], span.Length - start);
                return (false, span.Length);
            }

            // Adjust index to absolute position
            var pos = start + index;
            var current = span[pos];

            // Update state for characters before the found position
            UpdateStateForSpan(span[start..pos], pos - start);

            // Process the found character
            if (_inQuotes && current == (byte)'\\')
            {
                _escaped = !_escaped;
                start = pos + 1;
                continue;
            }

            if (current == (byte)'"' && !_escaped)
            {
                _inQuotes = !_inQuotes;
                start = pos + 1;
                continue;
            }

            if (_escaped && current != (byte)'\\')
            {
                _escaped = false;
            }

            // If we found a newline outside quotes, this is the end of the line
            if (current == (byte)'\n' && !_inQuotes)
            {
                // Include the newline character
                return (true, pos + 1);
            }

            // Move past the current character
            start = pos + 1;
        }

        // This point should not be reached because the while loop either returns or breaks
        // But for safety, consume the entire span
        return (false, span.Length);
    }

    private void UpdateStateForSpan(ReadOnlySpan<byte> span, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var ch = span[i];
            if (_inQuotes && ch == (byte)'\\')
            {
                _escaped = !_escaped;
                continue;
            }

            if (ch == (byte)'"' && !_escaped)
            {
                _inQuotes = !_inQuotes;
            }

            if (_escaped && ch != (byte)'\\')
            {
                _escaped = false;
            }
        }
    }
}
