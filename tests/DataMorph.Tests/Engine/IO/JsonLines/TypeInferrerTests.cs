using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Engine.IO.JsonLines;

public sealed class TypeInferrerTests
{
    [Theory]
    [InlineData("42", ColumnType.WholeNumber)]
    [InlineData("-9223372036854775808", ColumnType.WholeNumber)]
    [InlineData("9223372036854775807", ColumnType.WholeNumber)]
    [InlineData("3.14", ColumnType.FloatingPoint)]
    [InlineData("1.5e10", ColumnType.FloatingPoint)]
    [InlineData("9999999999999999999999", ColumnType.Text)]
    [InlineData("\"hello\"", ColumnType.Text)]
    [InlineData("\"\"", ColumnType.Text)]
    [InlineData("true", ColumnType.Boolean)]
    [InlineData("false", ColumnType.Boolean)]
    [InlineData("{}", ColumnType.JsonObject)]
    [InlineData("{\"a\":1}", ColumnType.JsonObject)]
    [InlineData("[]", ColumnType.JsonArray)]
    [InlineData("[1,2,3]", ColumnType.JsonArray)]
    public void InferType_ValueTokens_ReturnsExpectedType(string json, ColumnType expected)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read();

        // Act
        var result = TypeInferrer.InferType(reader.TokenType, reader.ValueSpan);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void InferType_NoneToken_ReturnsFallbackText()
    {
        // Arrange / Act
        var result = TypeInferrer.InferType(JsonTokenType.None, []);

        // Assert
        result.Should().Be(ColumnType.Text);
    }

    [Theory]
    [InlineData(JsonTokenType.Null, true)]
    [InlineData(JsonTokenType.String, false)]
    [InlineData(JsonTokenType.Number, false)]
    [InlineData(JsonTokenType.True, false)]
    [InlineData(JsonTokenType.False, false)]
    [InlineData(JsonTokenType.StartObject, false)]
    [InlineData(JsonTokenType.StartArray, false)]
    public void IsNullToken_TokenScenarios_ReturnsExpected(JsonTokenType tokenType, bool expected)
    {
        // Act
        var result = TypeInferrer.IsNullToken(tokenType);

        // Assert
        result.Should().Be(expected);
    }
}
