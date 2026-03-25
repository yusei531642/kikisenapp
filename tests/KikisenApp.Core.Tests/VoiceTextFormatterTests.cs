using KikisenApp.Core;

namespace KikisenApp.Core.Tests;

public sealed class VoiceTextFormatterTests
{
    [Fact]
    public void NormalizeForSpeech_ReplacesLineBreaksAndAddsPeriod()
    {
        var actual = VoiceTextFormatter.NormalizeForSpeech("こんにちは\r\nテスト  です");

        Assert.Equal("こんにちは。テスト です。", actual);
    }

    [Fact]
    public void NormalizeForSpeech_ReturnsEmptyForWhitespaceOnly()
    {
        var actual = VoiceTextFormatter.NormalizeForSpeech("   \r\n   ");

        Assert.Equal(string.Empty, actual);
    }
}
