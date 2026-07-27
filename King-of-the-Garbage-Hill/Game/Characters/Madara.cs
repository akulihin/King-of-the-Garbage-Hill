using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class Madara
{
    public const string CharacterName = "Мадара";
    public const string GodOfShinobi = "Бог шиноби";
    public const string ReanimatedBody = "Воскрешенное тело";
    public const string SecondMeteorite = "Второй метеорит";
    public const string SusanooClones = "Клоны Сусано";
    public const string EternalTsukuyomi = "Вечное Цукуеми";
    public const string EternalTsukuyomiPhrase = "Узрите идеальный мир без войн.";
    public const string ThemeFile = "DataBase/sound/character_passives/madara/madara_tsukuemi_theme.mp3";
    public const int RoundEightBotReactionDelaySeconds = 30;

    // Hidden Клоны Сусано rider: a score source, not a passive — it is deliberately absent from
    // characters.json so no player-facing description can leak it.
    public const string FearOfMadara = "Страх перед Мадарой";
    public const int FearOfMadaraPenalty = -2;

    public sealed class State
    {
        public int IncomingUniqueAttackersThisRound { get; set; }
        public HashSet<Guid> IncomingAttackerIdsThisRound { get; set; } = new();
        public int ResolvedFights { get; set; }
        public HashSet<Guid> RoundEightAttackers { get; set; } = new();
        public HashSet<Guid> RoundEightFightParticipants { get; set; } = new();
        public int RoundEightWins { get; set; }
        public int RoundEightLosses { get; set; }
        public bool RoundEightJusticeGranted { get; set; }
        public bool RoundNineResolved { get; set; }
        public Guid RedTigerPlayerId { get; set; }
        public bool TopOnePhraseSent { get; set; }
        public bool ThemeStarted { get; set; }
        public bool Sealed { get; set; }
        public bool EternalTsukuyomiActive { get; set; }
        public bool EternalTsukuyomiRoundPrepared { get; set; }
        public Dictionary<Guid, List<Guid>> EternalTsukuyomiIllusoryTargets { get; set; } = new();
    }

    public static bool HasReanimatedBody(CharacterClass character) =>
        character?.Name == CharacterName
        && character.Passive.Any(passive => passive.PassiveName == ReanimatedBody);

    public static bool IsMadara(GamePlayerBridgeClass player) =>
        player != null && HasReanimatedBody(player.GameCharacter);

    public static GamePlayerBridgeClass Find(GameClass game) =>
        game?.PlayersList.Find(IsMadara);

    public static bool IsSealed(GamePlayerBridgeClass player) =>
        IsMadara(player) && player.Passives.Madara.Sealed;

    public static bool IsRedTiger(GameClass game, GamePlayerBridgeClass player) =>
        game != null
        && player != null
        && Find(game)?.Passives.Madara.RedTigerPlayerId == player.GetPlayerId();

    public static bool IsEternalTsukuyomiActive(GameClass game)
    {
        var madara = Find(game);
        return madara != null
               && madara.Passives.Madara.EternalTsukuyomiActive
               && !madara.Passives.Madara.Sealed;
    }

    public static bool IsEternalTsukuyomiRound(GameClass game) =>
        game?.RoundNo == 10 && IsEternalTsukuyomiActive(game);

    public static bool PrepareEternalTsukuyomiRound(GameClass game)
    {
        if (!IsEternalTsukuyomiRound(game)) return false;

        var madara = Find(game);
        var state = madara!.Passives.Madara;
        if (!state.EternalTsukuyomiRoundPrepared)
        {
            state.EternalTsukuyomiRoundPrepared = true;
            madara.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                EternalTsukuyomi,
                EternalTsukuyomiPhrase,
                "Infinite Tsukuyomi",
                "Behold the perfect world without wars.") + "\n");
            state.EternalTsukuyomiIllusoryTargets = game.PlayersList
                .Where(player => player.GetPlayerId() != madara.GetPlayerId())
                .ToDictionary(
                    player => player.GetPlayerId(),
                    player => player.Status.WhoToAttackThisTurn
                        .Where(targetId => targetId != player.GetPlayerId())
                        .ToList());
        }

        foreach (var player in game.PlayersList)
        {
            // Terminal isolation: the world-wide skip cannot rewrite Bug's submitted action.
            if (UnknownBug.Is(player))
                continue;

            if (GordonFreeman.IsAwakeForEternalTsukuyomi(player, game))
            {
                player.Status.IsSkip = false;
                player.Status.IsSkipBreak = true;
                player.Status.ConfirmedSkip = true;
                continue;
            }

            player.Status.IsSkip = true;
            player.Status.TurnInterference = player.GetPlayerId() == madara.GetPlayerId()
                ? TurnInterferenceKind.Self
                : TurnInterferenceKind.Enemy;
            player.Status.IsBlock = false;
            player.Status.IsAutoMove = false;
            player.Status.IsReady = true;
            player.Status.ConfirmedSkip = true;
            player.Status.ConfirmedPredict = true;
            player.Status.IsAbleToChangeMind = false;
            player.Status.WhoToAttackThisTurn.Clear();
        }

        return true;
    }

    public static IReadOnlyList<Guid> GetIllusoryTargets(
        GameClass game, GamePlayerBridgeClass viewer)
    {
        var madara = Find(game);
        if (madara == null || viewer == null) return Array.Empty<Guid>();

        return madara.Passives.Madara.EternalTsukuyomiIllusoryTargets
            .TryGetValue(viewer.GetPlayerId(), out var targets)
            ? targets
            : Array.Empty<Guid>();
    }

    public static bool CanUseTooGood(GamePlayerBridgeClass player) =>
        !IsMadara(player) || player.Passives.Madara.IncomingUniqueAttackersThisRound > 1;

    public static bool CanUseTooStronk(GamePlayerBridgeClass player) =>
        !IsMadara(player) || player.Passives.Madara.IncomingUniqueAttackersThisRound > 2;

    // More than three unique attackers show them "a couple of techniques"; more than four — every
    // enemy at once — doubles that override. 0 means Бог шиноби adds no fight Skill at all.
    public static decimal GetGodOfShinobiSkill(GamePlayerBridgeClass player)
    {
        if (!IsMadara(player)) return 0;

        var attackers = player.Passives.Madara.IncomingUniqueAttackersThisRound;
        if (attackers > 4) return 200;
        return attackers > 3 ? 100 : 0;
    }

    // Клоны Сусано, round seven: every fight Madara takes part in is his win, and his own attack
    // may not be prevented by an enemy Block/Skip.
    public static bool IsRoundSevenAutoWin(GameClass game, GamePlayerBridgeClass player) =>
        game?.RoundNo == 7
        && IsMadara(player)
        && !player.Passives.Madara.Sealed
        && player.GameCharacter.Passive.Any(passive => passive.PassiveName == SusanooClones);

    public static void PrepareIncomingAttackers(GameClass game)
    {
        var madara = Find(game);
        if (madara == null) return;

        var attackers = game.PlayersList
            .Where(player => player.GetPlayerId() != madara.GetPlayerId())
            .Where(player => player.Status.WhoToAttackThisTurn.Contains(madara.GetPlayerId()))
            .Select(player => player.GetPlayerId())
            .ToHashSet();

        var state = madara.Passives.Madara;
        state.IncomingAttackerIdsThisRound = attackers;
        if (game.RoundNo == 8)
            state.RoundEightAttackers = attackers.ToHashSet();

        RefreshIncomingEffects(game, madara);
    }

    public static void RegisterIncomingAttacker(
        GameClass game, GamePlayerBridgeClass madara, GamePlayerBridgeClass attacker)
    {
        if (!IsMadara(madara) || attacker == null || attacker.GetPlayerId() == madara.GetPlayerId()) return;

        var state = madara.Passives.Madara;
        state.IncomingAttackerIdsThisRound.Add(attacker.GetPlayerId());
        if (game.RoundNo == 8)
            state.RoundEightAttackers.Add(attacker.GetPlayerId());

        RefreshIncomingEffects(game, madara);
    }

    private static void RefreshIncomingEffects(GameClass game, GamePlayerBridgeClass madara)
    {
        var state = madara.Passives.Madara;
        state.IncomingUniqueAttackersThisRound = state.IncomingAttackerIdsThisRound.Count;

        if (!state.Sealed && state.IncomingUniqueAttackersThisRound == game.PlayersList.Count - 1)
            state.EternalTsukuyomiActive = true;

        if (game.RoundNo == 8 && state.IncomingUniqueAttackersThisRound > 2
            && !state.RoundEightJusticeGranted)
        {
            state.RoundEightJusticeGranted = true;
            madara.GameCharacter.Justice.AddRealJusticeNow(1);
            madara.Status.AddInGamePersonalLogs($"|>Stat<|{SusanooClones}: +1 Справедливости\n");
        }
    }

    public static void RecordResolvedFight(GamePlayerBridgeClass madara, GameClass game, bool defense)
    {
        if (!IsMadara(madara)) return;
        if (madara.Status.IsWonThisCalculation == Guid.Empty
            && madara.Status.IsLostThisCalculation == Guid.Empty) return;

        var state = madara.Passives.Madara;
        state.ResolvedFights++;
        if (state.ResolvedFights == 1)
            game.Phrases.MadaraFirstFight.SendLog(madara, false, isRandomOrder: false);
        else if (state.ResolvedFights == 2)
            game.Phrases.MadaraSecondFight.SendLog(madara, false, isRandomOrder: false);

        // Страх перед Мадарой counts fights that actually happened, so a Block, a Skip or a fight
        // canceled by Наруто never spares an enemy from the penalty. Attack side included: a
        // Геральт contract can still make round-eight Madara the attacker.
        if (game.RoundNo == 8)
        {
            var opponentId = madara.Status.IsWonThisCalculation != Guid.Empty
                ? madara.Status.IsWonThisCalculation
                : madara.Status.IsLostThisCalculation;
            if (opponentId != Guid.Empty)
                state.RoundEightFightParticipants.Add(opponentId);
        }

        if (!defense || game.RoundNo != 8) return;

        if (madara.Status.IsWonThisCalculation != Guid.Empty)
            state.RoundEightWins++;
        if (madara.Status.IsLostThisCalculation != Guid.Empty)
            state.RoundEightLosses++;
    }

    public static void ResolveRoundNine(GamePlayerBridgeClass madara, GameClass game)
    {
        if (!IsMadara(madara) || game.RoundNo != 9) return;
        var state = madara.Passives.Madara;
        if (state.RoundNineResolved) return;
        state.RoundNineResolved = true;

        var attackers = state.RoundEightAttackers.Count;
        var wins = state.RoundEightWins;
        var losses = state.RoundEightLosses;

        // Страх перед Мадарой: cowardice during the Клоны Сусано event costs bonus points.
        // AddBonusPoints already refuses negatives for unknown_bug and a protected Homelander,
        // logs personally and keeps the score floor — the hidden mechanic adds no global line.
        foreach (var player in game.PlayersList)
        {
            if (player.GetPlayerId() == madara.GetPlayerId() || player.Passives.IsDead) continue;
            if (state.RoundEightFightParticipants.Contains(player.GetPlayerId())) continue;
            player.Status.AddBonusPoints(FearOfMadaraPenalty, FearOfMadara);
        }

        // A flawless Клоны Сусано event arms the hidden ending just like "all five attacked".
        // Mutually exclusive with the sealing branch below, which requires five losses.
        if (!state.Sealed && wins > 0 && losses == 0)
            state.EternalTsukuyomiActive = true;

        if (attackers == game.PlayersList.Count - 1 && losses >= 5)
        {
            game.AddGlobalLogs("Мадара: Не могу... поверить... Ведь я же... бог...");
            game.AddGlobalLogs("**Бессмертное тело Мадары было успешно запечатано!**");
            Seal(madara);
            return;
        }

        // Explicit designer override: several challengers and no Madara losses beats every other round-nine line.
        if (attackers > 1 && wins > 0 && losses == 0)
        {
            game.AddGlobalLogs("Мадара: Вам повезло, что вы не притащили сюда Хашираму. Вам придется перерисовывать меньше карт.");
            return;
        }

        if (attackers == game.PlayersList.Count - 1)
        {
            game.AddGlobalLogs("Мадара: Хахаха! Притащите сюда хоть всю объединенную армию шиноби! Без Хаширами некому меня остановить!");
            return;
        }

        if (attackers == 0)
        {
            game.AddGlobalLogs("Мадара: Какие же вы трусы... Новое поколение шиноби обрекают этот мир на вечное забвение.");
            return;
        }

        if (attackers == 1)
        {
            if (losses > 0)
                state.RedTigerPlayerId = state.RoundEightAttackers.Single();
            game.AddGlobalLogs(losses > 0
                ? "Мадара: Красный Тигр! Ты дрался достойно! Нарекаю тебя сильнейшим человеком на земле!"
                : "Мадара: Твои товарищи тебя бросили... Ведь в этом мире нет надежды, нет света. Только отчаяние.");
            return;
        }

        if (wins == 0 && losses > 0)
        {
            game.AddGlobalLogs("Мадара: Что ж, признаю, вам удалось меня развлечь.");
            return;
        }

        game.AddGlobalLogs("Мадара: Вам клонов с Сусано или без?");
    }

    public static void Seal(GamePlayerBridgeClass madara)
    {
        var state = madara.Passives.Madara;
        state.Sealed = true;
        state.EternalTsukuyomiActive = false;
        SetUnableToAct(madara);
    }

    public static void SetUnableToAct(GamePlayerBridgeClass madara)
    {
        madara.Status.IsSkip = false;
        madara.Status.TurnInterference = madara.Passives.Madara.Sealed
            ? TurnInterferenceKind.Enemy
            : TurnInterferenceKind.Self;
        madara.Status.IsBlock = false;
        madara.Status.IsAutoMove = false;
        madara.Status.IsReady = true;
        madara.Status.IsAbleToChangeMind = false;
        madara.Status.ConfirmedPredict = true;
        madara.Status.WhoToAttackThisTurn = new List<Guid>();
    }

    public static void ForceRoundEightBotPrediction(
        GamePlayerBridgeClass bot, GameClass game)
    {
        if (game?.RoundNo != 8 || bot?.PlayerType != 404 || bot.Passives.IsDead
            || game.GameMode == "Aram" || IsMadara(bot)
            || bot.GameCharacter.DoomRollMode
            || bot.GameCharacter.Passive.Any(passive =>
                passive.PassiveName is "Тетрадь смерти" or "AdminPlayerType" or "Булькает")) return;

        var madara = Find(game);
        if (madara == null || madara.Passives.Madara.Sealed) return;

        bot.Predict.RemoveAll(prediction => prediction.PlayerId == madara.GetPlayerId());
        bot.Predict.Add(new PredictClass(CharacterName, madara.GetPlayerId()));
        bot.Status.ConfirmedPredict = true;
    }

    public static bool MustAcceptRoundEightBotChallenge(
        GamePlayerBridgeClass bot, GameClass game)
    {
        if (game?.RoundNo != 8 || bot?.PlayerType != 404 || bot.Passives.IsDead
            || IsMadara(bot)) return false;

        var madara = Find(game);
        if (madara == null || madara.Passives.IsDead || madara.Passives.Madara.Sealed)
            return false;

        return bot.GameCharacter.Name is Naruto.CharacterName or Sakura.CharacterName or "Итачи";
    }

    public static void SanitizeSealedActions(GameClass game)
    {
        var madara = Find(game);
        if (madara == null || !madara.Passives.Madara.Sealed) return;

        var madaraId = madara.GetPlayerId();
        SetUnableToAct(madara);
        foreach (var player in game.PlayersList)
            player.Status.WhoToAttackThisTurn.RemoveAll(targetId => targetId == madaraId);
    }

    public static decimal GetIllusoryBonus(GameClass game, GamePlayerBridgeClass viewer)
    {
        var contenders = game.PlayersList
            .Where(player => !player.Passives.IsDead || player.GetPlayerId() == viewer.GetPlayerId())
            .ToList();
        var topScore = contenders.Max(player => player.Status.GetScore());
        var viewerScore = viewer.Status.GetScore();
        var viewerAlreadyWonAlone = !viewer.Passives.IsDead
                                    && viewer.Status.GetPlaceAtLeaderBoard() == 1
                                    && contenders.Count(player => player.Status.GetScore() == topScore) == 1;
        return viewerAlreadyWonAlone ? 0 : Math.Max(0, topScore - viewerScore + 1);
    }

    public static List<GamePlayerBridgeClass> GetIllusoryOrder(GameClass game, GamePlayerBridgeClass viewer)
    {
        if (IsMadara(viewer))
            return game.PlayersList.OrderBy(player => player.Status.GetPlaceAtLeaderBoard()).ToList();

        return new[] { viewer }
            .Concat(game.PlayersList
                .Where(player => player.GetPlayerId() != viewer.GetPlayerId())
                .OrderBy(player => player.Passives.IsDead)
                .ThenBy(player => player.Status.GetPlaceAtLeaderBoard()))
            .ToList();
    }

    public static string GetProjectedFinalLogs(GameClass game, GamePlayerBridgeClass viewer)
    {
        var realWinner = game.PlayersList
            .Where(player => !player.Passives.IsDead)
            .OrderBy(player => player.Status.GetPlaceAtLeaderBoard())
            .FirstOrDefault() ?? game.PlayersList.First();

        if (IsMadara(viewer))
            return $"Все игроки пропустили ход...\n\n**{realWinner.DiscordUsername}** победил, играя за **{realWinner.GameCharacter.Name}**";

        return $"**{viewer.DiscordUsername}** победил, играя за **{viewer.GameCharacter.Name}**";
    }
}
