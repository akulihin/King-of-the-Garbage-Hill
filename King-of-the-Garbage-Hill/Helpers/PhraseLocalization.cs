using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using King_of_the_Garbage_Hill.Game.MemoryStorage;

namespace King_of_the_Garbage_Hill.Helpers;

/// <summary>
/// Loads the authored Russian/English pair for every character phrase. Russian phrase text remains
/// canonical and is validated against the C# source; the field/passive context keeps repeated memes
/// free to use different English adaptations.
/// </summary>
public static class PhraseLocalization
{
    private static readonly Lazy<IReadOnlyDictionary<string, PhraseGroup>> PhraseGroups =
        new(LoadPhraseGroups);

    public static void Populate(CharactersUniquePhrase phrases)
    {
        var catalog = PhraseGroups.Value;
        var fields = typeof(CharactersUniquePhrase)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => field.FieldType == typeof(CharactersUniquePhrase.PhraseClass))
            .ToList();

        var errors = new List<string>();
        foreach (var field in fields)
        {
            var phrase = (CharactersUniquePhrase.PhraseClass)field.GetValue(phrases)!;
            if (!catalog.TryGetValue(field.Name, out var localized))
            {
                errors.Add($"missing group {field.Name}");
                continue;
            }

            if (!string.Equals(localized.PassiveNameRussian, phrase.PassiveNameRus, StringComparison.Ordinal))
            {
                errors.Add($"{field.Name}: passive name differs from the Russian source");
                continue;
            }

            if (localized.Phrases.Count != phrase.PassiveLogRus.Count)
            {
                errors.Add($"{field.Name}: {phrase.PassiveLogRus.Count} source / {localized.Phrases.Count} catalog variants");
                continue;
            }

            for (var index = 0; index < phrase.PassiveLogRus.Count; index++)
            {
                if (!string.Equals(localized.Phrases[index].Russian, phrase.PassiveLogRus[index], StringComparison.Ordinal))
                    errors.Add($"{field.Name}[{index}]: Russian catalog text differs from the source");
            }

            phrase.PassiveLogEng = localized.Phrases.Select(pair => pair.English).ToList();
            phrase.PassiveNameEng = localized.PassiveNameEnglish;
            if (ContainsCyrillic(phrase.PassiveNameEng))
                errors.Add($"{field.Name}: untranslated passive name '{phrase.PassiveNameRus}'");
        }

        foreach (var extra in catalog.Keys.Except(fields.Select(field => field.Name), StringComparer.Ordinal))
            errors.Add($"unknown group {extra}");

        if (errors.Count > 0)
            throw new InvalidDataException("Invalid DataBase/phrases.en.json:\n- " + string.Join("\n- ", errors));
    }

    public static bool TryTranslateLegacy(
        string passiveName, string phrase, string language, out string translated)
    {
        var english = GameLocalization.Normalize(language) == GameLocalization.English;
        var groups = PhraseGroups.Value.Values.Where(candidate =>
            string.Equals(candidate.PassiveNameRussian, passiveName, StringComparison.Ordinal) ||
            string.Equals(candidate.PassiveNameEnglish, passiveName, StringComparison.Ordinal)).ToList();
        if (groups.Count == 0)
        {
            translated = phrase;
            return false;
        }
        if (english && groups.SelectMany(group => group.Phrases)
                .Any(pair => phrase.StartsWith(pair.English, StringComparison.Ordinal)))
        {
            translated = phrase;
            return false;
        }

        translated = phrase;
        var changed = false;
        foreach (var pair in groups.SelectMany(group => group.Phrases).OrderByDescending(pair =>
                     (english ? pair.Russian : pair.English).Length))
        {
            var source = english ? pair.Russian : pair.English;
            var replacement = english ? pair.English : pair.Russian;
            if (!translated.Contains(source, StringComparison.Ordinal)) continue;
            translated = translated.Replace(source, replacement, StringComparison.Ordinal);
            changed = true;
        }
        return changed;
    }

    public static string ResolveLegacyMarkers(string text, string language)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("|>Phrase<|", StringComparison.Ordinal)) return text;
        var english = GameLocalization.Normalize(language) == GameLocalization.English;
        var header = new Regex(@"\|>Phrase<\|([^:\r\n]+):\s*", RegexOptions.Compiled);
        var result = new StringBuilder();
        var cursor = 0;
        while (cursor < text.Length)
        {
            var match = header.Match(text, cursor);
            if (!match.Success) break;
            result.Append(text, cursor, match.Index - cursor);
            var passiveName = match.Groups[1].Value;
            var bodyStart = match.Index + match.Length;
            var matchPair = PhraseGroups.Value.Values
                .Where(candidate =>
                    string.Equals(candidate.PassiveNameRussian, passiveName, StringComparison.Ordinal) ||
                    string.Equals(candidate.PassiveNameEnglish, passiveName, StringComparison.Ordinal))
                .SelectMany(group => group.Phrases.Select(pair => (Group: group, Pair: pair)))
                .OrderByDescending(candidate =>
                    (english ? candidate.Pair.Russian : candidate.Pair.English).Length)
                .FirstOrDefault(candidate => text.AsSpan(bodyStart).StartsWith(
                    english ? candidate.Pair.Russian : candidate.Pair.English, StringComparison.Ordinal));
            if (matchPair.Group == null || matchPair.Pair == null)
            {
                result.Append(match.Value);
                cursor = bodyStart;
                continue;
            }

            var translatedName = english ? matchPair.Group.PassiveNameEnglish : matchPair.Group.PassiveNameRussian;
            var translatedBody = english ? matchPair.Pair.English : matchPair.Pair.Russian;
            var sourceLength = (english ? matchPair.Pair.Russian : matchPair.Pair.English).Length;
            result.Append("|>Phrase<|").Append(translatedName).Append(": ").Append(translatedBody);
            cursor = bodyStart + sourceLength;
        }
        result.Append(text, cursor, text.Length - cursor);
        return result.ToString();
    }

    public static IEnumerable<KeyValuePair<string, string>> GetUnambiguousPairs(bool english)
    {
        var passiveNames = PhraseGroups.Value.Values
            .Select(group => english ? group.PassiveNameRussian : group.PassiveNameEnglish)
            .ToHashSet(StringComparer.Ordinal);
        var pairs = PhraseGroups.Value.Values
            .SelectMany(group => group.Phrases)
            .GroupBy(pair => english ? pair.Russian : pair.English, StringComparer.Ordinal);
        foreach (var group in pairs)
        {
            var translations = group
                .Select(pair => english ? pair.English : pair.Russian)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (translations.Count == 1 && !passiveNames.Contains(group.Key) &&
                (!english || ContainsCyrillic(group.Key)))
                yield return new KeyValuePair<string, string>(group.Key, translations[0]);
        }
    }

    private static IReadOnlyDictionary<string, PhraseGroup> LoadPhraseGroups()
    {
        var path = FindDataFile("phrases.en.json");
        var result = JsonSerializer.Deserialize<Dictionary<string, PhraseGroup>>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{path} is empty.");

        var invalid = result
            .SelectMany(group => group.Value.Phrases.Select((pair, index) => (group.Key, index, pair.English)))
            .Where(entry => string.IsNullOrWhiteSpace(entry.English) || ContainsCyrillic(entry.English))
            .Select(entry => $"{entry.Key}[{entry.index}]")
            .ToList();
        invalid.AddRange(result
            .Where(group => string.IsNullOrWhiteSpace(group.Value.PassiveNameEnglish) ||
                            ContainsCyrillic(group.Value.PassiveNameEnglish))
            .Select(group => $"{group.Key}.passiveNameEnglish"));
        if (invalid.Count > 0)
            throw new InvalidDataException($"English phrases are empty or contain Cyrillic: {string.Join(", ", invalid)}");

        return result;
    }

    private sealed class PhraseGroup
    {
        public string PassiveNameRussian { get; set; } = "";
        public string PassiveNameEnglish { get; set; } = "";
        public List<PhrasePair> Phrases { get; set; } = new();
    }

    private sealed class PhrasePair
    {
        public string Russian { get; set; } = "";
        public string English { get; set; } = "";
    }

    private static string FindDataFile(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "DataBase", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "DataBase", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "King-of-the-Garbage-Hill", "DataBase", fileName),
        };
        return candidates.FirstOrDefault(File.Exists)
               ?? throw new FileNotFoundException($"DataBase/{fileName} was not found.");
    }

    private static bool ContainsCyrillic(string value) => Regex.IsMatch(value, "[А-Яа-яЁё]");
}

/// <summary>
/// Replay-safe phrase record. Both renderings travel with the selected random line so switching
/// language never changes the joke or depends on a client-side translation catalog.
/// </summary>
public static class PhrasePayload
{
    public const string Marker = "|>PhraseV2<|";
    public const string TextMarker = "|>PhraseTextV2<|";
    private static readonly Regex PayloadPattern = new(
        @"\|>Phrase(?<text>Text)?V2<\|(?<token>[A-Za-z0-9_-]+)", RegexOptions.Compiled);

    public static string Encode(string russianName, string russianText, string englishName, string englishText)
        => EncodeCore(Marker, russianName, russianText, englishName, englishText);

    public static string EncodeText(string russianName, string russianText, string englishName, string englishText)
        => EncodeCore(TextMarker, russianName, russianText, englishName, englishText);

    private static string EncodeCore(
        string marker, string russianName, string russianText, string englishName, string englishText)
    {
        var json = JsonSerializer.Serialize(new[] { russianName, russianText, englishName, englishText });
        return marker + Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Resolve(string text, string language, bool includeLegacyMarker = true)
    {
        if (string.IsNullOrEmpty(text) ||
            (!text.Contains(Marker, StringComparison.Ordinal) &&
             !text.Contains(TextMarker, StringComparison.Ordinal))) return text;
        var english = GameLocalization.Normalize(language) == GameLocalization.English;
        return PayloadPattern.Replace(text, match =>
        {
            try
            {
                var values = Decode(match.Groups["token"].Value);
                var name = english ? values[2] : values[0];
                var phrase = english ? values[3] : values[1];
                var marker = includeLegacyMarker && !match.Groups["text"].Success ? "|>Phrase<|" : "";
                return $"{marker}{name}: {phrase}";
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[i18n] Invalid bilingual phrase payload: {exception.Message}");
                return "|>Phrase<|Ability: Ability triggered.";
            }
        });
    }

    public static string Protect(string text, string language, out IReadOnlyList<string> renderedPhrases)
    {
        var rendered = new List<string>();
        var protectedText = PayloadPattern.Replace(text, match =>
        {
            var index = rendered.Count;
            rendered.Add(Resolve(match.Value, language));
            return $"\uE000{index}\uE001";
        });
        renderedPhrases = rendered;
        return protectedText;
    }

    public static string Restore(string text, IReadOnlyList<string> renderedPhrases) =>
        Regex.Replace(text, "\\uE000(\\d+)\\uE001", match =>
            int.TryParse(match.Groups[1].Value, out var index) && index < renderedPhrases.Count
                ? renderedPhrases[index]
                : "");

    public static bool ContainsRussianPhrase(string text, string russianPhrase)
    {
        if (text.Contains(russianPhrase, StringComparison.Ordinal)) return true;
        foreach (Match match in PayloadPattern.Matches(text))
        {
            try
            {
                if (Decode(match.Groups["token"].Value)[1] == russianPhrase) return true;
            }
            catch
            {
                // A malformed old log must not prevent a new phrase from being selected.
            }
        }
        return false;
    }

    public static string MaskPassiveNames(
        string text, IReadOnlySet<string> canonicalNames, bool hidePhraseBody)
    {
        if (string.IsNullOrEmpty(text) || canonicalNames.Count == 0) return text;
        return PayloadPattern.Replace(text, match =>
        {
            try
            {
                var values = Decode(match.Groups["token"].Value);
                if (!canonicalNames.Contains(values[0])) return match.Value;
                var marker = match.Groups["text"].Success ? TextMarker : Marker;
                return hidePhraseBody
                    ? EncodeCore(marker, "Неизвестно", "Способность сработала.", "Unknown", "Ability triggered.")
                    : EncodeCore(marker, $"❓ {values[0]}", values[1], $"❓ {values[2]}", values[3]);
            }
            catch
            {
                return match.Value;
            }
        });
    }

    private static string[] Decode(string token)
    {
        var base64 = token.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        var values = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
        if (values is not { Length: 4 }) throw new InvalidDataException("Expected four phrase fields.");
        return values;
    }
}
