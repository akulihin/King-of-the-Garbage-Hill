using System;
using System.Text.RegularExpressions;

namespace King_of_the_Garbage_Hill.Helpers;

public sealed record BilingualGeneratedText(string Russian, string English);

/// <summary>Parses the shared tagged contract used by AI-generated bilingual presentation text.</summary>
public static class BilingualGeneratedTextParser
{
    public static bool TryParse(string text, out BilingualGeneratedText result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var russian = Extract(text, "ru");
        var english = Extract(text, "en");
        if (string.IsNullOrWhiteSpace(russian) || string.IsNullOrWhiteSpace(english)) return false;

        result = new BilingualGeneratedText(russian, english);
        return true;
    }

    public static string CollapseToOneLine(string text) =>
        Regex.Replace(text?.Trim() ?? "", @"\s+", " ");

    private static string Extract(string text, string language)
    {
        var match = Regex.Match(text, $@"<{language}>\s*(.*?)\s*</{language}>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
