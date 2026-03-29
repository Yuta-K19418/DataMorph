using System.Diagnostics;
using AwesomeAssertions;
using DataMorph.App;

namespace DataMorph.Tests.App;

public sealed class FileLoaderTests : IDisposable
{
    private readonly string _csvFilePath;
    private readonly string _jsonlFilePath;
    private readonly string _unsupportedFilePath;
    private readonly string _emptyCsvFilePath;
    private readonly string _nonExistentFilePath;
    private readonly string _headerOnlyCsvFilePath;

    public FileLoaderTests()
    {
        _csvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _jsonlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        _unsupportedFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _emptyCsvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _nonExistentFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        _headerOnlyCsvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
    }

    public void Dispose()
    {
        foreach (var path in new[] { _csvFilePath, _jsonlFilePath, _unsupportedFilePath, _emptyCsvFilePath, _headerOnlyCsvFilePath })
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
        var result = await loader.LoadAsync(_csvFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
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
        var result = await loader.LoadAsync(_jsonlFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
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
        var result = await loader.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
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
        var result = await loader.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task LoadAsync_WithUnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_unsupportedFilePath, "{\"key\":\"value\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_unsupportedFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(".json");
        state.CsvIndexer.Should().BeNull();
        state.JsonLinesIndexer.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_nonExistentFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
        state.CsvIndexer.Should().BeNull();
        state.JsonLinesIndexer.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithEmptyFile_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllBytesAsync(_emptyCsvFilePath, []);
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_emptyCsvFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
        state.CsvIndexer.Should().BeNull();
        state.JsonLinesIndexer.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithCsvContainingHeaderOnly_LoadsSuccessfully()
    {
        // Arrange
        // A CSV with a header row but no data rows is valid; schema is inferred from column names.
        await File.WriteAllTextAsync(_headerOnlyCsvFilePath, "Name,Age\n");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_headerOnlyCsvFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.CsvTable);

        var schema = state.Schema;
        schema.Should().NotBeNull();
        schema.Columns.Should().NotBeNull();
        schema.Columns.Should().HaveCount(2);
        state.CsvIndexer.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_JsonLines_WhenCalledTwice_CancelsPreviousBuildIndex()
    {
        // Arrange
        // 500,000 rows (~6.0 MB) ensures the build index takes multiple 1 MB buffer reads,
        // providing a wide window for the UI thread to wake up and subscribe before completion.
        var lines = Enumerable.Range(0, 500_000).Select(i => $"{{\"id\":{i}}}").ToList();
        await File.WriteAllLinesAsync(_jsonlFilePath, lines);
        var state = new AppState();
        using var loader = new FileLoader(state);

        await loader.LoadAsync(_jsonlFilePath);
        state.JsonLinesIndexer.Should().NotBeNull();
        var indexer = state.JsonLinesIndexer ?? throw new UnreachableException();

        var completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        indexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        // Act - Use a different file path for the second load to avoid I/O sharing violations
        // while the first indexer is still reading the original file.
        var secondPath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        try
        {
            var secondLines = Enumerable.Range(0, 100).Select(i => $"{{\"id\":{i}}}").ToList();
            await File.WriteAllLinesAsync(secondPath, secondLines);
            var result = await loader.LoadAsync(secondPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            completed.Task.IsCompletedSuccessfully.Should().BeTrue();
            state.JsonLinesIndexer.Should().NotBeNull();
        }
        finally
        {
            File.Delete(secondPath);
        }
    }

    [Fact]
    public async Task LoadAsync_Csv_WhenCalledTwice_CancelsPreviousBuildIndex()
    {
        // Arrange
        // 500,000 rows (~7.0 MB) ensures BuildIndex requires multiple 1 MB buffer
        // read, so cancellation is detected between iterations even after InitialScanAsync
        // (which reads only 200 rows) returns and allows subscription to proceed.
        var header = "id,name";
        var rows = Enumerable.Range(0, 500_000).Select(i => $"{i},Row{i}");
        var content = string.Join("\n", [header, .. rows]);
        await File.WriteAllTextAsync(_csvFilePath, content);
        var state = new AppState();
        using var loader = new FileLoader(state);
        await loader.LoadAsync(_csvFilePath);
        state.CsvIndexer.Should().NotBeNull();
        var indexer = state.CsvIndexer ?? throw new UnreachableException();

        var completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        indexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        // Act - Use a different file path for the second load to avoid I/O sharing violations.
        var secondPath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        try
        {
            var secondRows = Enumerable.Range(0, 100).Select(i => $"{i},Row{i}");
            var secondContent = string.Join("\n", [header, .. secondRows]);
            await File.WriteAllTextAsync(secondPath, secondContent);
            var result = await loader.LoadAsync(secondPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            completed.Task.IsCompletedSuccessfully.Should().BeTrue();
            state.CsvIndexer.Should().NotBeNull();
        }
        finally
        {
            File.Delete(secondPath);
        }
    }

    [Fact]
    public async Task Dispose_CancelsBuildIndex()
    {
        // Arrange
        // 500,000 rows (~6.0 MB) forces BuildIndex into multiple 1 MB buffer reads.
        // Cancellation is checked at the start of each iteration, so disposing
        // after the first checkpoint guarantees detection before subsequent reads.
        var lines = Enumerable.Range(0, 500_000).Select(i => $"{{\"id\":{i}}}").ToList();
        await File.WriteAllLinesAsync(_jsonlFilePath, lines);
        var state = new AppState();
        var loader = new FileLoader(state);
        await loader.LoadAsync(_jsonlFilePath);
        state.JsonLinesIndexer.Should().NotBeNull();
        var indexer = state.JsonLinesIndexer ?? throw new UnreachableException();

        var completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        indexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        // Act
        loader.Dispose();

        // Assert
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        completed.Task.IsCompletedSuccessfully.Should().BeTrue();
    }
}
