using System.Buffers;
using System.IO.Pipelines;
using System.Text.Json;
using DataMorph.Engine.Types;
using nietras.SeparatedValues;

namespace DataMorph.Engine.IO;

/// <summary>
/// Detects the data format (CSV, JSON Lines, JSON Array, JSON Object) from file content.
/// </summary>
public static class FormatDetector
{
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];
    private static ReadOnlySpan<byte> WhiteSpaceBytes =>
        [(byte)' ', (byte)'\t', (byte)'\n', (byte)'\r'];
    private static readonly string _supportedFormatNames = string.Join(
        ", ",
        Enum.GetValues<DataFormat>().Select(format => format.GetDisplayName())
    );

    /// <summary>
    /// Detects the format of the data in a stream.
    /// The provided stream will be disposed after detection.
    /// </summary>
    /// <param name="createStream">A factory function that creates the stream to be analyzed.</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>A Result containing the detected DataFormat, or an error message.</returns>
    public static async ValueTask<Result<DataFormat>> Detect(
        Func<Stream> createStream,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(createStream);

        try
        {
            using var pipeStream = createStream();
            var reader = PipeReader.Create(pipeStream);

            // First loop: Skip BOM and whitespace until first valid character is found
            ReadOnlySequence<byte> processedBuffer;
            Result<DataFormat>? errorResult;

            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                var skipResult = TrySkipBomAndWhitespace(buffer, result.IsCompleted);

                if (!skipResult.canSkip)
                {
                    processedBuffer = skipResult.remainingBuffer;
                    errorResult = skipResult.errorResult;
                    break;
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
            }

            if (errorResult is { } error)
            {
                await reader.CompleteAsync();
                return error;
            }

            reader.AdvanceTo(processedBuffer.Start);

            // Detect format based on first character
            var immediateResult = await TryDetectAndValidateImmediateFormat(
                processedBuffer,
                createStream,
                cancellationToken
            );

            if (immediateResult is { } detectedFormat)
            {
                await reader.CompleteAsync();
                return detectedFormat;
            }

            // Second loop: Process JSON to distinguish JsonObject vs JsonLines
            var jsonState = new JsonReaderState();
            var completedFirstObject = false;

            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                var processResult = ProcessJson(
                    buffer,
                    result.IsCompleted,
                    jsonState,
                    completedFirstObject
                );

                if (processResult.result is { } finalResult)
                {
                    await reader.CompleteAsync();
                    return finalResult;
                }

                jsonState = processResult.jsonState;
                completedFirstObject = processResult.completedFirstObject;
                reader.AdvanceTo(processResult.consumed, buffer.End);
            }
        }
        catch (ObjectDisposedException ex)
        {
            return Results.Failure<DataFormat>($"Stream has been disposed: {ex.Message}");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.Failure<DataFormat>($"Invalid read range: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Results.Failure<DataFormat>(
                $"Failed to read file for format detection: {ex.Message}"
            );
        }
    }

    private static (
        bool canSkip,
        ReadOnlySequence<byte> remainingBuffer,
        Result<DataFormat>? errorResult
    ) TrySkipBomAndWhitespace(ReadOnlySequence<byte> buffer, bool isCompleted)
    {
        var sequenceReader = new SequenceReader<byte>(buffer);
        var remainingBuffer = buffer;

        // BOM check
        if (sequenceReader.IsNext(Utf8Bom))
        {
            remainingBuffer = buffer.Slice(sequenceReader.Position);
            sequenceReader.Advance(Utf8Bom.Length);
        }

        // Empty file check
        if (!sequenceReader.TryPeek(out var first) && isCompleted)
        {
            return (canSkip: false, remainingBuffer, Results.Failure<DataFormat>("File is empty"));
        }

        // Skip whitespace
        sequenceReader.AdvancePastAny(WhiteSpaceBytes);
        remainingBuffer = buffer.Slice(sequenceReader.Position);

        if (!sequenceReader.TryPeek(out first))
        {
            if (isCompleted)
            {
                return (
                    canSkip: false,
                    remainingBuffer,
                    Results.Failure<DataFormat>("File contains only whitespace")
                );
            }

            return (canSkip: true, remainingBuffer, null); // Need more data
        }

        // Found first valid character
        return (canSkip: false, remainingBuffer, null);
    }

    private static async ValueTask<Result<DataFormat>?> TryDetectAndValidateImmediateFormat(
        ReadOnlySequence<byte> processedBuffer,
        Func<Stream> createStream,
        CancellationToken cancellationToken
    )
    {
        var classifiedFormat = ClassifyByFirstCharacter(processedBuffer);
        if (classifiedFormat is not { } format)
        {
            return null; // Need JSON processing
        }

        if (format == DataFormat.Csv)
        {
            return await ValidateCsvFormat(createStream, cancellationToken);
        }

        return Results.Success(format);
    }

    private static DataFormat? ClassifyByFirstCharacter(ReadOnlySequence<byte> buffer)
    {
        var sequenceReader = new SequenceReader<byte>(buffer);
        sequenceReader.TryPeek(out var firstChar);

        return (char)firstChar switch
        {
            '[' => DataFormat.JsonArray,
            '{' => null, // Need JSON processing
            _ => DataFormat.Csv,
        };
    }

    private static async ValueTask<Result<DataFormat>> ValidateCsvFormat(
        Func<Stream> createStream,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var sepStream = createStream();
            using var sepReader = await Sep.New(',')
                .Reader()
                .FromAsync(sepStream, cancellationToken);

            if (sepReader.Header.ColNames.Count <= 1)
            {
                return Results.Failure<DataFormat>(
                    $"Invalid CSV format: requires at least 2 columns. Supported formats: {_supportedFormatNames}"
                );
            }

            return Results.Success(DataFormat.Csv);
        }
        catch (Exception ex)
            when (ex is FormatException or ArgumentException or InvalidDataException)
        {
            return Results.Failure<DataFormat>($"Failed to parse CSV format: {ex.Message}");
        }
    }

    private static (
        Result<DataFormat>? result,
        JsonReaderState jsonState,
        bool completedFirstObject,
        SequencePosition consumed
    ) ProcessJson(
        ReadOnlySequence<byte> buffer,
        bool isCompleted,
        JsonReaderState jsonState,
        bool completedFirstObject
    )
    {
        var jsonReader = new Utf8JsonReader(buffer, isCompleted, jsonState);

        try
        {
            while (jsonReader.Read())
            {
                if (jsonReader.CurrentDepth == 0 && jsonReader.TokenType == JsonTokenType.EndObject)
                {
                    completedFirstObject = true;
                }
            }

            if (!isCompleted)
            {
                return (null, jsonReader.CurrentState, completedFirstObject, jsonReader.Position); // Need more data
            }

            return (
                Results.Success(DataFormat.JsonObject),
                jsonReader.CurrentState,
                completedFirstObject,
                jsonReader.Position
            );
        }
        catch (JsonException ex)
        {
            // If the next root-level object starts in a subsequent buffer (after buffer boundary),
            // Utf8JsonReader will throw JsonException when it encounters a new root-level object
            // because it expects only one root-level value per JSON document.
            // This exception after completing the first object indicates JSON Lines format.
            if (completedFirstObject)
            {
                return (
                    Results.Success(DataFormat.JsonLines),
                    jsonReader.CurrentState,
                    completedFirstObject,
                    jsonReader.Position
                );
            }

            // JsonException before completing the first object indicates invalid JSON
            return (
                Results.Failure<DataFormat>($"Invalid JSON format: {ex.Message}"),
                jsonReader.CurrentState,
                completedFirstObject,
                jsonReader.Position
            );
        }
    }
}
