using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class Madara
{
    public const string CharacterName = "Мадара";
    public const string GodOfShinobi = "Бог шиноби";
    public const string ReanimatedBody = "Воскрешенное тело";
    public const string SecondMeteorite = "Второй метеорит";
    public const string SusanooClones = "Клоны Сусано";
    public const string EternalTsukuyomi = "Вечное Цукуеми";
    public const string ThemeFile = "DataBase/sound/character_passives/madara/madara_tsukuemi_theme.mp3";

    public sealed class State
    {
        public int IncomingUniqueAttackersThisRound { get; set; }
        public HashSet<Guid> IncomingAttackerIdsThisRound { get; set; } = new();
        public int ResolvedFights { get; set; }
        public HashSet<Guid> RoundEightAttackers { get; set; } = new();
        public int RoundEightWins { get; set; }
        public int RoundEightLosses { get; set; }
        public bool RoundEightJusticeGranted { get; set; }
        public bool RoundNineResolved { get; set; }
        public bool TopOnePhraseSent { get; set; }
        public bool ThemeStarted { get; set; }
        public bool Sealed { get; set; }
        public bool EternalTsukuyomiActive { get; set; }
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

    public static bool IsEternalTsukuyomiActive(GameClass game)
    {
        var madara = Find(game);
        return madara != null
               && madara.Passives.Madara.EternalTsukuyomiActive
               && !madara.Passives.Madara.Sealed;
    }

    public static bool CanUseTooGood(GamePlayerBridgeClass player) =>
        !IsMadara(player) || player.Passives.Madara.IncomingUniqueAttackersThisRound > 1;

    public static bool CanUseTooStronk(GamePlayerBridgeClass player) =>
        !IsMadara(player) || player.Passives.Madara.IncomingUniqueAttackersThisRound > 2;

    public static bool ShouldUseHundredSkill(GamePlayerBridgeClass player) =>
        IsMadara(player) && player.Passives.Madara.IncomingUniqueAttackersThisRound > 3;

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
        madara.Status.IsBlock = false;
        madara.Status.IsAutoMove = false;
        madara.Status.IsReady = true;
        madara.Status.IsAbleToChangeMind = false;
        madara.Status.ConfirmedPredict = true;
        madara.Status.WhoToAttackThisTurn = new List<Guid>();
    }

    public static void PrepareRoundEightBotChallenges(GameClass game)
    {
        if (game?.RoundNo != 8) return;
        var madara = Find(game);
        if (madara == null || madara.Passives.Madara.Sealed) return;
        // Reassert the round lock before readiness counting. The post-round human prediction reset
        // used to overwrite ConfirmedPredict after HandleNextRound had already locked Madara.
        SetUnableToAct(madara);

        foreach (var bot in game.PlayersList.Where(player =>
                     player.PlayerType == 404
                     && player.GetPlayerId() != madara.GetPlayerId()
                     && !player.Passives.IsDead
                     && !player.Status.IsSkip))
        {
            var alreadyChallenging = bot.Status.WhoToAttackThisTurn.Count == 1
                                     && bot.Status.WhoToAttackThisTurn[0] == madara.GetPlayerId();
            bot.Status.IsBlock = false;
            bot.Status.IsReady = true;
            bot.Status.ConfirmedPredict = true;
            bot.Status.WhoToAttackThisTurn = new List<Guid> { madara.GetPlayerId() };
            if (!alreadyChallenging)
                bot.Status.AddInGamePersonalLogs($"{SusanooClones}: вызов Мадаре принят.\n");
        }
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

        var bonus = GetIllusoryBonus(game, viewer);
        return $"{EternalTsukuyomi}: {viewer.DiscordUsername} выиграл свой бой.\n"
               + $"{EternalTsukuyomi}: +{bonus} бонусных очков\n\n"
               + $"**{viewer.DiscordUsername}** победил, играя за **{viewer.GameCharacter.Name}**";
    }
}
