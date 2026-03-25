using System.Text.RegularExpressions;

namespace KikisenApp.Core;

public static partial class VoiceTextFormatter
{
    private static readonly string[] EndingPunctuation = ["。", "！", "？", ".", "!", "?"];

    public static string NormalizeForSpeech(string text)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var normalized = trimmed
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\t', ' ');

        normalized = MultiWhitespaceRegex().Replace(normalized, " ");
        normalized = NewLineRegex().Replace(normalized, "。");
        normalized = ConsecutiveJapanesePeriodsRegex().Replace(normalized, "。");
        normalized = normalized.Trim();

        if (!EndingPunctuation.Any(normalized.EndsWith))
        {
            normalized += "。";
        }

        return normalized;
    }

    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex MultiWhitespaceRegex();

    [GeneratedRegex(@"\n+")]
    private static partial Regex NewLineRegex();

    [GeneratedRegex(@"。{2,}")]
    private static partial Regex ConsecutiveJapanesePeriodsRegex();
}
