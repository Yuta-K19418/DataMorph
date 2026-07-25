using AwesomeAssertions;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.Engine.IO;

public sealed class CacheEntryTests
{
    [Fact]
    public void Clear_WhenCalled_SetsRowIndexToMinusOne()
    {
        // Arrange
        var entry = new CacheEntry<int> { RowIndex = 5, Value = 100 };
        var emptyValue = -1;

        // Act
        entry.Clear(emptyValue);

        // Assert
        entry.RowIndex.Should().Be(-1);
    }

    [Fact]
    public void Clear_WhenCalled_SetsValueToEmptyValue()
    {
        // Arrange
        var entry = new CacheEntry<string> { RowIndex = 5, Value = "test" };
        var emptyValue = string.Empty;

        // Act
        entry.Clear(emptyValue);

        // Assert
        entry.Value.Should().Be(string.Empty);
        entry.RowIndex.Should().Be(-1);
    }
}
