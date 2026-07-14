using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Discord;

namespace King_of_the_Garbage_Hill.Helpers;

/// <summary>
/// Presentation-only localization. Russian remains the canonical language of game state and
/// passive dispatch; text is translated only immediately before it is shown to a player.
/// </summary>
public static class GameLocalization
{
    public const string Russian = "ru";
    public const string English = "en";

    private static readonly ConcurrentDictionary<ulong, string> UserLanguages = new();
    private static readonly Lazy<Catalog> EnglishCatalog = new(LoadEnglishCatalog);

    // Translation is a pure function of (text, language, catalog), and the projection layer re-translates
    // the same bounded strings (character/passive/class names, stat labels) for every player of every round.
    // Memoize them; long one-off log blocks are not worth caching and are cheap anyway (see ReplaceTerm).
    private static readonly ConcurrentDictionary<(string Language, bool ResolveBilingual, string Text), string>
        TranslationCache = new();
    private const int TranslationCacheLimit = 50_000;
    private const int TranslationCacheMaxTextLength = 512;

    private static readonly Regex CyrillicProbe = new("[А-Яа-яЁё]", RegexOptions.Compiled);
    private static readonly Regex LatinProbe = new("[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex WordTermProbe = new(@"^[\p{L}\p{N}_.-]+$", RegexOptions.Compiled);

    private static readonly (Regex Pattern, string Replacement)[] PhraseRules =
    {
        (new Regex(@"Раунд\s*#(\d+)", RegexOptions.IgnoreCase), "Round #$1"),
        (new Regex(@"Раунд\s+(\d+)", RegexOptions.IgnoreCase), "Round $1"),
        (new Regex(@"\+(\d+)\s+очков", RegexOptions.IgnoreCase), "+$1 points"),
        (new Regex(@"(\d+)\s+очков", RegexOptions.IgnoreCase), "$1 points"),
        (new Regex(@"(\d+)\s+очка", RegexOptions.IgnoreCase), "$1 points"),
        (new Regex(@"(\d+)\s+очко", RegexOptions.IgnoreCase), "$1 point"),
        (new Regex(@"Обменять\s+(\d+)\s+Морали\s+на\s+(\d+)\s+бонусных очков", RegexOptions.IgnoreCase), "Trade $1 Moral for $2 bonus points"),
        (new Regex(@"Обменять\s+(\d+)\s+Морали\s+на\s+(\d+)\s+[CС]килла", RegexOptions.IgnoreCase), "Trade $1 Moral for $2 Skill"),
        (new Regex(@"(?:Вы|You)\s+поставили\s+(?:блок|Block)!", RegexOptions.IgnoreCase), "You blocked!"),
        (new Regex(@"(?:Вы|You)\s+поставили\s+(?:блок|Block)", RegexOptions.IgnoreCase), "You blocked"),
        (new Regex(@"(?:Вы|You)\s+использовали\s+(?:Авто Ход|Auto Move)!", RegexOptions.IgnoreCase), "You used Auto Move!"),
        (new Regex(@"(?:Вы|You)\s+использовали\s+(?:Авто Ход|Auto Move)", RegexOptions.IgnoreCase), "You used Auto Move"),
        (new Regex(@"Вы напали на игрока\s+", RegexOptions.IgnoreCase), "You attacked "),
        (new Regex(@"Вы напали на\s+", RegexOptions.IgnoreCase), "You attacked "),
        (new Regex(@"You напали на игрока\s+", RegexOptions.IgnoreCase), "You attacked "),
        (new Regex(@"за \*\*сильного\*\* врага", RegexOptions.IgnoreCase), "for a **strong** enemy"),
        (new Regex(@"за \*\*умного\*\* врага", RegexOptions.IgnoreCase), "for a **smart** enemy"),
        (new Regex(@"за \*\*быстрого\*\* врага", RegexOptions.IgnoreCase), "for a **fast** enemy"),
        (new Regex(@"за сильного врага", RegexOptions.IgnoreCase), "for a strong enemy"),
        (new Regex(@"за умного врага", RegexOptions.IgnoreCase), "for a smart enemy"),
        (new Regex(@"за быстрого врага", RegexOptions.IgnoreCase), "for a fast enemy"),
        (new Regex(@"Они скинули\s+(\*\*[^*]+\*\*|[^\r\n!]+)!\s*Сволочи!", RegexOptions.IgnoreCase), "They threw $1 off the hill! Bastards!"),
        (new Regex(@"Всё,\s*у меня\s+(?:горит|is burning)!", RegexOptions.IgnoreCase), "That's it, I'm absolutely tilted!"),
        (new Regex(@"(?:вас|you)\s+(?:обманул|outsmarted)", RegexOptions.IgnoreCase), "outsmarted you"),
        (new Regex(@"(?:вас|you)\s+(?:обогнал|overtook)", RegexOptions.IgnoreCase), "overtook you"),
        (new Regex(@"(?:вас|you)\s+(?:пресанул|pressured)", RegexOptions.IgnoreCase), "pressured you"),
        (new Regex(@"(?:Rumbling|Рокот Земли):\s*Эрен остался на\s+(\d+)\s+месте\.\s*Между ним и Элдией никого нет\.", RegexOptions.IgnoreCase),
            "Rumbling: Eren remains at place $1. No one stands between him and Eldia."),
        (new Regex(@"Mitsuki\s+отнял в общей сумме\s+(\d+(?:[.,]\d+)?)\s+(?:очков|points)\.", RegexOptions.IgnoreCase),
            "Mitsuki took away $1 points in total."),
        (new Regex(@"(.+?) наконец показал свою ИСТИННУЮ СИЛУ! ONE PUUUUUUNCH!!!", RegexOptions.IgnoreCase), "$1 finally unleashed their TRUE POWER! ONE PUUUUUUNCH!!!"),
        (new Regex(@"Игрок\s+(.+?)\s+победил", RegexOptions.IgnoreCase), "Player $1 won"),
        (new Regex(@"Ожидаем других игроков", RegexOptions.IgnoreCase), "Waiting for other players"),
        (new Regex(@"Вы не походили\. Использовался Авто Ход", RegexOptions.IgnoreCase), "You did not act. Auto Move was used"),
        (new Regex(@"#life:\s*Я прокачал (Интеллект|Силу|Скорость|Психику) на (-?\d+)!", RegexOptions.IgnoreCase), "#life: I upgraded $1 to $2!"),
        (new Regex(@"Подтвердите свои предложения перед атакой", RegexOptions.IgnoreCase), "Confirm your predictions before attacking"),
        (new Regex(@"Напасть на\s+", RegexOptions.IgnoreCase), "Attack "),
        (new Regex(@"Победа:\s*", RegexOptions.IgnoreCase), "Victory: "),
        (new Regex(@"Поражение:\s*", RegexOptions.IgnoreCase), "Defeat: "),
        (new Regex(@"Вы улучшили\s+(Интеллект|Силу|Скорость|Психику)\s+до\s+(-?\d+)", RegexOptions.IgnoreCase), "You upgraded $1 to $2"),
        (new Regex(@"Вам понерфали\s+(Интеллект|Силу|Скорость|Психику)\s+до\s+(-?\d+)", RegexOptions.IgnoreCase), "Your $1 was nerfed to $2"),
        (new Regex(@"Получено вреда:\s*(\d+)", RegexOptions.IgnoreCase), "Harm taken: $1"),
        (new Regex(@"Команда\s+#(\d+)\s+победила набрав\s+(-?\d+)\s+(?:Очков|points)", RegexOptions.IgnoreCase), "Team #$1 won with $2 points"),
        (new Regex(@"Команда\s+#(\d+)\s+Набрала\s+(-?\d+)\s+(?:Очков|points)", RegexOptions.IgnoreCase), "Team #$1 scored $2 points"),
        (new Regex(@"Шэн:\s*Перепрыгнул\s+(.+?)\.\s*Зарядов осталось:\s*(\d+)", RegexOptions.IgnoreCase), "Shen: jumped over $1. Charges left: $2"),
        (new Regex(@"Шэн:\s*Атака на\s+(.+?)\.\s*Цель уже позади, заряд потрачен\.\s*Осталось:\s*(\d+)", RegexOptions.IgnoreCase), "Shen: attacked $1. The target was already behind you; charge spent. Left: $2"),
        (new Regex(@"Шэн:\s*Зиккурат перекрыл прыжок через\s+(.+?)\.\s*Заряд потрачен", RegexOptions.IgnoreCase), "Shen: a Ziggurat blocked the jump over $1. Charge spent"),
        (new Regex(@"Временная капсула:\s*Кола найдена в переписанной истории!", RegexOptions.IgnoreCase), "Time Capsule: cola recovered from rewritten history!"),
        (new Regex(@"Великий летописец:\s*История раунда\s+(\d+)\s+переписана!\s*Украдено\s+(-?\d+)\s+(?:очков|points)", RegexOptions.IgnoreCase), "Great Chronicler: round $1 was rewritten! Stole $2 points"),
        (new Regex(@"Salldorum:\s*Помните\s+(\d+)\s+ход\?\s*На самом деле в этот день пришло подкрепление из Киева и мы всех победили!", RegexOptions.IgnoreCase), "Salldorum: Remember turn $1? Reinforcements from Kyiv actually arrived that day, and we defeated everyone!"),
        (new Regex(@"Salldorum:\s*А вы знали, что в\s+(\d+)\s+ход на самом деле мы подписали мирный договор и этих поражений не было", RegexOptions.IgnoreCase), "Salldorum: Did you know that on turn $1 we actually signed a peace treaty, so those defeats never happened"),
        (new Regex(@"Заказ Француза:\s*Новая цель\s*[—-]\s*(.+?)\.\s*3 хода", RegexOptions.IgnoreCase), "Frenchie's contract: new target — $1. 3 turns"),
        (new Regex(@"Тактика выбрана:\s*", RegexOptions.IgnoreCase), "Strategy selected: "),
        (new Regex(@"Ты стал пешкой Йохана", RegexOptions.IgnoreCase), "You became Johan's pawn"),
        (new Regex(@"Штормяк провоцирует вас!\s*Атакуйте\s+", RegexOptions.IgnoreCase), "Stormy taunts you! Attack "),
        (new Regex(@"Штормяк провоцирует\s+", RegexOptions.IgnoreCase), "Stormy taunts "),
        (new Regex(@"Тетрадь смерти:\s*Ты записал имя\s+", RegexOptions.IgnoreCase), "Death Note: you wrote the name "),
        (new Regex(@"Глаза бога смерти:\s*Активированы!\s*Следующая атака раскроет имя врага", RegexOptions.IgnoreCase), "Shinigami Eyes activated! Your next attack will reveal the enemy's name"),
    };

    public static string Normalize(string language) =>
        string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English : Russian;

    public static void SetUserLanguage(ulong userId, string language) =>
        UserLanguages[userId] = Normalize(language);

    public static string GetUserLanguage(ulong userId) =>
        UserLanguages.TryGetValue(userId, out var language) ? language : Russian;

    public static string TextForUser(ulong userId, string text) => Text(text, GetUserLanguage(userId));

    /// <summary>
    /// Localizes ordinary text for the web client while preserving replay-safe bilingual phrase
    /// records for Vue to resolve whenever the viewer changes language.
    /// </summary>
    public static string TextForClient(ulong userId, string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Memoized(text, GetUserLanguage(userId), false);
    }

    public static string Text(string text, string language)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return Memoized(text, Normalize(language), true);
    }

    private static string Memoized(string text, string language, bool resolveBilingualPhrases)
    {
        var catalog = EnglishCatalog.Value;
        if (text.Length > TranslationCacheMaxTextLength)
            return Translate(text, language, catalog, true, resolveBilingualPhrases);

        var key = (language, resolveBilingualPhrases, text);
        if (TranslationCache.TryGetValue(key, out var cached))
            return cached;

        var translated = Translate(text, language, catalog, true, resolveBilingualPhrases);
        if (TranslationCache.Count < TranslationCacheLimit)
            TranslationCache[key] = translated;
        return translated;
    }

    public static string PhraseForUser(ulong userId, string passiveName, string phrase)
    {
        var language = GetUserLanguage(userId);
        if (language == Russian) return phrase;
        return TranslatePhrase(passiveName, phrase, EnglishCatalog.Value);
    }

    private static string Translate(
        string text, string language, Catalog catalog, bool translatePhraseMarkers,
        bool resolveBilingualPhrases = true)
    {
        if (resolveBilingualPhrases &&
            (text.Contains(PhrasePayload.Marker, StringComparison.Ordinal) ||
             text.Contains(PhrasePayload.TextMarker, StringComparison.Ordinal)))
        {
            var protectedText = PhrasePayload.Protect(text, language, out var renderedPhrases);
            return PhrasePayload.Restore(
                Translate(protectedText, language, catalog, translatePhraseMarkers, false),
                renderedPhrases);
        }
        if (language == English && !CyrillicProbe.IsMatch(text) &&
            !text.Contains("|>Phrase<|", StringComparison.Ordinal))
            return text;
        if (language == Russian && !LatinProbe.IsMatch(text))
            return text;

        if (translatePhraseMarkers && language == English && text.Contains("|>Phrase<|", StringComparison.Ordinal))
        {
            text = PhraseLocalization.ResolveLegacyMarkers(text, English);
            text = Regex.Replace(text, @"\|>Phrase<\|([^:\r\n]+):\s*([^\r\n]*)", match =>
                $"|>Phrase<|{Translate(match.Groups[1].Value, English, catalog, false)}: " +
                TranslatePhrase(match.Groups[1].Value, match.Groups[2].Value, catalog));
        }

        var leadingLength = text.Length - text.TrimStart().Length;
        var trailingLength = text.Length - text.TrimEnd().Length;
        var coreLength = text.Length - leadingLength - trailingLength;
        var core = coreLength > 0 ? text.Substring(leadingLength, coreLength) : text.Trim();
        if (Normalize(language) == Russian)
        {
            if (catalog.RussianExact.TryGetValue(core, out var russian))
                return text[..leadingLength] + russian + (trailingLength > 0 ? text[^trailingLength..] : "");

            return ReplaceCatalogEntries(text, catalog.SortedRussianExact);
        }
        if (catalog.Exact.TryGetValue(core, out var exact))
            return text[..leadingLength] + exact + (trailingLength > 0 ? text[^trailingLength..] : "");

        // Logs and dynamic UI strings commonly contain several catalogued phrases in one value.
        // Match dynamic canonical templates first, then apply longest exact fragments and terms.
        var translated = text;
        foreach (var (pattern, replacement) in PhraseRules)
            translated = pattern.Replace(translated, replacement);
        translated = ReplaceCatalogEntries(translated, catalog.SortedExact);
        translated = ReplaceCatalogEntries(translated, catalog.SortedTerms);
        return translated;
    }

    private static string TranslatePhrase(string passiveName, string phrase, Catalog catalog)
    {
        if (PhraseLocalization.TryTranslateLegacy(passiveName, phrase, English, out var authored))
            return authored;

        var translated = Translate(phrase, English, catalog, false);
        if (!Regex.IsMatch(translated, "[А-Яа-яЁё]")) return translated;

        if (catalog.PhraseFallbacks.TryGetValue(passiveName, out var adapted))
            return adapted;

        // Old replay snapshots can contain a passive title that an earlier projection translated
        // while leaving its phrase canonical. Resolve that display name back to the shared fallback.
        var canonicalPassive = catalog.PhraseFallbacks.Keys.FirstOrDefault(key =>
            string.Equals(Translate(key, English, catalog, false), passiveName, StringComparison.Ordinal));
        return canonicalPassive != null
            ? catalog.PhraseFallbacks[canonicalPassive]
            : "Ability triggered.";
    }

    private static string ReplaceCatalogEntries(string text, IReadOnlyList<CatalogEntry> entries)
    {
        var translated = text;
        foreach (var entry in entries)
            translated = ReplaceTerm(translated, entry);
        return translated;
    }

    /// <summary>
    /// Applies one catalog entry. Both branches below (ordinal Replace and the case-sensitive
    /// word-boundary Regex) can only match when the source occurs literally in the text, so the
    /// ordinal scan is an exact pre-filter: it skips the overwhelming majority of the ~1000 catalog
    /// entries for any given string without changing the result. The boundary Regex is compiled once
    /// per entry (see <see cref="CatalogEntry"/>) — building it per call funnelled ~1000 distinct
    /// patterns through the 15-entry static Regex cache, so nearly every call recompiled its pattern.
    /// </summary>
    private static string ReplaceTerm(string text, CatalogEntry entry)
    {
        if (!text.Contains(entry.Source, StringComparison.Ordinal))
            return text;
        if (entry.Boundary == null)
            return text.Replace(entry.Source, entry.Replacement, StringComparison.Ordinal);
        return entry.Boundary.Replace(text, entry.Evaluator);
    }

    /// <summary>One catalog row with its matching strategy resolved once, at load time.</summary>
    private sealed class CatalogEntry
    {
        public CatalogEntry(string source, string replacement)
        {
            Source = source;
            Replacement = replacement;
            if (!WordTermProbe.IsMatch(source)) return;
            Boundary = new Regex(
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(source)}(?![\p{{L}}\p{{N}}])", RegexOptions.Compiled);
            Evaluator = _ => replacement;
        }

        public string Source { get; }
        public string Replacement { get; }

        /// <summary>Null for entries replaced ordinally (phrases); set for single-token terms.</summary>
        public Regex Boundary { get; }
        public MatchEvaluator Evaluator { get; }
    }

    public static EmbedBuilder EmbedForUser(ulong userId, EmbedBuilder embed) =>
        LocalizeEmbed(embed, GetUserLanguage(userId));

    public static EmbedBuilder LocalizeEmbed(EmbedBuilder embed, string language)
    {
        if (Normalize(language) == Russian) return embed;
        if (embed.Title != null) embed.Title = Text(embed.Title, language);
        if (embed.Description != null) embed.Description = Text(embed.Description, language);
        if (embed.Author?.Name != null) embed.Author.Name = Text(embed.Author.Name, language);
        if (embed.Footer?.Text != null) embed.Footer.Text = Text(embed.Footer.Text, language);
        foreach (var field in embed.Fields)
        {
            field.Name = Text(field.Name, language);
            if (field.Value is string value) field.Value = Text(value, language);
        }
        return embed;
    }

    public static ComponentBuilder ComponentsForUser(ulong userId, ComponentBuilder builder)
    {
        var language = GetUserLanguage(userId);
        if (language == Russian) return builder;
        foreach (var row in builder.ActionRows)
        foreach (var component in row.Components)
        {
            switch (component)
            {
                case ButtonBuilder button when button.Label != null:
                    button.Label = Text(button.Label, language);
                    break;
                case SelectMenuBuilder select:
                    if (select.Placeholder != null) select.Placeholder = Text(select.Placeholder, language);
                    foreach (var option in select.Options)
                    {
                        option.Label = Text(option.Label, language);
                        if (option.Description != null)
                            option.Description = Text(option.Description, language);
                        // option.Value is deliberately canonical and must never be translated.
                    }
                    break;
            }
        }
        return builder;
    }

    public static MessageComponent ComponentsForUser(ulong userId, MessageComponent components)
    {
        if (components == null || GetUserLanguage(userId) == Russian) return components;
        var builder = ComponentBuilder.FromComponents(components.Components);
        return ComponentsForUser(userId, builder).Build();
    }

    private static Catalog LoadEnglishCatalog()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "DataBase", "localization.en.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "DataBase", "localization.en.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "King-of-the-Garbage-Hill", "DataBase", "localization.en.json"),
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path == null)
        {
            Console.WriteLine("[i18n] DataBase/localization.en.json was not found; English falls back to canonical text.");
            return new Catalog();
        }

        try
        {
            var result = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new Catalog();
            AddCharacterContent(result, path);
            AddPhraseContent(result);
            result.BuildSortedTerms();
            Console.WriteLine($"[i18n] Loaded {result.Exact.Count} exact phrases and {result.Terms.Count} terms.");
            return result;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[i18n] Failed to load English catalog: {exception.Message}");
            return new Catalog();
        }
    }

    private static void AddCharacterContent(Catalog catalog, string catalogPath)
    {
        var characterPath = Path.Combine(Path.GetDirectoryName(catalogPath) ?? "", "characters.json");
        if (!File.Exists(characterPath)) return;
        var characters = JsonSerializer.Deserialize<List<CharacterContent>>(File.ReadAllText(characterPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CharacterContent>();
        foreach (var character in characters)
        {
            if (!string.IsNullOrEmpty(character.Description)
                && catalog.Characters.TryGetValue(character.Name ?? "", out var description))
                catalog.Exact[character.Description] = description;
            foreach (var passive in character.Passive)
            {
                if (!string.IsNullOrEmpty(passive.PassiveDescription)
                    && catalog.Passives.TryGetValue(passive.PassiveName ?? "", out var passiveDescription))
                    catalog.Exact[passive.PassiveDescription] = passiveDescription;
            }
        }
    }

    private static void AddPhraseContent(Catalog catalog)
    {
        foreach (var (russian, english) in PhraseLocalization.GetUnambiguousPairs(true))
            catalog.Exact.TryAdd(russian, english);
        foreach (var (english, russian) in PhraseLocalization.GetUnambiguousPairs(false))
            catalog.RussianExact.TryAdd(english, russian);
    }

    private sealed class Catalog
    {
        public Dictionary<string, string> Exact { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Terms { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> RussianExact { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> PhraseFallbacks { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Characters { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Passives { get; set; } = new(StringComparer.Ordinal);
        public List<CatalogEntry> SortedExact { get; private set; } = new();
        public List<CatalogEntry> SortedRussianExact { get; private set; } = new();
        public List<CatalogEntry> SortedTerms { get; private set; } = new();

        public void BuildSortedTerms()
        {
            SortedExact = Prepare(Exact);
            SortedRussianExact = Prepare(RussianExact);
            SortedTerms = Prepare(Terms);
        }

        // Longest-first ordering is load-bearing: a longer phrase must win over a shorter fragment of it.
        private static List<CatalogEntry> Prepare(Dictionary<string, string> entries) =>
            entries.OrderByDescending(x => x.Key.Length)
                .Select(x => new CatalogEntry(x.Key, x.Value))
                .ToList();
    }

    private sealed class CharacterContent
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<PassiveContent> Passive { get; set; } = new();
    }

    private sealed class PassiveContent
    {
        public string PassiveName { get; set; }
        public string PassiveDescription { get; set; }
    }
}
