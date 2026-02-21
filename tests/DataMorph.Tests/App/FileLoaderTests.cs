using AwesomeAssertions;
using DataMorph.App;

namespace DataMorph.Tests.App;

public sealed class FileLoaderTests : IDisposable
{
    private readonly string _csvFilePath;
    private readonly string _jsonlFilePath;
    private readonly string _unsupportedFilePath;

    public FileLoaderTests()
    {
        _csvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _jsonlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        _unsupportedFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
    }

    public void Dispose()
    {
        foreach (var path in new[] { _csvFilePath, _jsonlFilePath, _unsupportedFilePath })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WithCsvFile_UpdatesStateAndMode()
    {
        // Arrange
        await File.WriteAllTextAsync(_csvFilePath, "Name,Age\nAlice,30\nBob,25");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        await loader.LoadAsync(_csvFilePath);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
        state.Schema.Should().NotBeNull();
        state.CsvIndexer.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_WithJsonLinesFile_UpdatesStateAndMode()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}\n{\"name\":\"Bob\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        await loader.LoadAsync(_jsonlFilePath);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        state.JsonLinesIndexer.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTreeMode_ScansSchemaAndSwitchesToTable()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}\n{\"name\":\"Bob\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);
        await loader.LoadAsync(_jsonlFilePath);

        // Act
        await loader.ToggleJsonLinesModeAsync();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        state.Schema.Should().NotBeNull();
        state.JsonLinesSchemaScanner.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTableMode_RestoresTreeMode()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);
        await loader.LoadAsync(_jsonlFilePath);
        await loader.ToggleJsonLinesModeAsync();

        // Act
        await loader.ToggleJsonLinesModeAsync();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task LoadAsync_WithUnsupportedFormat_SetsLastError()
    {
        // Arrange
        await File.WriteAllTextAsync(_unsupportedFilePath, "{\"key\":\"value\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        await loader.LoadAsync(_unsupportedFilePath);

        // Assert
        state.LastError.Should().NotBeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }
}
