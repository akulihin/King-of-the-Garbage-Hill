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
        (new Regex(@"Вы напали на игрока\s+", RegexOptions.IgnoreCase), "You attacked "),
        (new Regex(@"Вы напали на\s+", RegexOptions.IgnoreCase), "You attacked "),
        (new Regex(@"Игрок\s+(.+?)\s+победил", RegexOptions.IgnoreCase), "Player $1 won"),
        (new Regex(@"Ожидаем других игроков", RegexOptions.IgnoreCase), "Waiting for other players"),
        (new Regex(@"Вы не походили\. Использовался Авто Ход", RegexOptions.IgnoreCase), "You did not act. Auto Move was used"),
        (new Regex(@"Подтвердите свои предложения перед атакой", RegexOptions.IgnoreCase), "Confirm your predictions before attacking"),
        (new Regex(@"Напасть на\s+", RegexOptions.IgnoreCase), "Attack "),
        (new Regex(@"Победа:\s*", RegexOptions.IgnoreCase), "Victory: "),
        (new Regex(@"Поражение:\s*", RegexOptions.IgnoreCase), "Defeat: "),
        (new Regex(@"Вы улучшили\s+(Интеллект|Силу|Скорость|Психику)\s+до\s+(-?\d+)", RegexOptions.IgnoreCase), "You upgraded $1 to $2"),
        (new Regex(@"Вам понерфали\s+(Интеллект|Силу|Скорость|Психику)\s+до\s+(-?\d+)", RegexOptions.IgnoreCase), "Your $1 was nerfed to $2"),
        (new Regex(@"Получено вреда:\s*(\d+)", RegexOptions.IgnoreCase), "Harm taken: $1"),
        (new Regex(@"Команда\s+#(\d+)\s+победила набрав\s+(-?\d+)\s+(?:Очков|points)", RegexOptions.IgnoreCase), "Team #$1 won with $2 points"),
        (new Regex(@"Команда\s+#(\d+)\s+Набрала\s+(-?\d+)\s+(?:Очков|points)", RegexOptions.IgnoreCase), "Team #$1 scored $2 points"),
        (new Regex(@"Шэн:\s*Активирован на позицию\s+(\d+)\.\s*Зарядов:\s*(\d+)", RegexOptions.IgnoreCase), "Shen: activated at position $1. Charges: $2"),
        (new Regex(@"Шэн:\s*Деактивирован\.\s*Заряд возвращён", RegexOptions.IgnoreCase), "Shen: deactivated. Charge refunded"),
        (new Regex(@"Великий летописец:\s*История раунда\s+(\d+)\s+переписана!\s*Украдено\s+(-?\d+)\s+(?:очков|points)", RegexOptions.IgnoreCase), "Great Chronicler: round $1 was rewritten! Stole $2 points"),
        (new Regex(@"Salldorum переписал историю раунда\s+(\d+)", RegexOptions.IgnoreCase), "Salldorum rewrote round $1"),
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

    public static string Text(string text, string language)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var catalog = EnglishCatalog.Value;
        return Translate(text, Normalize(language), catalog, true);
    }

    public static string PhraseForUser(ulong userId, string passiveName, string phrase)
    {
        var language = GetUserLanguage(userId);
        if (language == Russian) return phrase;
        return TranslatePhrase(passiveName, phrase, EnglishCatalog.Value);
    }

    private static string Translate(string text, string language, Catalog catalog, bool translatePhraseMarkers)
    {
        if (language == English && !Regex.IsMatch(text, "[А-Яа-яЁё]"))
            return text;
        if (language == Russian && !Regex.IsMatch(text, "[A-Za-z]"))
            return text;

        if (translatePhraseMarkers && language == English && text.Contains("|>Phrase<|", StringComparison.Ordinal))
        {
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
        foreach (var (russian, english) in catalog.SortedTerms)
            translated = ReplaceTerm(translated, russian, english);
        return translated;
    }

    private static string TranslatePhrase(string passiveName, string phrase, Catalog catalog)
    {
        var translated = Translate(phrase, English, catalog, false);
        if (!Regex.IsMatch(translated, "[А-Яа-яЁё]")) return translated;

        var passive = Translate(passiveName, English, catalog, false);
        var adapted = passiveName switch
        {
            "Авто Ход" => "No hands on the keyboard? Fine. I'll do it myself.",
            "Справедливость" => "Justice is on our side. Probably.",
            "Вампуризм" or "Гематофагия" => "Just a tiny bite. You will barely notice.",
            "СОсиновый кол" => "Garlic, stakes... people take all the fun out of dinner.",
            "Тигр топ, а ты холоп" => "Tiger on top. Everyone else knows their place.",
            "Стримснайпят и банят и банят и банят" => "Banned again. Worth it.",
            "Безумие" => "The plan makes perfect sense if you stop thinking about it.",
            "Стёб" or "__Стёб__" => "All according to the plan inside the plan.",
            "Сомнительная тактика" => "Trust me: this terrible idea is secretly brilliant.",
            "Месть" => "Revenge tastes better than victory.",
            "Буль" => "Blub. The water is perfectly safe.",
            "Испанец" => "The bull is angry. The Spaniard is louder.",
            "Спящее хуйло" => "Five more minutes... then Challenger.",
            "Претендент русского сервера" => "The old Challenger has opened one eye.",
            "Я щас приду" => "Be right there. Any minute now.",
            "Я за чаем" => "Tea is ready. Staying awake is optional.",
            "Еврей" => "A perfectly legitimate redistribution of points.",
            "Булинг" => "Why are you bullying me?",
            "Гребанные ассассины" => "Goddamn assassins. Every single time.",
            "Импакт" => "Impact secured. Nobody saw it, naturally.",
            "Подсчет" => "I calculated everything. The numbers owe me money.",
            "Раммус мейн" => "Okay.",
            "Одиночество" => "Look at all my friends not answering.",
            "Доебаться" => "You have 37 unread messages.",
            "Заводить друзей" => "Congratulations. We are friends now.",
            "Дракон" => "ROAR. No, louder: ROAR!",
            "Много выебывается" => "Big talk from the top of the hill.",
            "Запах мусора" => "Something smells like free points.",
            "Научите играть" => "Wait, which button makes me good?",
            "Произошел троллинг" => "An unfortunate amount of trolling occurred.",
            "АФКА" => "AFK. Spiritually and mechanically.",
            "Мне (не)везет" or "Повезло" or "Не повезло" => "Luck is a skill. Bad luck is the team's fault.",
            "Лежит на дне" => "Rock bottom has excellent visibility.",
            "Челюсти" => "The shark has remembered it has teeth.",
            "Клинки хаоса" or "Охота на богов" => "If Olympus denies my vengeance, Olympus will fall.",
            "Лысина" => "Serious Series: one very ordinary punch.",
            "Огурчик Рик" => "I'M PICKLE RIIIICK!",
            "Портальная пушка" => "Portal open, Morty. Try not to touch reality.",
            "Тетрадь смерти" => "Delete. Delete. Delete.",
            "Гений" => "Everything is going according to plan, Ryuk.",
            "Вороны" => "There is always another crow up the sleeve.",
            "Аматерасу" => "Black flames do not ask twice.",
            "Глаза Итачи" => "You were already inside the illusion.",
            "Впарить говна" => "Heh heh... today only, no refunds.",
            "Закуп" or "Выгодная сделка" => "A fine investment. For me.",
            "Макро" or "Взгляд в будущее" => "The next two turns were decided three turns ago.",
            "Тоннели Гоблинов" => "Run away! Tactically!",
            "Гоблины" => "More Goblins for the Goblin god!",
            "Отличный рудник" => "A fine mine! Definitely ours now.",
            "Гоблины тупые, но не идиоты" => "The Ziggurat works. Nobody ask how.",
            "Минька" => "*purrs with complete innocence*",
            "Штормяк" or "Кошачья засада" => "MEOW! *chooses violence*",
            "Монстр" or "Пейзаж конца света" => "Look at the monster inside you.",
            "Пацаны" => "The Boys are back in business.",
            "Временная капсула" => "The cola is still cold. History checks out.",
            "Великий летописец" => "History is written by whoever edits last.",
            "Ведьмачьи заказы" => "Another monster, another coin.",
            "Медитация" => "Wind's howling. Garbage too.",
            "Шевелись, Плотва" => "Roach! How did you get on that roof?",
            "DooM Guy" => "Rip and tear. Until it is done.",
            _ => $"{passive}: ability triggered.",
        };
        return adapted;
    }

    private static string ReplaceCatalogEntries(
        string text, IReadOnlyList<KeyValuePair<string, string>> entries)
    {
        var translated = text;
        foreach (var (source, replacement) in entries)
            translated = ReplaceTerm(translated, source, replacement);
        return translated;
    }

    private static string ReplaceTerm(string text, string source, string translation)
    {
        if (!Regex.IsMatch(source, @"^[\p{L}\p{N}_.-]+$"))
            return text.Replace(source, translation, StringComparison.Ordinal);
        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(source)}(?![\p{{L}}\p{{N}}])";
        return Regex.Replace(text, pattern, _ => translation);
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

    private sealed class Catalog
    {
        public Dictionary<string, string> Exact { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Terms { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> RussianExact { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Characters { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Passives { get; set; } = new(StringComparer.Ordinal);
        public List<KeyValuePair<string, string>> SortedExact { get; private set; } = new();
        public List<KeyValuePair<string, string>> SortedRussianExact { get; private set; } = new();
        public List<KeyValuePair<string, string>> SortedTerms { get; private set; } = new();

        public void BuildSortedTerms()
        {
            SortedExact = Exact.OrderByDescending(x => x.Key.Length).ToList();
            SortedRussianExact = RussianExact.OrderByDescending(x => x.Key.Length).ToList();
            SortedTerms = Terms.OrderByDescending(x => x.Key.Length).ToList();
        }
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
