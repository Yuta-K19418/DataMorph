using System.Diagnostics;
using AwesomeAssertions;
using DataMorph.App.Views;
using Terminal.Gui.Drivers;

namespace DataMorph.Tests.App.Views;

public sealed class VimKeyTranslatorTests
{
    // ---------------------------------------------------------------------------
    // Basic hjkl mappings
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(KeyCode.H, (int)VimAction.MoveLeft)]
    [InlineData(KeyCode.J, (int)VimAction.MoveDown)]
    [InlineData(KeyCode.K, (int)VimAction.MoveUp)]
    [InlineData(KeyCode.L, (int)VimAction.MoveRight)]
    public void Translate_HjklKeys_ReturnExpectedMoveAction(KeyCode keyCode, int expectedRaw)
    {
        // Arrange
        var translator = new VimKeyTranslator();
        var expected = (VimAction)expectedRaw;

        // Act
        var result = translator.Translate(keyCode);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Translate_UnrecognizedKey_ReturnsNone()
    {
        // Arrange
        var translator = new VimKeyTranslator();

        // Act
        var result = translator.Translate(KeyCode.A);

        // Assert
        result.Should().Be(VimAction.None);
    }

    // ---------------------------------------------------------------------------
    // Shift+G → GoToEnd
    // ---------------------------------------------------------------------------

    [Fact]
    public void Translate_ShiftG_ReturnsGoToEnd()
    {
        // Arrange
        var translator = new VimKeyTranslator();

        // Act
        var result = translator.Translate(KeyCode.G | KeyCode.ShiftMask);

        // Assert
        result.Should().Be(VimAction.GoToEnd);
    }

    [Fact]
    public void Translate_ShiftG_ClearsPendingGState()
    {
        // Arrange
        var translator = new VimKeyTranslator();
        _ = translator.Translate(KeyCode.G); // start pending

        // Act
        var result = translator.Translate(KeyCode.G | KeyCode.ShiftMask);

        // Assert
        result.Should().Be(VimAction.GoToEnd);
    }

    // ---------------------------------------------------------------------------
    // 'gg' sequence — within timeout
    // ---------------------------------------------------------------------------

    [Fact]
    public void Translate_FirstG_ReturnsPendingGSequence()
    {
        // Arrange
        var translator = new VimKeyTranslator();

        // Act
        var result = translator.Translate(KeyCode.G);

        // Assert
        result.Should().Be(VimAction.PendingGSequence);
    }

    [Fact]
    public void Translate_GG_WithinTimeout_ReturnsGoToFirst()
    {
        // Arrange — timeout of 500 ms; do not advance time (elapsed is 0 ms)
        long fakeTime = 0;
        var translator = new VimKeyTranslator(() => fakeTime, timeoutMs: 500);

        _ = translator.Translate(KeyCode.G); // first g
        // fakeTime stays at 0 — 0 ms elapsed, well within the 500 ms timeout

        // Act
        var result = translator.Translate(KeyCode.G); // second g

        // Assert
        result.Should().Be(VimAction.GoToFirst);
    }

    // ---------------------------------------------------------------------------
    // 'gg' sequence — timeout exceeded
    // ---------------------------------------------------------------------------

    [Fact]
    public void Translate_GG_TimeoutExceeded_ReturnsPendingGSequenceAgain()
    {
        // Arrange — timeout of 500 ms; advance fake time by 1 second (1000 ms > 500 ms)
        long fakeTime = 0;
        var translator = new VimKeyTranslator(() => fakeTime, timeoutMs: 500);

        _ = translator.Translate(KeyCode.G); // first g
        fakeTime += Stopwatch.Frequency; // advance by exactly 1 second, exceeds 500 ms timeout

        // Act
        var result = translator.Translate(KeyCode.G); // second g (timed out)

        // Assert — treated as a new first 'g', not GoToFirst
        result.Should().Be(VimAction.PendingGSequence);
    }

    // ---------------------------------------------------------------------------
    // 'g' followed by a different key resets pending state
    // ---------------------------------------------------------------------------

    [Fact]
    public void Translate_GPlusJ_ResetsAndReturnsMoveDown()
    {
        // Arrange
        var translator = new VimKeyTranslator();
        _ = translator.Translate(KeyCode.G); // first g

        // Act
        var result = translator.Translate(KeyCode.J);

        // Assert — pending cleared; 'j' is processed normally
        result.Should().Be(VimAction.MoveDown);
    }

    [Fact]
    public void Translate_GPlusUnrecognizedKey_ResetsAndReturnsNone()
    {
        // Arrange
        var translator = new VimKeyTranslator();
        _ = translator.Translate(KeyCode.G); // first g

        // Act
        var result = translator.Translate(KeyCode.A);

        // Assert
        result.Should().Be(VimAction.None);
    }

    // ---------------------------------------------------------------------------
    // State reset after GoToFirst
    // ---------------------------------------------------------------------------

    [Fact]
    public void Translate_AfterGoToFirst_PendingIsReset()
    {
        // Arrange
        long fakeTime = 0;
        var translator = new VimKeyTranslator(
            () => fakeTime,
            timeoutMs: VimKeyTranslator.DefaultTimeoutMs
        );

        _ = translator.Translate(KeyCode.G); // first g
        _ = translator.Translate(KeyCode.G); // gg → GoToFirst

        // Act — next 'g' should start a fresh pending sequence
        var result = translator.Translate(KeyCode.G);

        // Assert
        result.Should().Be(VimAction.PendingGSequence);
    }

    // ---------------------------------------------------------------------------
    // Shift modifier does not affect hjkl
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(KeyCode.H)]
    [InlineData(KeyCode.J)]
    [InlineData(KeyCode.K)]
    [InlineData(KeyCode.L)]
    public void Translate_HjklWithShift_ReturnsNone(KeyCode keyCode)
    {
        // Arrange
        var translator = new VimKeyTranslator();

        // Act — uppercase H/J/K/L (shift held) should not trigger vim moves
        var result = translator.Translate(keyCode | KeyCode.ShiftMask);

        // Assert
        result.Should().Be(VimAction.None);
    }
}
