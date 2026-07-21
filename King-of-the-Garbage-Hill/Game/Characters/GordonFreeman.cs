using System;
using System.Collections.Generic;
using System.Globalization;
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
    public const string HalfLifeReleaseDecision =
        "Игра готова к релизу. Гейб предлагает зафиксировать текущий профит или перенести релиз ещё раз.";
    public const string ReleaseNowLabel = "Выпустить игру!";
    public const string WaitForMoreProfitLabel = "Нет, ждем! Я хочу еще больше профита!!!";
    public const string RoundThreeAnnouncementPrompt =
        "Так, первый, второй... о, третий раунд. Самое время для анонса третьего Halflife!";
    public const string SilencePhrase = "Молчание: ...";

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
        public bool SuccessDecisionOffered { get; set; }
        public bool PendingReleaseConfirmation { get; set; }
        public bool ActionSubmittedThisRound { get; set; }
        public bool PendingDecision { get; set; }
        public int DecisionSerial { get; set; }
        public DateTimeOffset? DeadlineUtc { get; set; }
        public decimal RawPoints { get; set; }
        public int OrdinaryMultiplier { get; set; }
        public bool SuperMultiplierDisabled { get; set; }
        public decimal Exponent { get; set; }
        public decimal OrdinarySettlement { get; set; }
        public decimal FinalPoints { get; set; }
        public decimal ReleaseBonusPoints { get; set; }
        public HashSet<Guid> AttemptItachiThiefIds { get; set; } = new();
        public HashSet<Guid> ReleaseItachiThiefIds { get; set; } = new();
        public decimal? SettlementOverride { get; set; }
    }

    public static bool Is(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName
        && !player.Passives.PassiveAbilitiesDisabledByKimiko;

    public static GamePlayerBridgeClass Find(GameClass game) =>
        game?.PlayersList.Find(Is);

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
            target.GameCharacter.IntelligenceCappedAtZero = true;
            target.GameCharacter.AddIntelligence(-lost, SilentHero, isLog: false);
            target.Status.AddInGamePersonalLogs($"Вы стали крабом. -{lost} Интеллекта\n");
        }

        ApplyAllZombiesPenalty(game, gordon);
    }

    public static void ReevaluateAllZombiesPenalty(GameClass game)
    {
        var gordon = Find(game);
        if (gordon == null || gordon.Passives.IsDead) return;
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

        if (player.Status.IsSkip || IsUnderItachiEyes(player, game)) return true;
        return game.RoundNo == 9 && Madara.IsEternalTsukuyomiActive(game);
    }

    public static bool Wake(GamePlayerBridgeClass player, GameClass game)
    {
        if (!CanWake(player, game)) return false;

        var state = player.Passives.Gordon;
        state.WakeUsed = true;
        if (game.RoundNo == 9 && Madara.IsEternalTsukuyomiActive(game))
            state.WakeReservedForEternalTsukuyomi = true;

        CancelItachiEyes(player, game);

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

    private static bool IsUnderItachiEyes(GamePlayerBridgeClass player, GameClass game) =>
        game.PlayersList.Any(source =>
            !source.Passives.IsDead
            && source.GameCharacter.Passive.Any(passive => passive.PassiveName == "Глаза Итачи")
            && source.Passives.ItachiTsukuyomi.TsukuyomiActiveTarget == player.GetPlayerId());

    private static void CancelItachiEyes(GamePlayerBridgeClass player, GameClass game)
    {
        foreach (var source in game.PlayersList.Where(source =>
                     source.GameCharacter.Passive.Any(passive => passive.PassiveName == "Глаза Итачи")
                     && (source.Passives.ItachiTsukuyomi.TsukuyomiActiveTarget == player.GetPlayerId()
                         || source.Passives.ItachiTsukuyomi.TsukuyomiTargetThisRound == player.GetPlayerId())))
        {
            source.Passives.ItachiTsukuyomi.TsukuyomiActiveTarget = Guid.Empty;
            source.Passives.ItachiTsukuyomi.TsukuyomiTargetThisRound = Guid.Empty;
            game.Phrases.ItachiTsukuyomiEnd.SendLog(source, false);
        }
    }

    public static bool IsAwakeForEternalTsukuyomi(GamePlayerBridgeClass player, GameClass game) =>
        Is(player) && game != null && Madara.IsEternalTsukuyomiRound(game)
        && player.Passives.Gordon.WakeReservedForEternalTsukuyomi;

    public static bool SeesEternalTsukuyomiReality(GamePlayerBridgeClass player, GameClass game) =>
        Is(player) && game != null
        && player.Passives.Gordon.WakeReservedForEternalTsukuyomi
        && Madara.IsEternalTsukuyomiActive(game);

    public static bool CanAnnounceHalfLife3(GamePlayerBridgeClass player, GameClass game) =>
        Is(player) && game != null && !player.Passives.IsDead && game.RoundNo is >= 3 and <= 7
        && !player.Passives.Gordon.HalfLife.Announced
        && !player.Passives.Gordon.HalfLife.Finished;

    public static bool AnnounceHalfLife3(GamePlayerBridgeClass player, GameClass game)
    {
        if (!CanAnnounceHalfLife3(player, game) || game.IsRoundTransitionPaused) return false;

        var halfLife = player.Passives.Gordon.HalfLife;
        halfLife.Announced = true;
        halfLife.ReleaseRound = game.RoundNo + 1;
        halfLife.ActionSubmittedThisRound = true;
        halfLife.AttemptItachiThiefIds.Clear();
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
        halfLife.OrdinaryMultiplier = gordon.Status.GetRoundScoreMultiplier(game);
        halfLife.SuperMultiplierDisabled = gordon.Status.IsRoundScoreMultiplierDisabledByTolya(game);
        halfLife.Exponent = halfLife.RawPoints;
        halfLife.OrdinarySettlement = halfLife.RawPoints * halfLife.OrdinaryMultiplier;
        halfLife.FinalPoints = gordon.Passives.Gordon.AllZombiesPenaltyRound == game.RoundNo
            ? 0
            : halfLife.SuperMultiplierDisabled
                ? halfLife.OrdinarySettlement
                : CalculateHalfLifePoints(halfLife.RawPoints);
        halfLife.SettlementOverride = halfLife.FinalPoints;

        if (halfLife.Exponent >= 3)
        {
            if (!gordon.IsBot() && halfLife.Postponements < 3
                                && !halfLife.SuccessDecisionOffered)
            {
                halfLife.SuccessDecisionOffered = true;
                game.AddGlobalLogs(PhrasePayload.Encode(
                    HalfLife3,
                    $"{HalfLifeReleaseDecision}\nПрофит: {halfLife.FinalPoints} очков.",
                    "Half-Life 3",
                    $"The game is ready. Gabe can lock in {halfLife.FinalPoints} points or wait for more profit."));
                return BeginHalfLifeDecision(game, halfLife, releaseConfirmation: true);
            }

            ReleaseHalfLife3(gordon, game);
            return false;
        }

        var failureCalculation = halfLife.SuperMultiplierDisabled
            ? $"Подсчет отключил расчет степени: начислено {halfLife.FinalPoints} обычных очков."
            : $"{halfLife.RawPoints}^{halfLife.Exponent} = {halfLife.FinalPoints} обычных очков.";
        var failureCalculationEnglish = halfLife.SuperMultiplierDisabled
            ? $"Counting disabled the power calculation: {halfLife.FinalPoints} regular points awarded."
            : $"{halfLife.RawPoints}^{halfLife.Exponent} = {halfLife.FinalPoints} regular points.";
        game.AddGlobalLogs(PhrasePayload.Encode(
            HalfLife3,
            $"{HalfLifeFailure}\n{failureCalculation}",
            "Half-Life 3",
            $"Not enough profit to release the game.\n{failureCalculationEnglish}"));

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

        return BeginHalfLifeDecision(game, halfLife, releaseConfirmation: false);
    }

    private static bool BeginHalfLifeDecision(
        GameClass game,
        HalfLifeState halfLife,
        bool releaseConfirmation)
    {
        halfLife.PendingDecision = true;
        halfLife.PendingReleaseConfirmation = releaseConfirmation;
        halfLife.DecisionSerial++;
        halfLife.DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(20);
        game.IsRoundTransitionPaused = true;
        game.TransitionDeadlineUtc = halfLife.DeadlineUtc;
        game.StateRevision++;
        return true;
    }

    private static void ReleaseHalfLife3(GamePlayerBridgeClass gordon, GameClass game)
    {
        var halfLife = gordon.Passives.Gordon.HalfLife;
        halfLife.Released = true;
        halfLife.Finished = true;
        halfLife.PendingReleaseConfirmation = false;
        gordon.Passives.AchievementTracker.GordonHalfLifeReleased = true;
        halfLife.ReleaseBonusPoints = Math.Max(0, halfLife.FinalPoints - halfLife.OrdinarySettlement);
        halfLife.ReleaseItachiThiefIds = new HashSet<Guid>(halfLife.AttemptItachiThiefIds);
        var releaseCalculation = halfLife.SuperMultiplierDisabled
            ? $"Подсчет отключил расчет степени: начислено {halfLife.FinalPoints} обычных очков."
            : $"{halfLife.RawPoints}^{halfLife.Exponent} = {halfLife.FinalPoints} обычных очков.";
        game.AddGlobalLogs(PhrasePayload.Encode(
            HalfLife3,
            releaseCalculation,
            "Half-Life 3",
            halfLife.SuperMultiplierDisabled
                ? $"Counting disabled the power calculation: {halfLife.FinalPoints} regular points awarded."
                : $"{halfLife.RawPoints}^{halfLife.Exponent} = {halfLife.FinalPoints} regular points."));
        game.HalfLifeReleaseSerial++;
        game.StateRevision++;
    }

    private static decimal CalculateHalfLifePoints(decimal points)
    {
        if (points <= 0) return 0;

        var value = Math.Pow((double)points, (double)points);
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
        if (player.Status.IsRoundScoreMultiplierDisabledByTolya(game))
            return raw * player.Status.GetRoundScoreMultiplier(game);
        return CalculateHalfLifePoints(raw);
    }

    public static void MarkCurrentAttemptStolenByItachi(
        GamePlayerBridgeClass player,
        GamePlayerBridgeClass itachi,
        GameClass game)
    {
        if (!Is(player) || itachi == null || game == null) return;
        var halfLife = player.Passives.Gordon.HalfLife;
        if (!halfLife.Announced || halfLife.Finished || halfLife.ReleaseRound != game.RoundNo)
            return;
        halfLife.AttemptItachiThiefIds.Add(itachi.GetPlayerId());
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

            var releaseChoice = choice.Equals("release", StringComparison.OrdinalIgnoreCase);
            var postponeChoice = choice.Equals("postpone", StringComparison.OrdinalIgnoreCase);
            var freezeChoice = choice.Equals("freeze", StringComparison.OrdinalIgnoreCase);

            if (releaseChoice && halfLife.PendingReleaseConfirmation)
            {
                ReleaseHalfLife3(gordon, game);
            }
            else if (postponeChoice && halfLife.Postponements < 3)
            {
                var cost = halfLife.Postponements + 1;
                if (halfLife.FinalPoints < cost)
                {
                    halfLife.Finished = true;
                    halfLife.Released = false;
                    game.AddGlobalLogs(
                        $"Halflife 3 был отменен: для переноса нужно {cost} очк., доступно {halfLife.FinalPoints}.");
                }
                else
                {
                    halfLife.SettlementOverride = halfLife.FinalPoints - cost;
                    halfLife.Postponements++;
                    halfLife.ReleaseRound = game.RoundNo + 1;
                    halfLife.AttemptItachiThiefIds.Clear();
                    game.AddGlobalLogs(halfLife.Postponements switch
                    {
                        1 => "Внимание! Halflife 3 был перенесен.",
                        2 => "Внимание! Halflife 3 был перенесен повторно.",
                        _ => "Внимание! Halflife 3 был перенесен в третий раз!!!",
                    });
                }
            }
            else if (freezeChoice && !halfLife.PendingReleaseConfirmation)
            {
                halfLife.Finished = true;
                game.AddGlobalLogs(FreezePhrases[SecureRandom.Next(0, FreezePhrases.Length - 1)]);
            }
            else
            {
                return false;
            }

            halfLife.PendingDecision = false;
            halfLife.PendingReleaseConfirmation = false;
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

        return ResolveHalfLifeDecision(
            gordon,
            game,
            halfLife.DecisionSerial,
            halfLife.PendingReleaseConfirmation ? "release" : "freeze");
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
        state.HalfLife.PendingReleaseConfirmation
            ? ReleaseNowLabel
            : FreezeLabels[Math.Min(state.HalfLife.Postponements, FreezeLabels.Length - 1)];

    public static string GetPostponeLabel(State state) =>
        state.HalfLife.PendingReleaseConfirmation
            ? WaitForMoreProfitLabel
            : PostponeLabels[Math.Min(state.HalfLife.Postponements, PostponeLabels.Length - 1)];

    public static string GetDecisionMessage(State state) =>
        state.HalfLife.PendingReleaseConfirmation ? HalfLifeReleaseDecision : HalfLifeFailure;

    public static void HandleRoundPhrase(GamePlayerBridgeClass player, int roundNo)
    {
        if (!Is(player) || player.Passives.IsDead || roundNo is < 1 or > 10) return;
        var russian = roundNo == 3 ? RoundThreeAnnouncementPrompt : SilencePhrase;
        var english = roundNo == 3
            ? "So, first, second... oh, round three. The perfect time to announce the third Half-Life!"
            : "Silence: ...";
        player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
            SilentHero, russian, "Silent Hero", english) + "\n");
    }

    public static bool WonThanksToHalfLife(GameClass game, GamePlayerBridgeClass gordon)
    {
        if (game == null || gordon == null || gordon.Passives.IsDead
            || !game.WinnerPlayerIds.Contains(gordon.GetPlayerId()))
            return false;

        var halfLife = gordon.Passives.Gordon.HalfLife;
        var releaseDeductedByLivingItachi = halfLife.ReleaseItachiThiefIds.Any(thiefId =>
            game.PlayersList.Any(player =>
                player.GetPlayerId() == thiefId
                && !player.Passives.IsDead
                && player.GameCharacter.Passive.Any(passive => passive.PassiveName == "Глаза Итачи")
                && player.Passives.ItachiTsukuyomi.StolenFromPlayers.TryGetValue(
                    gordon.GetPlayerId(), out var stolenAmount)
                && stolenAmount > 0));
        var retainedBonus = releaseDeductedByLivingItachi ? 0 : halfLife.ReleaseBonusPoints;
        if (!halfLife.Released || retainedBonus <= 0) return false;

        if (game.Teams.Count == 0)
        {
            var strongestOpponent = game.PlayersList
                .Where(player => !player.Passives.IsDead
                                 && player.GetPlayerId() != gordon.GetPlayerId())
                .Select(player => player.Status.GetScore())
                .DefaultIfEmpty(0)
                .Max();
            return gordon.Status.GetScore() - retainedBonus <= strongestOpponent;
        }

        var gordonTeam = game.Teams.Find(team => team.TeamPlayers.Contains(gordon.GetPlayerId()));
        if (gordonTeam == null) return false;
        var winningTeamScore = game.PlayersList
            .Where(player => gordonTeam.TeamPlayers.Contains(player.GetPlayerId()))
            .Sum(player => player.Status.GetScore());
        var strongestOtherTeam = game.Teams
            .Where(team => team.TeamId != gordonTeam.TeamId)
            .Select(team => game.PlayersList
                .Where(player => team.TeamPlayers.Contains(player.GetPlayerId()))
                .Sum(player => player.Status.GetScore()))
            .DefaultIfEmpty(0)
            .Max();
        return winningTeamScore - retainedBonus <= strongestOtherTeam;
    }

    public static string BuildWinningReleaseLog(GamePlayerBridgeClass gordon)
    {
        var points = gordon.Passives.Gordon.HalfLife.FinalPoints
            .ToString("0.##", CultureInfo.InvariantCulture);
        return PhrasePayload.Encode(
            HalfLife3,
            $"**Hilfelife 3 в первый же день разошлась тиражом {points} миллионов и получила премию Игра Тысячилетия!**",
            "Half-Life 3",
            $"**Hilfelife 3 sold {points} million copies on day one and won the Game of the Millennium award!**");
    }
}
