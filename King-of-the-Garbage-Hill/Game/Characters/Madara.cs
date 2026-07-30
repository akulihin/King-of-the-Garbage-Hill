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
    public const string BattleTaste = "Вкус битвы";
    public const string EternalTsukuyomiPhrase = "Узрите идеальный мир без войн.";
    public const string ThemeFile = "DataBase/sound/character_passives/madara/madara_tsukuemi_theme.mp3";
    public const int RoundEightBotReactionDelaySeconds = 30;
    public const int BattleTasteMaxMadaraAdvantage = 10;

    // Hidden Клоны Сусано rider: a score source, not a passive — it is deliberately absent from
    // characters.json so no player-facing description can leak it.
    public const string FearOfMadara = "Страх перед Мадарой";
    public const int FearOfMadaraPenalty = -5;
    public const int FearOfMadaraLateTurnPenalty = -1;

    public sealed class State
    {
        public int IncomingUniqueAttackersThisRound { get; set; }
        public HashSet<Guid> IncomingAttackerIdsThisRound { get; set; } = new();
        public int ResolvedFights { get; set; }
        public int RoundFightCountRound { get; set; }
        public int RoundFightCount { get; set; }
        public int RoundFightPhraseSentRound { get; set; }
        public int FirstFightPhraseRound { get; set; }
        public bool SecondFightPhrasePending { get; set; }
        public HashSet<Guid> RoundEightAttackers { get; set; } = new();
        public HashSet<Guid> RoundEightFightParticipants { get; set; } = new();
        public int RoundEightWins { get; set; }
        public int RoundEightLosses { get; set; }
        public bool RoundEightWasDefeated { get; set; }
        public bool RoundEightJusticeGranted { get; set; }
        public bool RoundNineResolved { get; set; }
        public Guid RedTigerPlayerId { get; set; }
        public bool TopOnePhraseSent { get; set; }
        public bool ThemeStarted { get; set; }
        public bool Sealed { get; set; }
        public bool EternalTsukuyomiActive { get; set; }
        public bool EternalTsukuyomiRoundPrepared { get; set; }
        public bool EternalTsukuyomiWinnerCaptured { get; set; }
        public Guid EternalTsukuyomiWinnerPlayerId { get; set; } = Guid.Empty;
        public Dictionary<Guid, List<Guid>> EternalTsukuyomiIllusoryTargets { get; set; } = new();
    }

    public static bool HasReanimatedBody(CharacterClass character) =>
        character?.Name == CharacterName
        && character.Passive.Any(passive => passive.PassiveName == ReanimatedBody);

    public static bool BlocksStatLoss(CharacterClass character, string source) =>
        HasReanimatedBody(character) && source != Cthulhu.Morok;

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

    public static bool IsEternalTsukuyomiEndingCaptured(GameClass game) =>
        Find(game)?.Passives.Madara.EternalTsukuyomiWinnerCaptured == true;

    public static bool TryCaptureEternalTsukuyomiEnding(GameClass game)
    {
        if (game?.RoundNo != 10) return false;

        var ordered = Naruto.OrderLeaderboard(game.PlayersList);
        var madara = ordered.FirstOrDefault(IsMadara);
        if (madara == null || madara.Passives.IsDead || madara.Passives.Madara.Sealed)
            return false;

        var state = madara.Passives.Madara;
        if (!state.EternalTsukuyomiActive
            && ordered.FirstOrDefault()?.GetPlayerId() != madara.GetPlayerId())
            return false;

        game.PlayersList = ordered;
        for (var index = 0; index < game.PlayersList.Count; index++)
        {
            game.PlayersList[index].Status.SetPlaceAtLeaderBoard(index + 1);
            game.PlayersList[index].Status.PlaceAtLeaderBoardHistory.Add(
                new InGameStatus.PlaceAtLeaderBoardHistoryClass(
                    game.RoundNo,
                    index + 1));
        }

        state.EternalTsukuyomiActive = true;
        state.EternalTsukuyomiWinnerCaptured = true;
        state.EternalTsukuyomiWinnerPlayerId =
            game.PlayersList.FirstOrDefault(player => !player.Passives.IsDead)?.GetPlayerId()
            ?? game.PlayersList.First().GetPlayerId();
        PrepareEternalTsukuyomiRound(game);
        game.IsFinished = true;
        return true;
    }

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
                    player => state.EternalTsukuyomiWinnerCaptured
                        ? new List<Guid>()
                        : player.Status.WhoToAttackThisTurn
                            .Where(targetId => targetId != player.GetPlayerId())
                            .ToList());
        }

        foreach (var player in game.PlayersList)
        {
            // The terminal capture opens no action window. Bug and a reserved Gordon keep their
            // presentation state only so the final projection can select the authoritative view.
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
        RecordFightAppearance(madara, game, state);

        // Страх перед Мадарой counts fights that actually happened, so a Block, a Skip or a fight
        // skipped by Наруто never spares an enemy from the penalty. Attack side included: a
        // Геральт contract can still make round-eight Madara the attacker.
        if (game.RoundNo == 8)
        {
            var opponentId = madara.Status.IsWonThisCalculation != Guid.Empty
                ? madara.Status.IsWonThisCalculation
                : madara.Status.IsLostThisCalculation;
            if (opponentId != Guid.Empty)
                state.RoundEightFightParticipants.Add(opponentId);
            if (madara.Status.IsLostThisCalculation != Guid.Empty)
                state.RoundEightWasDefeated = true;
        }

        if (!defense || game.RoundNo != 8) return;

        if (madara.Status.IsWonThisCalculation != Guid.Empty)
            state.RoundEightWins++;
        if (madara.Status.IsLostThisCalculation != Guid.Empty)
            state.RoundEightLosses++;
    }

    public static void RecordSpecialResolvedFight(
        GamePlayerBridgeClass madara,
        GameClass game)
    {
        if (!IsMadara(madara) || game == null) return;
        RecordFightAppearance(madara, game, madara.Passives.Madara);
    }

    private static void RecordFightAppearance(
        GamePlayerBridgeClass madara,
        GameClass game,
        State state)
    {
        if (state.RoundFightCountRound != game.RoundNo)
        {
            state.RoundFightCountRound = game.RoundNo;
            state.RoundFightCount = 0;
        }
        state.RoundFightCount++;
        state.ResolvedFights++;
        if (state.ResolvedFights == 1)
        {
            state.FirstFightPhraseRound = game.RoundNo;
            game.Phrases.MadaraFirstFight.SendLog(madara, false, isRandomOrder: false);
        }
        else if (state.ResolvedFights == 2)
        {
            if (state.FirstFightPhraseRound != game.RoundNo)
                game.Phrases.MadaraSecondFight.SendLog(madara, false, isRandomOrder: false);
            else
                state.SecondFightPhrasePending = true;
        }
    }

    public static void SendRoundTechniquePhrases(GamePlayerBridgeClass madara, GameClass game)
    {
        if (!IsMadara(madara)) return;

        var state = madara.Passives.Madara;
        if (state.RoundFightCountRound != game.RoundNo
            || state.RoundFightCount < 2
            || state.RoundFightPhraseSentRound == game.RoundNo)
            return;

        state.RoundFightPhraseSentRound = game.RoundNo;
        var phrase = Math.Min(state.RoundFightCount, 5) switch
        {
            2 => game.Phrases.MadaraTwoFights,
            3 => game.Phrases.MadaraThreeFights,
            4 => game.Phrases.MadaraFourFights,
            _ => game.Phrases.MadaraFiveFights,
        };
        phrase.SendLog(madara, false, isRandomOrder: false);
    }

    public static void SendDeferredFightPhrase(GamePlayerBridgeClass madara, GameClass game)
    {
        if (!IsMadara(madara)) return;

        var state = madara.Passives.Madara;
        if (!state.SecondFightPhrasePending || game.RoundNo <= state.FirstFightPhraseRound) return;

        state.SecondFightPhrasePending = false;
        game.Phrases.MadaraSecondFight.SendLog(madara, false, isRandomOrder: false);
    }

    public static void RewardBattleTaste(
        GamePlayerBridgeClass winner,
        GamePlayerBridgeClass opponent,
        GameClass game,
        decimal madaraStepOneAdvantage)
    {
        if (!IsMadara(winner)
            || opponent == null
            || winner.GetPlayerId() == opponent.GetPlayerId()
            || madaraStepOneAdvantage > BattleTasteMaxMadaraAdvantage)
            return;

        winner.Status.AddBonusPoints(1, BattleTaste);
        game.Phrases.MadaraBattleTaste.SendLog(winner, false);
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
            var scoreEntryCount = player.Status.ScoreEntries.Count;
            player.Status.AddBonusPoints(FearOfMadaraPenalty, FearOfMadara);
            if (player.Status.ScoreEntries.Count > scoreEntryCount)
                game.Phrases.MadaraFear.SendLog(player, false, isRandomOrder: false);
        }

        // A flawless Клоны Сусано event arms the hidden ending just like "all five attacked".
        // Mutually exclusive with the sealing branch below, which requires five losses.
        if (!state.Sealed && !state.RoundEightWasDefeated)
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

    public static void ApplyLateTurnFear(GameClass game)
    {
        if (game?.RoundNo is < 9 or > 10) return;

        var madara = Find(game);
        if (madara == null || madara.Passives.IsDead || madara.Passives.Madara.Sealed) return;

        var madaraId = madara.GetPlayerId();
        foreach (var player in game.PlayersList)
        {
            if (player.GetPlayerId() == madaraId || player.Passives.IsDead) continue;
            if (player.Status.WhoToAttackThisTurn.Contains(madaraId)) continue;
            player.Status.AddBonusPoints(FearOfMadaraLateTurnPenalty, FearOfMadara);
        }
    }

    public static void Seal(GamePlayerBridgeClass madara)
    {
        var state = madara.Passives.Madara;
        state.Sealed = true;
        state.EternalTsukuyomiActive = false;
        SetUnableToAct(madara);
    }

    public static decimal ApplySecondMeteoriteMoral(
        GamePlayerBridgeClass madara,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        if (!IsMadara(madara)
            || target == null
            || madara.IsTeamMember(game, target.GetPlayerId())
            || madara.GameCharacter.Passive.All(passive =>
                passive.PassiveName != SecondMeteorite))
            return 0;

        var before = target.GameCharacter.GetMoral();
        if (before <= 0) return 0;
        target.GameCharacter.AddMoral(-before, SecondMeteorite);
        return target.GameCharacter.GetMoral() - before;
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

    public static void EnforcePostRoundSevenBotPrediction(
        GamePlayerBridgeClass bot, GameClass game)
    {
        if (game?.RoundNo <= 7 || bot?.PlayerType != 404 || bot.Passives.IsDead
            || game.GameMode == "Aram" || IsMadara(bot)
            || bot.GameCharacter.DoomRollMode
            || bot.GameCharacter.Passive.Any(passive =>
                passive.PassiveName is "Тетрадь смерти" or "AdminPlayerType" or "Булькает")) return;

        var madara = Find(game);
        if (madara == null) return;

        var madaraId = madara.GetPlayerId();
        bot.AiKnowledge.PredictionEvidence[madaraId] = new BotPredictionEvidence
        {
            CharacterName = CharacterName,
            Confidence = 100,
            Evidence = "mandatory post-round-seven Madara prediction",
            RoundUpdated = game.RoundNo,
            IsExactReveal = true,
        };

        var currentPredictions = bot.Predict
            .Where(prediction => prediction.PlayerId == madaraId)
            .ToList();
        if (currentPredictions.Count != 1
            || currentPredictions[0].CharacterName != CharacterName)
        {
            bot.Predict.RemoveAll(prediction => prediction.PlayerId == madaraId);
            bot.Predict.Add(new PredictClass(CharacterName, madaraId));
        }
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
