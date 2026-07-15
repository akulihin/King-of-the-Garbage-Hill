using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class GordonFreeman
{
    public const string CharacterName = "Гордон Фримен";
    public const string Crowbar = "Монтировка";
    public const string SilentHero = "Молчаливый Герой";
    public const string WakeUp = "Просыпайтесь, мистер Фримен";
    public const string HalfLife3 = "Halflife 3";

    public const string HalfLifeAnnouncement = "Внимание! Halflife 3 был анонсирован!!!";
    public const string HalfLifeFailure = "Недостаточно профита, нельзя  выпускать игру.";

    public static readonly string[] FreezePhrases =
    {
        "Halflife 3 был заморожен для \nнаших потомков.",
        "Halflife 3 был заморожен до лучших времен.",
        "Halflife 3 был заморожен и через тысячу лет он проснется.",
        "Halflife 3 был заморожен. Добрых снова, мистер Фриман.",
    };

    public static readonly string[] FreezeLabels =
    {
        "Заморозить игру",
        "Заморозить игру",
        "Ещё не поздно заморозить",
    };

    public static readonly string[] PostponeLabels =
    {
        "Нужно нагнать ажиотажа",
        "Подождем новое поколение консолей",
        "Сделаем мем что игра никогда не выйдет, а потом... shadowdrop!",
    };

    public sealed class State
    {
        public int ResolvedFights { get; set; }
        public int LastObservedJustice { get; set; }
        public int HeadcrabsRemoved { get; set; }
        public bool AllZombiesPenaltyApplied { get; set; }
        public int AllZombiesPenaltyRound { get; set; }
        public bool WakeUsed { get; set; }
        public bool WakeReservedForEternalTsukuyomi { get; set; }
        public HalfLifeState HalfLife { get; set; } = new();
    }

    public sealed class HeadcrabState
    {
        public Guid SourceId { get; set; } = Guid.Empty;
        public int ExpiresAfterRound { get; set; }
        public bool IsZombie { get; set; }
        public bool IsActive => SourceId != Guid.Empty && !IsZombie;

        public void ClearActive()
        {
            SourceId = Guid.Empty;
            ExpiresAfterRound = 0;
        }
    }

    public sealed class HalfLifeState
    {
        public bool Announced { get; set; }
        public bool Finished { get; set; }
        public bool Released { get; set; }
        public int ReleaseRound { get; set; }
        public int Postponements { get; set; }
        public bool ActionSubmittedThisRound { get; set; }
        public bool PendingDecision { get; set; }
        public int DecisionSerial { get; set; }
        public DateTimeOffset? DeadlineUtc { get; set; }
        public decimal RawPoints { get; set; }
        public int BaseMultiplier { get; set; }
        public decimal Exponent { get; set; }
        public decimal FinalPoints { get; set; }
        public decimal? SettlementOverride { get; set; }
    }

    public static bool Is(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName;

    public static GamePlayerBridgeClass Find(GameClass game) =>
        game?.PlayersList.Find(Is);

    public static void ApplyHevBattery(GamePlayerBridgeClass player)
    {
        if (!Is(player) || player.Passives.IsDead) return;

        var justice = player.GameCharacter.Justice.GetRealJusticeNow();
        player.FightCharacter.SetStrengthForOneFight(
            player.FightCharacter.GetStrength() + justice, Crowbar);
        player.FightCharacter.SetSpeedForOneFight(
            player.FightCharacter.GetSpeed() + justice, Crowbar);
    }

    public static bool BeginResolvedFight(
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass defender,
        out GamePlayerBridgeClass gordon)
    {
        gordon = Is(attacker) ? attacker : Is(defender) ? defender : null;
        if (gordon == null || gordon.Passives.IsDead) return false;

        gordon.Passives.Gordon.ResolvedFights++;
        return gordon.Passives.Gordon.ResolvedFights % 3 == 0;
    }

    public static void PlantInitialHeadcrabs(List<GamePlayerBridgeClass> players)
    {
        var gordon = players?.Find(Is);
        if (gordon == null || gordon.Passives.IsDead) return;
        PlantHeadcrabs(players, gordon, expiresAfterRound: 3);
    }

    public static void PlantHeadcrabs(GameClass game)
    {
        var gordon = Find(game);
        if (gordon == null || gordon.Passives.IsDead) return;
        PlantHeadcrabs(game.PlayersList, gordon, game.RoundNo + 2);
    }

    private static void PlantHeadcrabs(
        IReadOnlyCollection<GamePlayerBridgeClass> players,
        GamePlayerBridgeClass gordon,
        int expiresAfterRound)
    {
        var candidates = players
            .Where(player => player.GetPlayerId() != gordon.GetPlayerId())
            .Where(player => gordon.TeamId <= 0 || player.TeamId != gordon.TeamId)
            .Where(player => !player.Passives.IsDead)
            .Where(player => player.GameCharacter.Name != "Краборак")
            .Where(player => !UnknownBug.Is(player))
            .Where(player => !player.Passives.GordonHeadcrab.IsZombie)
            .Where(player => !player.Passives.GordonHeadcrab.IsActive)
            .ToList();

        foreach (var target in SecureRandom.Shuffle(candidates).Take(2))
        {
            target.Passives.GordonHeadcrab.SourceId = gordon.GetPlayerId();
            target.Passives.GordonHeadcrab.ExpiresAfterRound = expiresAfterRound;
        }
    }

    public static void MatureHeadcrabs(GameClass game)
    {
        var gordon = Find(game);
        if (gordon == null || gordon.Passives.IsDead) return;

        foreach (var target in game.PlayersList.Where(player =>
                     player.Passives.GordonHeadcrab.IsActive
                     && player.Passives.GordonHeadcrab.SourceId == gordon.GetPlayerId()
                     && player.Passives.GordonHeadcrab.ExpiresAfterRound <= game.RoundNo))
        {
            var lost = target.GameCharacter.GetIntelligence();
            target.Passives.GordonHeadcrab.ClearActive();
            target.Passives.GordonHeadcrab.IsZombie = true;
            target.GameCharacter.AddIntelligence(-lost, SilentHero, isLog: false);
            target.Status.AddInGamePersonalLogs($"Вы стали крабом. -{lost} Интеллекта\n");
        }

        ApplyAllZombiesPenalty(game, gordon);
    }

    private static void ApplyAllZombiesPenalty(GameClass game, GamePlayerBridgeClass gordon)
    {
        var state = gordon.Passives.Gordon;
        if (state.AllZombiesPenaltyApplied) return;

        var enemies = game.PlayersList
            .Where(player => player.GetPlayerId() != gordon.GetPlayerId())
            .ToList();
        if (enemies.Count == 0 || enemies.Any(player => !player.Passives.GordonHeadcrab.IsZombie)) return;

        state.AllZombiesPenaltyApplied = true;
        state.AllZombiesPenaltyRound = game.RoundNo;
        gordon.Status.SetScoreToThisNumber(0, SilentHero);
        gordon.Status.SetScoresToGiveAtEndOfRound(0, SilentHero, false);
    }

    public static void RescueHeadcrab(
        GamePlayerBridgeClass gordon,
        GamePlayerBridgeClass target)
    {
        if (!Is(gordon) || target == null) return;
        var mark = target.Passives.GordonHeadcrab;
        if (!mark.IsActive || mark.SourceId != gordon.GetPlayerId()) return;

        mark.ClearActive();
        gordon.Status.AddBonusPoints(3, SilentHero);
        gordon.Passives.Gordon.HeadcrabsRemoved++;
        gordon.Passives.AchievementTracker.GordonHeadcrabsRemoved++;
    }

    public static bool CanWake(GamePlayerBridgeClass player, GameClass game)
    {
        if (!Is(player) || game == null || player.Passives.IsDead
            || player.Passives.Gordon.WakeUsed || game.IsKratosEvent)
            return false;

        if (player.Status.IsSkip) return true;
        return game.RoundNo == 9 && Madara.IsEternalTsukuyomiActive(game);
    }

    public static bool Wake(GamePlayerBridgeClass player, GameClass game)
    {
        if (!CanWake(player, game)) return false;

        var state = player.Passives.Gordon;
        state.WakeUsed = true;
        if (game.RoundNo == 9 && Madara.IsEternalTsukuyomiActive(game))
            state.WakeReservedForEternalTsukuyomi = true;

        player.Status.IsSkip = false;
        player.Status.IsBlock = false;
        player.Status.IsAutoMove = false;
        player.Status.IsReady = false;
        player.Status.ConfirmedSkip = true;
        player.Status.IsAbleToChangeMind = true;
        player.Status.WhoToAttackThisTurn.Clear();
        player.Status.AddInGamePersonalLogs($"{WakeUp}: G-Man вернул вас в этот ход.\n");
        return true;
    }

    public static bool IsAwakeForEternalTsukuyomi(GamePlayerBridgeClass player, GameClass game) =>
        Is(player) && game != null && Madara.IsEternalTsukuyomiRound(game)
        && player.Passives.Gordon.WakeReservedForEternalTsukuyomi;

    public static bool SeesEternalTsukuyomiReality(GamePlayerBridgeClass player, GameClass game) =>
        Is(player) && game != null
        && player.Passives.Gordon.WakeReservedForEternalTsukuyomi
        && Madara.IsEternalTsukuyomiActive(game);

    public static bool CanAnnounceHalfLife3(GamePlayerBridgeClass player, GameClass game) =>
        Is(player) && game != null && !player.Passives.IsDead && game.RoundNo < 10
        && !player.Passives.Gordon.HalfLife.Announced
        && !player.Passives.Gordon.HalfLife.Finished;

    public static bool AnnounceHalfLife3(GamePlayerBridgeClass player, GameClass game)
    {
        if (!CanAnnounceHalfLife3(player, game) || game.IsRoundTransitionPaused) return false;

        var halfLife = player.Passives.Gordon.HalfLife;
        halfLife.Announced = true;
        halfLife.ReleaseRound = game.RoundNo + 1;
        halfLife.ActionSubmittedThisRound = true;
        player.Status.IsBlock = false;
        player.Status.IsSkip = false;
        player.Status.IsAutoMove = false;
        player.Status.WhoToAttackThisTurn.Clear();
        player.Status.IsReady = true;
        player.Status.IsAbleToChangeMind = false;
        game.AddGlobalLogs(HalfLifeAnnouncement);
        game.StateRevision++;
        return true;
    }

    public static bool PrepareHalfLifeSettlement(GameClass game)
    {
        var gordon = Find(game);
        if (gordon == null || gordon.Passives.IsDead) return false;

        var halfLife = gordon.Passives.Gordon.HalfLife;
        halfLife.ActionSubmittedThisRound = false;
        if (!halfLife.Announced || halfLife.Finished || halfLife.ReleaseRound != game.RoundNo)
            return false;

        halfLife.RawPoints = gordon.Status.GetScoresToGiveAtEndOfRound();
        halfLife.BaseMultiplier = gordon.Status.GetRoundScoreMultiplier(game);
        halfLife.Exponent = halfLife.RawPoints;
        halfLife.FinalPoints = gordon.Passives.Gordon.AllZombiesPenaltyRound == game.RoundNo
            ? 0
            : CalculateHalfLifePoints(halfLife.BaseMultiplier, halfLife.Exponent);
        halfLife.SettlementOverride = halfLife.FinalPoints;

        if (halfLife.Exponent >= 3)
        {
            halfLife.Released = true;
            halfLife.Finished = true;
            gordon.Passives.AchievementTracker.GordonHalfLifeReleased = true;
            gordon.Status.AddInGamePersonalLogs(
                $"{HalfLife3}: {halfLife.BaseMultiplier}^{halfLife.Exponent} = {halfLife.FinalPoints} обычных очков.\n");
            return false;
        }

        gordon.Status.AddInGamePersonalLogs(
            $"{HalfLifeFailure}\n{halfLife.RawPoints} очков, множитель {halfLife.BaseMultiplier}, итог {halfLife.FinalPoints}.\n");

        if (halfLife.Postponements >= 3)
        {
            halfLife.Finished = true;
            game.AddGlobalLogs("Halflife 3 был отменен...");
            return false;
        }

        if (gordon.IsBot())
        {
            ResolveHalfLifeDecision(gordon, game, halfLife.DecisionSerial, "postpone", allowMissingPending: true);
            return false;
        }

        halfLife.PendingDecision = true;
        halfLife.DecisionSerial++;
        halfLife.DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(20);
        game.IsRoundTransitionPaused = true;
        game.TransitionDeadlineUtc = halfLife.DeadlineUtc;
        game.StateRevision++;
        return true;
    }

    private static decimal CalculateHalfLifePoints(int multiplier, decimal exponent)
    {
        var value = Math.Pow(multiplier, (double)exponent);
        // Keep enough headroom for the player's already-settled score and any later bonuses.
        // This only affects pathological exponent values; ordinary game results remain exact.
        var safeMaximum = decimal.MaxValue / 1000m;
        if (double.IsNaN(value) || double.IsInfinity(value) || value > (double)safeMaximum)
            return safeMaximum;
        return Math.Round((decimal)value, 2);
    }

    public static decimal ProjectRegularSettlement(GamePlayerBridgeClass player, GameClass game)
    {
        var raw = player.Status.GetScoresToGiveAtEndOfRound();
        if (!Is(player)) return raw * player.Status.GetRoundScoreMultiplier(game);

        var state = player.Passives.Gordon;
        var halfLife = state.HalfLife;
        if (!halfLife.Announced || halfLife.Finished || halfLife.ReleaseRound != game.RoundNo)
            return raw * player.Status.GetRoundScoreMultiplier(game);
        if (state.AllZombiesPenaltyRound == game.RoundNo)
            return 0;
        return CalculateHalfLifePoints(player.Status.GetRoundScoreMultiplier(game), raw);
    }

    public static bool ResolveHalfLifeDecision(
        GamePlayerBridgeClass gordon,
        GameClass game,
        int serial,
        string choice,
        bool allowMissingPending = false)
    {
        if (!Is(gordon) || game == null) return false;
        var halfLife = gordon.Passives.Gordon.HalfLife;
        lock (halfLife)
        {
            if ((!halfLife.PendingDecision && !allowMissingPending)
                || (halfLife.PendingDecision && serial != halfLife.DecisionSerial))
                return false;

            if (choice.Equals("postpone", StringComparison.OrdinalIgnoreCase)
                && halfLife.Postponements < 3)
            {
                var cost = halfLife.Postponements + 1;
                halfLife.SettlementOverride = halfLife.FinalPoints - cost * halfLife.BaseMultiplier;
                halfLife.Postponements++;
                halfLife.ReleaseRound = game.RoundNo + 1;
                game.AddGlobalLogs(halfLife.Postponements switch
                {
                    1 => "Внимание! Halflife 3 был перенесен.",
                    2 => "Внимание! Halflife 3 был перенесен повторно.",
                    _ => "Внимание! Halflife 3 был перенесен в третий раз!!!",
                });
            }
            else
            {
                halfLife.Finished = true;
                game.AddGlobalLogs(FreezePhrases[SecureRandom.Next(0, FreezePhrases.Length - 1)]);
            }

            halfLife.PendingDecision = false;
            halfLife.DeadlineUtc = null;
            game.TransitionDeadlineUtc = null;
            game.StateRevision++;
            return true;
        }
    }

    public static bool ResolveHalfLifeTimeout(GameClass game)
    {
        var gordon = Find(game);
        var halfLife = gordon?.Passives.Gordon.HalfLife;
        if (gordon == null || halfLife == null || !halfLife.PendingDecision
            || halfLife.DeadlineUtc > DateTimeOffset.UtcNow)
            return false;

        return ResolveHalfLifeDecision(gordon, game, halfLife.DecisionSerial, "freeze");
    }

    public static decimal? ConsumeSettlementOverride(GamePlayerBridgeClass player)
    {
        if (!Is(player)) return null;
        var halfLife = player.Passives.Gordon.HalfLife;
        var value = halfLife.SettlementOverride;
        halfLife.SettlementOverride = null;
        return value;
    }

    public static string GetFreezeLabel(State state) =>
        FreezeLabels[Math.Min(state.HalfLife.Postponements, FreezeLabels.Length - 1)];

    public static string GetPostponeLabel(State state) =>
        PostponeLabels[Math.Min(state.HalfLife.Postponements, PostponeLabels.Length - 1)];

    public static void HandleJusticePhrases(GamePlayerBridgeClass player, GameClass game)
    {
        if (!Is(player) || player.Passives.IsDead) return;
        var state = player.Passives.Gordon;
        var justice = player.GameCharacter.Justice.GetRealJusticeNow();
        if (justice < state.LastObservedJustice)
            state.LastObservedJustice = justice;

        for (var threshold = Math.Max(3, state.LastObservedJustice + 1); threshold <= Math.Min(5, justice); threshold++)
        {
            var index = threshold - 3;
            var phrase = game.Phrases.GordonCrowbarJustice;
            player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                phrase.PassiveNameRus,
                phrase.PassiveLogRus[index],
                phrase.PassiveNameEng,
                phrase.PassiveLogEng[index]) + "\n");
        }

        state.LastObservedJustice = justice;
    }
}
