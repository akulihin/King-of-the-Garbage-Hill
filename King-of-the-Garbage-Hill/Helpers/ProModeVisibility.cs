using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Helpers;

/// <summary>
/// One authority for Pro presentation. Gameplay keeps canonical, load-bearing passive names;
/// only a viewer projection may replace a still-foreign source with a question mark.
/// </summary>
public static class ProModeVisibility
{
    private static readonly HashSet<string> HistoricalOrModuleForeignSources = new(StringComparer.Ordinal)
    {
        // These sources can produce a receipt after they are no longer represented by a live card.
        "Ничего не понимает",
        DoomGuy.InfernalEnergy,
    };

    private static readonly HashSet<string> VisibleForeignPassiveSources = new(StringComparer.Ordinal)
    {
        // Pre-D16 public contracts.
        "Запах мусора",
        "Чернильная завеса",
        "Еврей",
        "2kxaoc",

        // Explicit designer verdicts from D16 (2026-08-05).
        "Сомнительная тактика",
        "Глаза Итачи",
        "Вечное Цукуеми",
        "Страх перед Мадарой",
        "Оковы Правосудия",
        "Кусь за жопу",
        "Впарить говна",
        "Выгодная сделка",
        "Большой куш",
        "Вступить в союз",
        "Get cancer",
        "Великий летописец",
        "Морок",
        "Скинул вазу",
        "Башня Vought",
        "M.M.",
        "Пейзаж конца света",
    };

    public static HashSet<string> GetHiddenPassiveNames(
        GamePlayerBridgeClass viewer,
        GameClass game,
        bool allowAdminBypass = true,
        bool forceProMode = false)
    {
        if (game == null
            || (!forceProMode && viewer?.IsProMode != true)
            || (!forceProMode && allowAdminBypass && viewer?.PlayerType == 2))
            return new HashSet<string>(StringComparer.Ordinal);

        foreach (var player in game.PlayersList)
            player.RememberCurrentPassiveSources();

        // A forced unauthenticated projection (live spectator/public replay) has no owner identity;
        // every non-public source is foreign there.
        var ownNames = forceProMode
            ? new HashSet<string>(StringComparer.Ordinal)
            : viewer?.GetRememberedPassiveSources().ToHashSet(StringComparer.Ordinal)
              ?? new HashSet<string>(StringComparer.Ordinal);
        // Fortress modules are runtime state rather than passive cards, but remain the owner's
        // own source after the module fires.
        if (!forceProMode && viewer?.GameCharacter.Name == DoomGuy.CharacterName)
            ownNames.Add(DoomGuy.InfernalEnergy);

        var hiddenNames = game.PlayersList
            .Where(other => forceProMode
                            || viewer == null
                            || other.GetPlayerId() != viewer.GetPlayerId())
            .SelectMany(other => other.GetRememberedPassiveSources())
            .Where(name => !ownNames.Contains(name) && !VisibleForeignPassiveSources.Contains(name))
            .ToHashSet(StringComparer.Ordinal);
        hiddenNames.UnionWith(HistoricalOrModuleForeignSources.Where(name => !ownNames.Contains(name)));
        hiddenNames.ExceptWith(VisibleForeignPassiveSources);
        return hiddenNames;
    }

    public static string MaskPersonalText(
        string text,
        GamePlayerBridgeClass viewer,
        GameClass game,
        bool hidePhraseBody = true,
        bool allowAdminBypass = true,
        bool forceProMode = false)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var hiddenNames = GetHiddenPassiveNames(
            viewer, game, allowAdminBypass, forceProMode);
        var enforceAudience = forceProMode
                              || viewer?.IsProMode == true
                              && !(allowAdminBypass && viewer.PlayerType == 2);
        if (hiddenNames.Count == 0
            && !(enforceAudience
                 && (text.Contains(PhrasePayload.OwnerMarker, StringComparison.Ordinal)
                     || text.Contains(PhrasePayload.ProNeutralMarker, StringComparison.Ordinal)
                     || text.Contains(PhrasePayload.ProNeutralSourceMarker, StringComparison.Ordinal))))
            return text;
        text = PhrasePayload.MaskPassiveNames(
            text,
            hiddenNames,
            hidePhraseBody,
            viewer?.GetPlayerId(),
            enforceAudience,
            forceProMode);
        text = PhrasePayload.ProtectEncoded(text, out var encodedPhrases);

        foreach (var passiveName in hiddenNames.OrderByDescending(name => name.Length))
            text = MaskLegacySource(text, passiveName);
        text = PhrasePayload.RestoreEncoded(text, encodedPhrases);
        return text;
    }

    public static string MaskScoreSource(
        string source,
        GamePlayerBridgeClass viewer,
        GameClass game,
        bool allowAdminBypass = true,
        bool forceProMode = false)
    {
        if (string.IsNullOrEmpty(source))
            return source;
        var canonicalSource = source.Trim().Trim("🐙".ToCharArray());
        return GetHiddenPassiveNames(viewer, game, allowAdminBypass, forceProMode)
            .Contains(canonicalSource)
            ? "❓"
            : source;
    }

    private static string MaskLegacySource(string text, string canonicalName)
    {
        // Legacy mutators write a canonical source at the beginning of a line (or score token).
        // Restrict masking to that grammar: arbitrary body text and player nicknames are data,
        // even when they happen to equal a passive such as L or Mute.
        var pattern =
            $@"(?<prefix>(?:^|[\r\n+])-?(?:\|>Stat<\|)?🐙*){Regex.Escape(canonicalName)}🐙*(?=\s*[:=+])";
        text = Regex.Replace(
            text,
            pattern,
            match => match.Groups["prefix"].Value + "❓",
            RegexOptions.CultureInvariant);

        // Settled regular points use a different legacy grammar:
        // `+N **обычных** очков (Source+Source)`. Parse only that terminal source list so an
        // authored parenthetical or a nickname elsewhere in the body can never be rewritten.
        return Regex.Replace(
            text,
            @"(?<prefix>(?:\*\*обычных\*\* очков|\*\*очков\*\*(?:\.\.\.|!\?)) \()(?<sources>[^)\r\n]*)(?<suffix>\))",
            match => match.Groups["prefix"].Value
                     + MaskSettledScoreTokens(match.Groups["sources"].Value, canonicalName)
                     + match.Groups["suffix"].Value,
            RegexOptions.CultureInvariant);
    }

    private static string MaskSettledScoreTokens(string sources, string canonicalName)
    {
        var tokens = sources.Split('+');
        for (var i = 0; i < tokens.Length; i++)
        {
            var canonicalToken = tokens[i]
                .Trim()
                .TrimStart('-')
                .Trim("🐙".ToCharArray());
            if (string.Equals(canonicalToken, canonicalName, StringComparison.Ordinal))
                tokens[i] = tokens[i].Replace(canonicalName, "❓", StringComparison.Ordinal);
        }
        return string.Join('+', tokens);
    }

}
