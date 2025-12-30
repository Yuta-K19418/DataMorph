using System.IO.Pipelines;
using System.Text;
using DataMorph.Engine.IO;
using FluentAssertions;

namespace DataMorph.Tests.IO;

public sealed class PipelinesEngineTests : IDisposable
{
    private readonly string _testFilePath;

    public PipelinesEngineTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"pipelinesEngine_{Guid.NewGuid()}.txt");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    private static async Task<string> ReadAllFromPipe(PipeReader reader)
    {
        var sb = new StringBuilder();

        while (true)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            foreach (var segment in buffer)
            {
                sb.Append(Encoding.UTF8.GetString(segment.Span));
            }

            reader.AdvanceTo(buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        return sb.ToString();
    }

    [Fact]
    public void Create_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "line1\nline2\n");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;

        // Act
        var result = PipelinesEngine.Create(mmap, indexer);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var engine = result.Value;
        engine.RowCount.Should().Be(3);
    }

    [Fact]
    public void Create_WithNullMmapService_ThrowsArgumentNullException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;

        // Act & Assert
        var act = () => PipelinesEngine.Create(null!, indexer);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullRowIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;

        // Act & Assert
        var act = () => PipelinesEngine.Create(mmap, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task WriteRow_ValidIndex_WritesCorrectData()
    {
        // Arrange
        var content = "line1\nline2\nline3";
        File.WriteAllText(_testFilePath, content);
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var writeResult = await engine.WriteRowAsync(1);
        engine.CompleteWriter();

        // Assert
        writeResult.IsSuccess.Should().BeTrue();
        var data = await ReadAllFromPipe(engine.Reader);
        data.Should().Be("line2\n");
    }

    [Fact]
    public async Task WriteRow_FirstRow_WritesCorrectData()
    {
        // Arrange
        var content = "first\nsecond\nthird";
        File.WriteAllText(_testFilePath, content);
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var writeResult = await engine.WriteRowAsync(0);
        engine.CompleteWriter();

        // Assert
        writeResult.IsSuccess.Should().BeTrue();
        var data = await ReadAllFromPipe(engine.Reader);
        data.Should().Be("first\n");
    }

    [Fact]
    public async Task WriteRow_LastRow_WritesCorrectData()
    {
        // Arrange
        var content = "line1\nline2\nlast";
        File.WriteAllText(_testFilePath, content);
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var writeResult = await engine.WriteRowAsync(2);
        engine.CompleteWriter();

        // Assert
        writeResult.IsSuccess.Should().BeTrue();
        var data = await ReadAllFromPipe(engine.Reader);
        data.Should().Be("last");
    }

    [Fact]
    public async Task WriteRow_NegativeIndex_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var result = await engine.WriteRowAsync(-1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("out of range");
    }

    [Fact]
    public async Task WriteRow_IndexOutOfRange_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "line1\nline2");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var result = await engine.WriteRowAsync(10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("out of range");
    }

    [Fact]
    public async Task WriteRows_ValidRange_WritesAllRows()
    {
        // Arrange
        var content = "line1\nline2\nline3\nline4";
        File.WriteAllText(_testFilePath, content);
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var writeResult = await engine.WriteRowsAsync(1, 2);
        engine.CompleteWriter();

        // Assert
        writeResult.IsSuccess.Should().BeTrue();
        var data = await ReadAllFromPipe(engine.Reader);
        data.Should().Be("line2\nline3\n");
    }

    [Fact]
    public async Task WriteRows_EmptyRange_ReturnsSuccess()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "line1\nline2");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var result = await engine.WriteRowsAsync(0, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task WriteRows_NegativeStartRow_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var result = await engine.WriteRowsAsync(-1, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task WriteRows_NegativeRowCount_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var result = await engine.WriteRowsAsync(0, -1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task WriteRows_RangeExceedsRowCount_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "line1\nline2");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var result = await engine.WriteRowsAsync(0, 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("exceeds total rows");
    }

    [Fact]
    public async Task Reader_AfterWrite_CanReadData()
    {
        // Arrange
        var content = "test data\nmore data";
        File.WriteAllText(_testFilePath, content);
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        await engine.WriteRowAsync(0);
        engine.CompleteWriter();

        var readResult = await engine.Reader.ReadAsync();

        // Assert
        readResult.Buffer.IsEmpty.Should().BeFalse();
        var data = Encoding.UTF8.GetString(readResult.Buffer.FirstSpan);
        data.Should().Be("test data\n");

        engine.Reader.AdvanceTo(readResult.Buffer.End);
    }

    [Fact]
    public async Task Reader_MultipleWrites_CanReadInSequence()
    {
        // Arrange
        var content = "row1\nrow2\nrow3";
        File.WriteAllText(_testFilePath, content);
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        await engine.WriteRowAsync(0);
        await engine.WriteRowAsync(1);
        await engine.WriteRowAsync(2);
        engine.CompleteWriter();

        // Assert
        var data = await ReadAllFromPipe(engine.Reader);
        data.Should().Be("row1\nrow2\nrow3");
    }

    [Fact]
    public async Task Reader_AfterComplete_SignalsCompletion()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        engine.CompleteWriter();
        var readResult = await engine.Reader.ReadAsync();

        // Assert
        readResult.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task WriteRow_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        var engine = PipelinesEngine.Create(mmap, indexer).Value;
        engine.Dispose();

        // Act & Assert
        var act = async () => await engine.WriteRowAsync(0);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Reader_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        var engine = PipelinesEngine.Create(mmap, indexer).Value;
        engine.Dispose();

        // Act & Assert
        var act = () => _ = engine.Reader;
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void RowCount_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        var engine = PipelinesEngine.Create(mmap, indexer).Value;
        engine.Dispose();

        // Act & Assert
        var act = () => _ = engine.RowCount;
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act & Assert
        var act = () =>
        {
            engine.Dispose();
            engine.Dispose();
            engine.Dispose();
        };
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Integration_CsvFile_CanStreamAllRows()
    {
        // Arrange
        var csvContent = "Name,Age,City\nAlice,30,Tokyo\nBob,25,Osaka\nCharlie,35,Kyoto";
        File.WriteAllText(_testFilePath, csvContent);
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var writeResult = await engine.WriteRowsAsync(0, indexer.RowCount);
        engine.CompleteWriter();

        // Assert
        writeResult.IsSuccess.Should().BeTrue();
        var data = await ReadAllFromPipe(engine.Reader);
        data.Should().Be(csvContent);
    }

    [Fact]
    public async Task Integration_LargeFile_StreamsEfficiently()
    {
        // Arrange: Create a file with 1000 lines
        var lines = Enumerable.Range(0, 1000).Select(i => $"Line {i:D4}");
        var content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;

        // Act
        var writeResult = await engine.WriteRowsAsync(0, indexer.RowCount);
        engine.CompleteWriter();

        // Assert
        writeResult.IsSuccess.Should().BeTrue();
        var data = await ReadAllFromPipe(engine.Reader);
        data.Should().Be(content);
    }

    [Fact]
    public async Task WriteRow_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await engine.WriteRowAsync(0, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteRows_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "line1\nline2");
        using var mmap = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmap).Value;
        using var engine = PipelinesEngine.Create(mmap, indexer).Value;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await engine.WriteRowsAsync(0, 2, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
