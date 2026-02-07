using AwesomeAssertions;
using DataMorph.Engine.IO;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.IO;

public sealed class FormatDetectorTests : IDisposable
{
    private readonly string _testFilePath;

    public FormatDetectorTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"formatDetector_{Guid.NewGuid()}.txt");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task Detect_JsonArray_ReturnsJsonArray()
    {
        // Arrange
        var content = """
            [
              {"id": 1, "name": "Alice"},
              {"id": 2, "name": "Bob"}
            ]
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonArray);
    }

    [Fact]
    public async Task Detect_JsonObject_ReturnsJsonObject()
    {
        // Arrange
        var content = """
            {
              "data": [1, 2, 3],
              "metadata": {"version": 1}
            }
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task Detect_JsonLines_ReturnsJsonLines()
    {
        // Arrange
        var content = """
            {"id": 1, "name": "Alice"}
            {"id": 2, "name": "Bob"}
            {"id": 3, "name": "Charlie"}
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonLines);
    }

    [Fact]
    public async Task Detect_Csv_ReturnsCsv()
    {
        // Arrange
        var content = """
            id,name,age
            1,Alice,30
            2,Bob,25
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.Csv);
    }

    [Fact]
    public async Task Detect_CsvWithSingleColumn_ReturnsError()
    {
        // Arrange
        var content = """
            id
            1
            2
            3
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result
            .Error.Should()
            .Be(
                "Invalid CSV format: requires at least 2 columns. Supported formats: CSV, JSON Lines, JSON Array, JSON Object"
            );
    }

    [Fact]
    public async Task Detect_JsonArrayWithLeadingWhitespace_ReturnsJsonArray()
    {
        // Arrange
        var content = "   \n\t  [\n  {\"id\": 1}\n]";
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonArray);
    }

    [Fact]
    public async Task Detect_JsonObjectWithLeadingWhitespace_ReturnsJsonObject()
    {
        // Arrange
        var content = "  \n\r\n  {\"id\": 1}";
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task Detect_WithUtf8Bom_SkipsBomAndDetectsCorrectly()
    {
        // Arrange
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = "[{\"id\": 1}]"u8.ToArray();
        var combined = bom.Concat(content).ToArray();
        await File.WriteAllBytesAsync(_testFilePath, combined);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonArray);
    }

    [Fact]
    public async Task Detect_SingleLineJsonObject_ReturnsJsonObject()
    {
        // Arrange
        var content = "{\"id\":1,\"name\":\"Alice\",\"tags\":[1,2,3]}";
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task Detect_EmptyJsonArray_ReturnsJsonArray()
    {
        // Arrange
        var content = "[]";
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonArray);
    }

    [Fact]
    public async Task Detect_EmptyJsonObject_ReturnsJsonObject()
    {
        // Arrange
        var content = "{}";
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task Detect_JsonObjectWithNestedObjectAfterNewline_ReturnsJsonObject()
    {
        // Arrange
        var content = """
            {
              "user": "Alice",
              "profile":
              {
                "age": 30,
                "city": "Tokyo"
              }
            }
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task Detect_JsonObjectWithArrayOfObjects_ReturnsJsonObject()
    {
        // Arrange
        var content = """
            {
              "items": [
                {
                  "id": 1
                },
                {
                  "id": 2
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task Detect_JsonObjectWithNestedObjectNoIndent_ReturnsJsonObject()
    {
        // Arrange
        // This is a critical edge case: nested object without indentation
        // Pattern: "key":\n{ which contains \n{ but is still a single JSON object
        var content = """
            {"user":"Alice","profile":
            {"age":30,"city":"Tokyo"}}
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task Detect_FileWithOnlyWhitespace_ReturnsError()
    {
        // Arrange
        var content = "   \n\t  \r\n  ";
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("whitespace");
    }

    [Fact]
    public async Task Detect_EmptyFile_ReturnsError()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, string.Empty);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("File is empty");
    }

    [Fact]
    public async Task Detect_NullCreateStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await FormatDetector.Detect(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Detect_JsonLinesWithFirstObjectExceedingBufferSize_ReturnsJsonLines()
    {
        // Arrange
        // First object is > 4096 bytes, followed by a second object
        // 4096 bytes is the default value for StreamPipeReaderOptions.BufferSize
        var largeData = new string('x', 5000);
        var content = $$"""
            {"id":1,"data":"{{largeData}}"}
            {"id":2,"data":"short"}
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonLines);
    }

    [Fact]
    public async Task Detect_IncompleteJsonObject_ReturnsError()
    {
        // Arrange
        // JSON object that ends abruptly
        var content = """
            {"id": 1, "name": "Alice", "tags": [1, 2
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid JSON format");
    }

    [Fact]
    public async Task Detect_MalformedJsonObject_ReturnsError()
    {
        // Arrange
        // Malformed JSON object (e.g., missing value after comma)
        var content = """
            {"id": 1, "name": , "age": 30}
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid JSON format");
    }

    [Fact]
    public async Task Detect_StreamThrowsIOException_ReturnsError()
    {
        // Arrange
        // Create a factory function that throws an IOException
        Func<Stream> throwingStreamFactory = () => throw new IOException("Simulated IO error");

        // Act
        var result = await FormatDetector.Detect(throwingStreamFactory);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result
            .Error.Should()
            .Contain("Failed to read file for format detection: Simulated IO error");
    }

    [Fact]
    public async Task Detect_JsonLinesWithSingleBufferMultipleObjects_ReturnsJsonLines()
    {
        // Arrange
        // Content with multiple JSON objects that fits within a single buffer read
        var content = """
            {"id":1,"name":"Alice"}
            {"id":2,"name":"Bob"}
            {"id":3,"name":"Charlie"}
            """; // Total size < 4096 bytes (default PipeReader buffer)
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonLines);
    }

    [Fact]
    public async Task Detect_IncompleteJsonObjectNoJsonException_ReturnsError()
    {
        // Arrange
        // JSON object without a closing brace, which might not immediately throw JsonException
        // until a later attempt to read or validation. Utf8JsonReader.Read() will return false.
        var content = """
            {"key": "value", "anotherKey": 123
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The expected error message comes from the specific else branch in ProcessJson
        result.Error.Should().Contain("Invalid JSON format");
    }

    [Fact]
    public async Task Detect_XmlContent_ReturnsError()
    {
        // Arrange
        // XML content should be detected as CSV but fail validation
        var content = """
            <?xml version="1.0" encoding="UTF-8"?>
            <root>
              <item>
                <id>1</id>
                <name>Alice</name>
              </item>
              <item>
                <id>2</id>
                <name>Bob</name>
              </item>
            </root>
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result
            .Error.Should()
            .Be(
                "Invalid CSV format: requires at least 2 columns. Supported formats: CSV, JSON Lines, JSON Array, JSON Object"
            );
    }

    [Fact]
    public async Task Detect_XmlContentWithoutDeclaration_ReturnsError()
    {
        // Arrange
        // XML content without declaration
        var content = """
            <root>
              <user id="1" name="Alice"/>
              <user id="2" name="Bob"/>
            </root>
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result
            .Error.Should()
            .Be(
                "Invalid CSV format: requires at least 2 columns. Supported formats: CSV, JSON Lines, JSON Array, JSON Object"
            );
    }

    [Fact]
    public async Task Detect_YamlContent_ReturnsError()
    {
        // Arrange
        // YAML content should be detected as CSV but fail validation
        var content = """
            users:
              - id: 1
                name: Alice
                age: 30
              - id: 2
                name: Bob
                age: 25
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result
            .Error.Should()
            .Be(
                "Invalid CSV format: requires at least 2 columns. Supported formats: CSV, JSON Lines, JSON Array, JSON Object"
            );
    }

    [Fact]
    public async Task Detect_YamlContentWithDashes_ReturnsError()
    {
        // Arrange
        // YAML list content
        var content = """
            - name: Alice
              age: 30
              city: Tokyo
            - name: Bob
              age: 25
              city: Osaka
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result
            .Error.Should()
            .Be(
                "Invalid CSV format: requires at least 2 columns. Supported formats: CSV, JSON Lines, JSON Array, JSON Object"
            );
    }

    [Fact]
    public async Task Detect_YamlContentSimple_ReturnsError()
    {
        // Arrange
        // Simple YAML key-value pairs
        var content = """
            name: Alice
            age: 30
            city: Tokyo
            """;
        await File.WriteAllTextAsync(_testFilePath, content);

        // Act
        var result = await FormatDetector.Detect(() => File.OpenRead(_testFilePath));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result
            .Error.Should()
            .Be(
                "Invalid CSV format: requires at least 2 columns. Supported formats: CSV, JSON Lines, JSON Array, JSON Object"
            );
    }
}
