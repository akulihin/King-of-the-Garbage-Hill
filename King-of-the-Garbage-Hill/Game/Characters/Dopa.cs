using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class Dopa
{
    public const string CharacterName = "Dopa";
    public const string Macro = "Макро";
    public const string Vision = "Взгляд в будущее";
    public const string Permaban = "Permaban";
    public const string Meta = "Законодатель меты";

    public class MacroClass
    {
        public int FightsProcessed { get; set; } = 0;
        public int FightsResolved { get; set; } = 0;
        public int DuplicateTargetSkipRound { get; set; }
        public Guid DeducedTargetId { get; set; } = Guid.Empty;
        public string DeducedCharacterName { get; set; } = "";
    }

    public class VisionClass
    {
        public int Cooldown { get; set; } = 0;
    }

    public class MetaChoiceClass
    {
        public bool Triggered { get; set; } = false;
        public string ChosenTactic { get; set; } = "";
        public int StatLevelUpsTaken { get; set; } = 0;
    }

    public static bool HasMacro(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName
        && player.GameCharacter.Passive.Any(passive => passive.PassiveName == Macro);

    public static void ActivateDuplicateTargetSkip(
        GamePlayerBridgeClass player,
        GamePlayerBridgeClass target,
        GameClass game,
        IEnumerable<CharacterClass> visibleCharacters)
    {
        if (!HasMacro(player) || target == null || game == null) return;

        var isSecretTarget =
            target.GameCharacter.Tier == -1
            || target.GameCharacter.Name == "Монстр без имени";
        var prediction = target.GameCharacter.Name;
        if (isSecretTarget)
        {
            var wrongCandidateCharacters = visibleCharacters
                .Where(character =>
                    character.Name != target.GameCharacter.Name
                    && character.Name != "Sakura"
                    && (!character.TeamModeOnly || game.Teams.Count > 0))
                .GroupBy(character => character.Name)
                .Select(group => group.First())
                .ToList();
            var wrongCandidates = wrongCandidateCharacters
                .Select(character => character.Name)
                .ToList();
            var existingBotHypothesis = player.PlayerType == 404
                ? BotInformation.PredictionFor(player, target.GetPlayerId())?.CharacterName
                : null;
            prediction = !string.IsNullOrWhiteSpace(existingBotHypothesis)
                         && wrongCandidates.Contains(existingBotHypothesis, StringComparer.Ordinal)
                ? existingBotHypothesis
                : player.PlayerType == 404 && wrongCandidateCharacters.Count > 0
                    ? wrongCandidateCharacters
                        .Where(character => character.Tier == wrongCandidateCharacters.Max(candidate => candidate.Tier))
                        .OrderBy(character => character.Name, StringComparer.Ordinal)
                        .First().Name
                : wrongCandidates.Count > 0
                    ? wrongCandidates[SecureRandom.Next(0, wrongCandidates.Count - 1)]
                    : CharacterName;
        }

        var state = player.Passives.DopaMacro;
        state.DuplicateTargetSkipRound = game.RoundNo;
        state.DeducedTargetId = target.GetPlayerId();
        state.DeducedCharacterName = prediction;
        ReassertMacroPrediction(player);

        player.Status.WhoToAttackThisTurn.Clear();
        player.Status.IsBlock = false;
        player.Status.IsSkip = true;
        player.Status.TurnInterference = TurnInterferenceKind.Self;
        player.Status.ConfirmedSkip = true;
        player.Status.IsReady = true;
        player.Status.AddInGamePersonalLogs(
            $"{Macro}: две одинаковые цели — ход пропущен; личность {target.DiscordUsername} вычислена.\n");
    }

    public static void EnforceDuplicateTargetSkip(GameClass game)
    {
        if (game == null) return;

        foreach (var player in game.PlayersList.Where(player =>
                     player.Passives.DopaMacro.DuplicateTargetSkipRound == game.RoundNo))
        {
            player.Status.WhoToAttackThisTurn.Clear();
            player.Status.IsBlock = false;
            player.Status.IsSkip = true;
            player.Status.TurnInterference = TurnInterferenceKind.Self;
            player.Status.ConfirmedSkip = true;
            player.Status.IsReady = true;
            ReassertMacroPrediction(player);
        }
    }

    public static bool IsDuplicateTargetSkip(
        GamePlayerBridgeClass player,
        GameClass game) =>
        player?.Passives?.DopaMacro?.DuplicateTargetSkipRound == game?.RoundNo;

    public static void ReassertMacroPrediction(GamePlayerBridgeClass player)
    {
        if (player?.Passives?.DopaMacro == null) return;

        var state = player.Passives.DopaMacro;
        if (state.DeducedTargetId == Guid.Empty
            || string.IsNullOrWhiteSpace(state.DeducedCharacterName))
            return;

        BotInformation.ReplacePrediction(
            player,
            state.DeducedTargetId,
            state.DeducedCharacterName,
            100,
            "Dopa Macro deduction",
            state.DuplicateTargetSkipRound);
    }
}
