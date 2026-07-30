using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Localization;

/// <summary>
/// A stable reference to authored text. Game/domain code stores the key and arguments;
/// a presentation boundary chooses the viewer locale and renders it.
/// </summary>
public sealed record LocalizedMessage
{
    public LocalizedMessage(string key, IReadOnlyDictionary<string, string> arguments = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("A localization key is required.", nameof(key));

        Key = key;
        Arguments = arguments == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(arguments, StringComparer.Ordinal);
    }

    public string Key { get; init; }
    public IReadOnlyDictionary<string, string> Arguments { get; init; }
}

/// <summary>
/// Fully authored text that cannot use a reusable key, such as generated prose.
/// Static interface and gameplay text should use <see cref="LocalizedMessage"/>.
/// </summary>
public sealed record LocalizedText(string Ru, string En)
{
    public string Resolve(string language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? En : Ru;
}
