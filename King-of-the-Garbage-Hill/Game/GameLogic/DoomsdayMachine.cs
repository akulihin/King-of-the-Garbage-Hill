using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.API.Services;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class DoomsdayMachine : IServiceSingleton
{
    private readonly CharacterPassives _characterPassives;
    private readonly LoginFromConsole _logs;
    private readonly CalculateRounds _calculateRounds;
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly SecureRandom _rand;
    private readonly CharactersPull _charactersPull;
    private readonly Dictionary<ulong, PendingRoundContinuation> _pendingRounds = new();

    private sealed record PendingRoundContinuation(ReplayRoundDto ReplayRound, Stopwatch Watch);

    public DoomsdayMachine(CharacterPassives characterPassives, LoginFromConsole logs,
        CalculateRounds calculateRounds, GameUpdateMess gameUpdateMess, SecureRandom rand,
        CharactersPull charactersPull)
    {
        _characterPassives = characterPassives;
        _logs = logs;
        _calculateRounds = calculateRounds;
        _gameUpdateMess = gameUpdateMess;
        _rand = rand;
        _charactersPull = charactersPull;
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    // Called when attacker (me) has nemesis advantage over target
    public string GetLostNemesisText(GamePlayerBridgeClass me, GamePlayerBridgeClass target)
    {
        var (knownClass, flavorText) = me.GameCharacter.GetSkillClassType() switch
        {
            SkillClassType.Intelligence => ("(**Умный** ?) ", "вас обманул"),
            SkillClassType.Strength => ("(**Сильный** ?) ", "вас пресанул"),
            SkillClassType.Speed => ("(**Быстрый** ?) ", "вас обогнал"),
            _ => ("", "буль?")
        };

        if (knownClass != "")
            target.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(me.GetPlayerId(), knownClass));

        return flavorText;
    }


    public void ResetFight(GameClass game, GamePlayerBridgeClass me, GamePlayerBridgeClass target = null)
    {
        var players = new List<GamePlayerBridgeClass> { me, target };
        foreach (var player in players.Where(p => p != null))
        {

            if (player.Status.IsWonThisCalculation != Guid.Empty)
            {
                player.GameCharacter.AddWinStreak();
                player.Passives.WeedwickWeed++;
            }

            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                player.GameCharacter.SetWinStreak();
            }

            //OneFight Mechanics, reset on BOTH GameCharacter and FightCharacter
            //FightCharacter is deep-copied once per round, so ForOneFight overrides
            //set by before-fight passives would leak into subsequent fights without this.
            if (player.Status.IsIntelligenceForOneFight)
            {
                player.Status.IsIntelligenceForOneFight = false;
                player.GameCharacter.ResetIntelligenceForOneFight();
                player.FightCharacter.ResetIntelligenceForOneFight();
            }

            if (player.Status.IsStrengthForOneFight)
            {
                player.Status.IsStrengthForOneFight = false;
                player.GameCharacter.ResetStrengthForOneFight();
                player.FightCharacter.ResetStrengthForOneFight();
            }

            if (player.Status.IsSpeedForOneFight)
            {
                player.Status.IsSpeedForOneFight = false;
                player.GameCharacter.ResetSpeedForOneFight();
                player.FightCharacter.ResetSpeedForOneFight();
            }

            if (player.Status.IsPsycheForOneFight)
            {
                player.Status.IsPsycheForOneFight = false;
                player.GameCharacter.ResetPsycheForOneFight();
                player.FightCharacter.ResetPsycheForOneFight();
            }

            if (player.Status.IsJusticeForOneFight )
            {
                player.Status.IsJusticeForOneFight = false;
                player.GameCharacter.Justice.ResetJusticeForOneFight();
                player.FightCharacter.Justice.ResetJusticeForOneFight();
            }

            if (player.Status.IsSkillForOneFight)
            {
                player.Status.IsSkillForOneFight = false;
                player.GameCharacter.ResetSkillForOneFight();
                player.FightCharacter.ResetSkillForOneFight();
            }
            //end OneFight Mechanics

            player.Status.MoralGainedThisFight = 0;
            player.Status.IsWonThisCalculation = Guid.Empty;
            player.Status.IsLostThisCalculation = Guid.Empty;
            player.Status.IsFighting = Guid.Empty;
            player.Status.IsTargetSkipped = Guid.Empty;
            player.Status.IsTargetBlocked = Guid.Empty;
            player.Status.IsAbleToWin = true;
            player.Status.IsShadowAction = false;
            player.Passives.SaitamaUnnoticed.PretendedLossThisFight = false;
            player.Passives.AchievementTracker.SpartanRespectTriggeredThisFight = Guid.Empty;
        }
    }

    private static void MarkHiddenFight(
        GameClass game,
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass defender,
        int globalLogsLengthBeforeFight)
    {
        if (!attacker.Status.HideCurrentFight && !defender.Status.HideCurrentFight)
            return;

        var lastFight = game.WebFightLog.LastOrDefault();
        if (lastFight != null)
        {
            lastFight.HiddenFromNonAdmin = true;
            lastFight.ShadowAction = attacker.Status.IsShadowAction;
        }

        var globalLogsNow = game.GetGlobalLogs();
        if (globalLogsNow.Length > globalLogsLengthBeforeFight)
            game.HiddenGlobalLogSnippets.Add(globalLogsNow[globalLogsLengthBeforeFight..]);

        attacker.Status.HideCurrentFight = false;
        defender.Status.HideCurrentFight = false;
        attacker.Status.IsShadowAction = false;
        defender.Status.IsShadowAction = false;
    }

    public void DeepCopyGameCharacterToFightCharacter(GameClass game)
    {
        foreach (var player in game.PlayersList)
        {
            player.FightCharacter = player.GameCharacter.DeepCopy();
        }
    }

    public void HandleEventsBeforeCalculation(GameClass game)
    {
        foreach (var player in game.PlayersList.Where(player =>
                     !player.Passives.IsDead && UnknownBug.Is(player)))
            UnknownBug.SelectStreamTarget(game, player);
    }

    private static void EnforceKratosEventActions(GameClass game)
    {
        if (!game.IsKratosEvent) return;

        foreach (var player in game.PlayersList.Where(player =>
                     player.GameCharacter.Name != "Кратос" && !UnknownBug.Is(player)))
        {
            player.Status.WhoToAttackThisTurn.Clear();
            player.Status.IsSkip = false;
            player.Status.IsBlock = true;
            player.Status.IsReady = true;
        }
    }




    //пристрій судного дня
    public bool HasPendingRound(GameClass game) =>
        game != null && _pendingRounds.ContainsKey(game.GameId);

    public async Task<bool> ResumePendingRound(GameClass game)
    {
        if (game == null || !_pendingRounds.TryGetValue(game.GameId, out var continuation))
            return false;

        GordonFreeman.ResolveHalfLifeTimeout(game);
        var gordon = GordonFreeman.Find(game);
        if (gordon?.Passives.Gordon.HalfLife.PendingDecision == true)
            return false;

        _pendingRounds.Remove(game.GameId);
        game.IsRoundTransitionPaused = false;
        game.TransitionDeadlineUtc = null;
        continuation.Watch.Start();
        await CompleteRoundAsync(game, continuation.ReplayRound, continuation.Watch);
        return true;
    }

    public async Task<bool> CalculateAllFights(GameClass game)
    {
        var watch = new Stopwatch();
        watch.Start();

        game.AnyFightThisRound = false; // set true below whenever a fight resolves (Tilted / M8)
        UnknownBug.EnsureExploitMarker(game);

        // Clear web messages from the PREVIOUS round at the START of new processing.
        // This ensures they persist long enough for the SignalR timer to broadcast them.
        // Multi-round media (e.g. Kratos music with RoundsToPlay > 1) is kept alive.
        foreach (var p in game.PlayersList)
        {
            p.WebMessages.Clear();
            // Increment round counter and remove expired media; keep multi-round entries alive
            for (var mi = p.WebMediaMessages.Count - 1; mi >= 0; mi--)
            {
                var entry = p.WebMediaMessages[mi];
                // Madara's round-eight theme starts while players choose actions and must survive this
                // cleanup until the fights finish. It is removed explicitly after the fight loop below.
                if (game.RoundNo == 8 && entry.PassiveName == Madara.SusanooClones
                    && entry.FileUrl?.EndsWith("madara_tsukuemi_theme.mp3", StringComparison.OrdinalIgnoreCase) == true)
                    continue;
                entry.RoundsPlayed++;
                if (entry.RoundsPlayed >= entry.RoundsToPlay)
                    p.WebMediaMessages.RemoveAt(mi);
            }
        }

        // A v2 replay round owns matching pre-fight and result state. Clear the prior log first,
        // then freeze the action-locked state before any fight conversions or passive dispatch.
        game.WebFightLog.Clear();
        game.HiddenGlobalLogSnippets.Clear();
        var replayRound = ReplayService.BeginRound(game, _gameUpdateMess);

        game.TimePassed.Stop();
        var roundNumber = game.RoundNo + 1;
        if (roundNumber > 10) roundNumber = 10;

        //Возвращение из мертвых
        if (game.IsKratosEvent)
            roundNumber = game.RoundNo + 1;
        //end Возвращение из мертвых


        //Handle Moral
        foreach (var p in game.PlayersList)
        {
            if (p.Passives.IsDead)
            {
                p.GameCharacter.SetBonusPointsFromMoral(0);
                continue;
            }
            var moralPoints = p.GameCharacter.GetBonusPointsFromMoral();
            if (moralPoints != 0)
                p.Status.AddBonusPoints(moralPoints, "Мораль");
            p.GameCharacter.SetBonusPointsFromMoral(0);
        }
        //end Moral

        /*
        1-4 х1
        5-9 х2
        10  х4
         */

        game.SetGlobalLogs($"\n__**Раунд #{roundNumber}**__:\n\n");
        // Also mark the round boundary in the cumulative log (SetGlobalLogs only sets per-round GlobalLogs)
        game.AddGlobalLogsRaw($"\n__**Раунд #{roundNumber-1}**__:\n");

        //FightCharacter == READ ONLY
        //GameCharacter == WRITE ONLY
        //FightCharacter writes cans happens only "for one fight" not for the whole round!
        var isEternalTsukuyomiRound = Madara.PrepareEternalTsukuyomiRound(game);
        if (isEternalTsukuyomiRound)
        {
            var livingBugActs = game.PlayersList.Any(player =>
                UnknownBug.Is(player) && !player.Passives.IsDead
                                      && player.Status.WhoToAttackThisTurn.Count > 0);
            if (!livingBugActs)
                game.AddGlobalLogs(GordonFreeman.Find(game)?.Passives.Gordon.WakeReservedForEternalTsukuyomi == true
                    ? "Все игроки, кроме Гордона Фримена, пропустили ход..."
                    : "Все игроки пропустили ход...");
        }

        // Щит-акула replaces DooM Guy's submitted block with a fightable, non-attacking
        // one-turn copy of Братишка's defensive passive. Prepare it before the round snapshot.
        if (!isEternalTsukuyomiRound && !game.IsKratosEvent)
            foreach (var doom in game.PlayersList.Where(x =>
                         x.GameCharacter.Name == DoomGuy.CharacterName && !x.Passives.IsDead))
                DoomGuy.PrepareSharkShield(doom);

        DeepCopyGameCharacterToFightCharacter(game);

        // Геральт — Медитация: skip works as block
        if (!isEternalTsukuyomiRound && !game.IsKratosEvent)
            foreach (var player in game.PlayersList.Where(x =>
                x.GameCharacter.Passive.Any(y => y.PassiveName == "Медитация") &&
                x.GameCharacter.Name == "Геральт" &&
                !x.Passives.IsDead &&
                x.Status.IsSkip).ToList())
            {
                player.Status.IsSkip = false;
                player.Status.IsBlock = true;
            }







        // Pickle Rick — convert block to pickle form and keep the active pickle fightable.
        if (!isEternalTsukuyomiRound && !game.IsKratosEvent)
            foreach (var player in game.PlayersList.Where(x => !x.Passives.IsDead
                         && x.GameCharacter.Passive.Any(y => y.PassiveName == "Огурчик Рик")).ToList())
            {
                var pickle = player.Passives.RickPickle;
                if (pickle.PickleTurnsRemaining > 0)
                {
                    player.Status.IsBlock = false;
                    player.Status.IsSkip = false;
                }
                else if (player.Status.IsBlock && pickle.PenaltyTurnsRemaining == 0)
                {
                    player.Status.IsBlock = false;
                    player.Status.IsSkip = false;
                    pickle.PickleTurnsRemaining = 2;
                    pickle.WasAttackedAsPickle = false;
                    game.Phrases.RickPickleTransform.SendLog(player, false);
                }
            }

        // Эрен Йегер — block becomes Attack Titan for the whole round snapshot.
        foreach (var player in game.PlayersList.Where(x => !isEternalTsukuyomiRound && !game.IsKratosEvent &&
                     !x.Passives.IsDead &&
                     x.GameCharacter.Name == ErenYeager.CharacterName
                     && x.GameCharacter.Passive.Any(y => y.PassiveName == ErenYeager.AttackTitan)
                     && x.Passives.Eren.AttackTitanCooldown == 0
                     && x.Status.IsBlock).ToList())
        {
            player.Status.IsBlock = false;
            player.Passives.Eren.AttackTitanActiveThisRound = true;
            player.Passives.Eren.AttackTitanSoundSerial++;
            player.Status.AddInGamePersonalLogs(
                $"{ErenYeager.AttackTitan}: +5 всех статов на этот ход.\n");
        }

        // Portal Gun — override external skip/block if Rick wants to attack
        foreach (var player in game.PlayersList.Where(x => !isEternalTsukuyomiRound && !game.IsKratosEvent
                     && !x.Passives.IsDead
                     && x.GameCharacter.Passive.Any(y => y.PassiveName == "Портальная пушка")).ToList())
        {
            var gun = player.Passives.RickPortalGun;
            if ((player.Status.IsBlock || player.Status.IsSkip) && gun.Invented && gun.Charges > 0 && player.Status.WhoToAttackThisTurn.Count > 0)
            {
                player.Status.IsBlock = false;
                player.Status.IsSkip = false;
            }
        }

        // Aggress — Toxic Mate can't block or skip
        foreach (var player in game.PlayersList.Where(x => !isEternalTsukuyomiRound && !game.IsKratosEvent
                     && !x.Passives.IsDead
                     && x.GameCharacter.Passive.Any(y => y.PassiveName == "Aggress")).ToList())
        {
            if (player.Status.IsBlock || player.Status.IsSkip)
            {
                player.WebMessages.Add("Aggress: Ты не можешь пропустить ход!");
                player.Status.IsBlock = false;
                player.Status.IsSkip = false;
            }
        }

        // Геральт — Ведьмачьи заказы: inject extra fights based on contract count
        // Works both when Geralt attacks AND when someone attacks Geralt
        var geraltPlayer = isEternalTsukuyomiRound || game.IsKratosEvent ? null : game.PlayersList.Find(x =>
            x.GameCharacter.Name == "Геральт" &&
            !x.Passives.IsDead &&
            x.GameCharacter.Passive.Any(y => y.PassiveName == "Ведьмачьи заказы"));

        if (geraltPlayer != null)
        {
            var geraltContracts = geraltPlayer.Passives.GeraltContracts;
            var geraltAchievements = geraltPlayer.Passives.AchievementTracker;
            geraltAchievements.GeraltContractFightsRemaining.Clear();
            var geraltId = geraltPlayer.GetPlayerId();

            // Attack side: Geralt attacks someone — inject extra fights for Geralt
            if (!geraltPlayer.Status.IsBlock && !geraltPlayer.Status.IsSkip && geraltPlayer.Status.WhoToAttackThisTurn.Count > 0)
            {
                var shenMagnet = Salldorum.FindRandomTargetMagnet(game, geraltPlayer);
                var targetId = shenMagnet != null
                               && geraltPlayer.Status.WhoToAttackThisTurn.Contains(shenMagnet.GetPlayerId())
                    ? shenMagnet.GetPlayerId()
                    : geraltPlayer.Status.WhoToAttackThisTurn[0];
                var target = game.PlayersList.Find(x => x.GetPlayerId() == targetId);
                if (target != null && !UnknownBug.Is(target))
                {
                    var count = Salldorum.TakeGeraltContractCount(game, geraltPlayer, target);
                    if (count > 0)
                    {
                        for (int i = 1; i < count; i++)
                            geraltPlayer.Status.WhoToAttackThisTurn.Add(targetId);

                        geraltContracts.ContractsFoughtThisRound += count;
                        geraltContracts.ContractProcsOnEnemy.TryAdd(targetId, 0);
                        geraltContracts.ContractProcsOnEnemy[targetId] += count;
                        geraltAchievements.GeraltContractFightsRemaining[targetId] = count;
                    }
                }
            }

            // Defense side: someone attacks Geralt — inject extra fights for the attacker
            // Skip if Geralt is blocking/skipping — contracts stay
            if (!geraltPlayer.Status.IsBlock && !geraltPlayer.Status.IsSkip)
            foreach (var attacker in game.PlayersList)
            {
                if (attacker.GetPlayerId() == geraltId) continue;
                if (attacker.Passives.IsDead) continue;
                if (UnknownBug.Is(attacker)) continue;
                if (attacker.Status.IsBlock || attacker.Status.IsSkip) continue;
                if (!attacker.Status.WhoToAttackThisTurn.Contains(geraltId)) continue;

                var count = Salldorum.TakeGeraltContractCount(game, geraltPlayer, attacker);
                if (count <= 0) continue;

                // Add N-1 extra fights (original already in list)
                for (int i = 1; i < count; i++)
                    attacker.Status.WhoToAttackThisTurn.Add(geraltId);

                geraltContracts.ContractsFoughtThisRound += count;
                geraltContracts.ContractProcsOnEnemy.TryAdd(attacker.GetPlayerId(), 0);
                geraltContracts.ContractProcsOnEnemy[attacker.GetPlayerId()] += count;
                geraltAchievements.GeraltContractFightsRemaining[attacker.GetPlayerId()] = count;
            }
        }

        // Naruto's block replacement operates on the finalized action queues, including every
        // readiness-stage forced action and Geralt contract expansion. Canceled queues must be gone
        // before PointFunnel and Madara snapshot their targets.
        if (!isEternalTsukuyomiRound && !game.IsKratosEvent)
        {
            var unknownBug = UnknownBug.FindOwner(game);
            var queuedExploitTarget = unknownBug == null
                ? null
                : game.PlayersList.FirstOrDefault(target =>
                    target.Passives.IsExploitable
                    && unknownBug.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()));

            Naruto.SanitizeMutualTargets(game);
            Naruto.ResolveHaremQueues(game);
            if (queuedExploitTarget != null
                && !unknownBug.Status.WhoToAttackThisTurn.Contains(queuedExploitTarget.GetPlayerId()))
                UnknownBug.TryCommitExploit(game, unknownBug, queuedExploitTarget, false);
        }
        EnforceKratosEventActions(game);
        TheBoys.DisablePassivesBeforeFights(game);
        HandleEventsBeforeCalculation(game);
        if (!game.IsKratosEvent)
            Madara.PrepareIncomingAttackers(game);
        if (!isEternalTsukuyomiRound && !game.IsKratosEvent)
            Naruto.SnapshotJustice(game);

        // Котики — Рандомное поведение Trick 1: pre-scan fight pairs and pick one for Storm
        Kotiki.RandomBehaviorClass stormRb = null;
        GamePlayerBridgeClass stormCarrier = null;
        {
            var kotikiOwnerDm = game.IsKratosEvent ? null : game.PlayersList.Find(x =>
                x.GameCharacter.Name == "Котики" && !x.Passives.IsDead);
            if (kotikiOwnerDm != null)
            {
                stormRb = kotikiOwnerDm.Passives.KotikiRandomBehavior;
                // Find who carries the "Рандомное поведение" passive
                stormCarrier = game.PlayersList.Find(x =>
                    !x.Passives.IsDead
                    && x.GameCharacter.Passive.Any(p => p.PassiveName == "Рандомное поведение"));

                if (stormRb.SelectedTrickThisRound == 1)
                {
                    // Collect all fight pairs from non-blocked/non-skipped players
                    var fightPairs = new List<(Guid attackerId, Guid defenderId)>();
                    foreach (var pl in game.PlayersList)
                    {
                        if (pl.Passives.IsDead || UnknownBug.Is(pl)) continue;
                        if ((pl.Status.IsBlock || pl.Status.IsSkip) && pl.Status.WhoToAttackThisTurn.Count == 0)
                            continue;
                        foreach (var targetId in pl.Status.WhoToAttackThisTurn.Where(t => t != pl.GetPlayerId()))
                        {
                            var target = game.PlayersList.Find(x => x.GetPlayerId() == targetId);
                            if (target != null && !target.Passives.IsDead && !UnknownBug.Is(target))
                                fightPairs.Add((pl.GetPlayerId(), targetId));
                        }
                    }

                    if (fightPairs.Count > 0)
                    {
                        var chosenPair = fightPairs[_rand.Random(0, fightPairs.Count - 1)];
                        stormRb.FightTargetAttackerId = chosenPair.attackerId;
                        stormRb.FightTargetDefenderId = chosenPair.defenderId;
                    }
                    else
                    {
                        stormRb.SelectedTrickThisRound = 0; // no fights — cancel trick
                    }
                }
            }
        }

        // Gordon's submitted attacks resolve before any incoming fights can spend a charged
        // Монтировка. Every other attacker keeps the existing leaderboard order.
        var fightCalculationOrder = game.PlayersList.ToList();
        var attackingGordon = fightCalculationOrder.Find(player =>
            GordonFreeman.Is(player)
            && !player.Status.IsBlock
            && !player.Status.IsSkip
            && player.Status.WhoToAttackThisTurn.Count > 0);
        if (attackingGordon != null)
        {
            fightCalculationOrder.Remove(attackingGordon);
            fightCalculationOrder.Insert(0, attackingGordon);
        }

        foreach (var player in fightCalculationOrder)
        {
            if (player.Passives.IsDead) continue;

            player.Status.AddFightingData($"\n\n**Logs for round #{game.RoundNo}:**");
            //if block => no one gets points, and no redundant playerAttacked variable
            if (player.Status.IsBlock || player.Status.IsSkip)
            {
                player.Status.AddFightingData($"IsBlock: {player.Status.IsBlock}");
                player.Status.AddFightingData($"IsSkip: {player.Status.IsSkip}");

                // Allow forced attacks (e.g. Котики Штормяк taunt) even when blocking/skipping
                if (player.Status.WhoToAttackThisTurn.Count == 0)
                {
                    //fight Reset — only when truly blocking/skipping with no forced fights
                    await _characterPassives.HandleCharacterAfterFight(player, game, true, false);
                    ResetFight(game, player);
                    continue;
                }
                // else fall through to process forced fights
            }

            var targetsToFight = player.Status.WhoToAttackThisTurn
                .Where(t => t != player.GetPlayerId())
                .Select(t => game.PlayersList.Find(x => x.GetPlayerId() == t))
                .Where(x => x != null && !x.Passives.IsDead)
                .Select(x => (Target: x, BfgDirection: 0, RailgunFight: false))
                .ToList();

            var doomGunState = player.Passives.DoomGuy;
            if (player.GameCharacter.Name == DoomGuy.CharacterName
                && player.GameCharacter.Passive.Any(passive => passive.PassiveName == DoomGuy.Gun)
                && !player.Status.IsBlock && !player.Status.IsSkip
                && doomGunState.GetActive(DoomGuy.Gun) == DoomGuy.Railgun
                && doomGunState.RailgunCharged && targetsToFight.Count > 0)
            {
                var primaryTarget = targetsToFight[0].Target;
                var attackerIndex = game.PlayersList.IndexOf(player);
                var targetIndex = game.PlayersList.IndexOf(primaryTarget);
                var direction = Math.Sign(targetIndex - attackerIndex);
                if (direction != 0)
                {
                    var railgunTargets = new List<GamePlayerBridgeClass> { primaryTarget };
                    railgunTargets.AddRange(game.PlayersList.Where((candidate, index) =>
                        candidate.GetPlayerId() != primaryTarget.GetPlayerId()
                        && Math.Sign(index - attackerIndex) == direction
                        && !candidate.Passives.IsDead
                        && !UnknownBug.Is(candidate)
                        && !Madara.IsSealed(candidate)
                        && !player.IsTeamMember(game, candidate.GetPlayerId())
                        && !Tigr.IsRoundTenBanned(candidate, game.RoundNo)));

                    var originalExtraTargets = targetsToFight.Skip(1).ToList();
                    targetsToFight = railgunTargets
                        .Select(x => (Target: x, BfgDirection: 0, RailgunFight: !UnknownBug.Is(x)))
                        .Concat(originalExtraTargets)
                        .ToList();
                    doomGunState.RailgunCharged = false;
                    player.Status.AddInGamePersonalLogs(
                        $"Рельса: атака охватывает {railgunTargets.Count} врагов на выбранной стороне таблицы.\n");
                    game.AddGlobalLogs($"Рельса пробивает сторону таблицы от {player.DiscordUsername}!");
                }
            }

            if (Naruto.TryCancelHaremFights(
                    game, player, targetsToFight.Select(entry => entry.Target.GetPlayerId())))
            {
                var exploitTarget = targetsToFight.Select(entry => entry.Target)
                    .FirstOrDefault(target => target.Passives.IsExploitable);
                UnknownBug.TryCommitExploit(game, player, exploitTarget, false);
                continue;
            }

            var bfgWaveVisited = new HashSet<Guid> { player.GetPlayerId() };
            foreach (var initialTarget in targetsToFight) bfgWaveVisited.Add(initialTarget.Target.GetPlayerId());

            for (var fightTargetIndex = 0; fightTargetIndex < targetsToFight.Count; fightTargetIndex++)
            {
                if (player.Passives.IsDead) break;
                var playerIamAttacking = targetsToFight[fightTargetIndex].Target;
                if (playerIamAttacking.Passives.IsDead) continue;
                var bfgWaveDirection = targetsToFight[fightTargetIndex].BfgDirection;
                var isRailgunFight = targetsToFight[fightTargetIndex].RailgunFight;

                if (Naruto.IsNarutoPair(player, playerIamAttacking))
                {
                    player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                        Naruto.ShadowClones,
                        "Наруто не могут нападать друг на друга. Бой отменен.",
                        "Shadow Clones",
                        "Narutos cannot attack one another. The fight was canceled.") + "\n");
                    continue;
                }

                if (playerIamAttacking.Passives.Naruto.HaremActiveThisRound
                    && Naruto.TryCancelHaremFights(game, player,
                        targetsToFight.Skip(fightTargetIndex).Select(entry => entry.Target.GetPlayerId())))
                {
                    var exploitTarget = targetsToFight.Skip(fightTargetIndex)
                        .Select(entry => entry.Target)
                        .FirstOrDefault(target => target.Passives.IsExploitable);
                    UnknownBug.TryCommitExploit(game, player, exploitTarget, false);
                    break;
                }

                // Snapshot GlobalLogs length before this fight (for hidden-fight mechanism)
                var globalLogsLenBefore = game.GetGlobalLogs().Length;

                //add skill
                decimal skillGainedFromTarget = 0;
                decimal skillGainedFromClassAttacker = 0;
                decimal skillGainedFromClassDefender = 0;


                player.Status.AddFightingData("\n");
                playerIamAttacking.Status.AddFightingData("\n");
                player.Status.AddFightingData($"**you VS {playerIamAttacking.GameCharacter.Name} ({playerIamAttacking.DiscordUsername})**");
                playerIamAttacking.Status.AddFightingData($"**{player.GameCharacter.Name} ({player.DiscordUsername}) VS you**");

                
                playerIamAttacking.Status.IsFighting = player.GetPlayerId();
                player.Status.IsFighting = playerIamAttacking.GetPlayerId();

                Madara.RegisterIncomingAttacker(game, playerIamAttacking, player);


                // Clear ForOneFight mod tracking for both players
                player.Status.ForOneFightMods.Clear();
                playerIamAttacking.Status.ForOneFightMods.Clear();

                // Snapshot original class before ForOneFight overrides
                var attackerOriginalClass = player.FightCharacter.GetSkillClass();
                var defenderOriginalClass = playerIamAttacking.FightCharacter.GetSkillClass();
                var attackerRealJusticeBeforeFight = player.GameCharacter.Justice.GetRealJusticeNow();

                _characterPassives.HandleDefenseBeforeFight(playerIamAttacking, player, game);

                // Snapshot: mods from defense passives belong to defender
                var defenderPassiveMods = playerIamAttacking.Status.ForOneFightMods
                    .Select(m => new ForOneFightModDto { Source = m.Source, Stat = m.Stat, OriginalValue = Math.Round(m.OriginalValue, 1), NewValue = Math.Round(m.NewValue, 1) })
                    .Concat(player.Status.ForOneFightMods
                        .Select(m => new ForOneFightModDto { Source = m.Source, Stat = m.Stat, OriginalValue = Math.Round(m.OriginalValue, 1), NewValue = Math.Round(m.NewValue, 1), IsOnEnemy = true }))
                    .ToList();
                player.Status.ForOneFightMods.Clear();
                playerIamAttacking.Status.ForOneFightMods.Clear();

                _characterPassives.HandleAttackBeforeFight(player, playerIamAttacking, game);

                // These module overrides are authoritative for this one fight and therefore run
                // after both ordinary before-fight passive dispatchers.
                DoomGuy.ApplyFightModules(player, playerIamAttacking, game);
                var narutoSummonAutoWin = !UnknownBug.Is(playerIamAttacking)
                                          && Naruto.IsSummonAutoWin(player, playerIamAttacking);
                var isTauntBypass = playerIamAttacking.Status.IsBlock
                                    && playerIamAttacking.GameCharacter.Passive.Any(x =>
                                        x.PassiveName == "Штормяк")
                                    && playerIamAttacking.Passives.KotikiStorm.CurrentTauntTarget
                                    == player.GetPlayerId();
                var fightWillResolve =
                    (!playerIamAttacking.Status.IsBlock || player.Status.IsArmorBreak
                                                          || isTauntBypass || narutoSummonAutoWin
                                                          || isRailgunFight)
                    && (!playerIamAttacking.Status.IsSkip || player.Status.IsSkipBreak
                                                            || narutoSummonAutoWin
                                                            || isRailgunFight);
                if (fightWillResolve)
                    JonSnow.ApplyDifficultyJustice(
                        player, playerIamAttacking, _calculateRounds);
                if (fightWillResolve)
                    TheBoys.ApplyKillingCoupleJustice(
                        player, playerIamAttacking, _calculateRounds);

                // This is the authoritative Pickle Rick outcome, applied after both before-fight
                // dispatchers: the active pickle always accepts the fight and always wins it, even
                // when a later attacker passive tried to restore block/skip or disable his victory.
                if (playerIamAttacking.GameCharacter.Passive.Any(x => x.PassiveName == "Огурчик Рик")
                    && playerIamAttacking.Passives.RickPickle.PickleTurnsRemaining > 0
                    && !UnknownBug.Is(player))
                {
                    playerIamAttacking.Passives.RickPickle.WasAttackedAsPickle = true;
                    playerIamAttacking.Status.IsBlock = false;
                    playerIamAttacking.Status.IsSkip = false;
                    player.Status.IsAbleToWin = false;
                    playerIamAttacking.Status.IsAbleToWin = true;
                }

                // Snapshot: mods from attack passives belong to attacker
                var attackerPassiveMods = player.Status.ForOneFightMods
                    .Select(m => new ForOneFightModDto { Source = m.Source, Stat = m.Stat, OriginalValue = Math.Round(m.OriginalValue, 1), NewValue = Math.Round(m.NewValue, 1) })
                    .Concat(playerIamAttacking.Status.ForOneFightMods
                        .Select(m => new ForOneFightModDto { Source = m.Source, Stat = m.Stat, OriginalValue = Math.Round(m.OriginalValue, 1), NewValue = Math.Round(m.NewValue, 1), IsOnEnemy = true }))
                    .ToList();



                game.AddGlobalLogs($"{player.DiscordUsername} <:war:561287719838547981> {playerIamAttacking.DiscordUsername}", "");

                player.Status.AddFightingData($"IsArmorBreak: {player.Status.IsArmorBreak}");
                player.Status.AddFightingData($"IsBlockEnemy: {playerIamAttacking.Status.IsBlock}");
                playerIamAttacking.Status.AddFightingData($"IsBlock: {playerIamAttacking.Status.IsBlock}");
                playerIamAttacking.Status.AddFightingData($"IsArmorBreakEnemy: {player.Status.IsArmorBreak}");

                //if block => no one gets points
                // Штормяк taunt bypass: provoked player fights the taunter normally (not as block)
                if (playerIamAttacking.Status.IsBlock && !player.Status.IsArmorBreak && !isTauntBypass
                    && !narutoSummonAutoWin
                    && !isRailgunFight)
                {
                    player.Status.IsTargetBlocked = playerIamAttacking.GetPlayerId();
                    // var logMess =  await _characterPassives.HandleBlock(player, playerIamAttacking, game);

                    // Sirinoks block — "НЕТ!"
                    if (playerIamAttacking.GameCharacter.Name == "Sirinoks")
                        game.Phrases.SirinoksBlockNoPhrase.SendLog(playerIamAttacking, false);

                    var logMess = " ⟶ *Бой не состоялся...*";
                    if (game.PlayersList.Any(x => x.PlayerType == 1))
                        logMess = " ⟶ *Бой не состоялся (Блок)...*";
                    game.AddGlobalLogs(logMess);


                    var blockPenalty = playerIamAttacking.GameCharacter.Name == DoomGuy.CharacterName
                                       && playerIamAttacking.GameCharacter.Passive.Any(passive =>
                                           passive.PassiveName == DoomGuy.Shield)
                                       && playerIamAttacking.Passives.DoomGuy.GetActive(DoomGuy.Shield) == DoomGuy.SawShield
                        ? -3
                        : -1;
                    if (player.GameCharacter.Name == Madara.CharacterName
                        && player.GameCharacter.Passive.Any(x => x.PassiveName == Madara.SecondMeteorite))
                    {
                        player.Status.AddRegularPoints(2, Madara.SecondMeteorite);
                        game.Phrases.MadaraSecondMeteorite.SendLog(player, false, isRandomOrder: false);
                    }
                    else if (!UnknownBug.Is(player))
                    {
                        player.Status.AddBonusPoints(blockPenalty, "Блок");
                    }

                    var doomShield = playerIamAttacking.Passives.DoomGuy;
                    if (playerIamAttacking.GameCharacter.Name == DoomGuy.CharacterName
                        && playerIamAttacking.GameCharacter.Passive.Any(passive =>
                            passive.PassiveName == DoomGuy.Shield)
                        && doomShield.GetActive(DoomGuy.Shield) == DoomGuy.ShockShield
                        && !doomShield.ShockShieldUsed
                        && !UnknownBug.Is(player))
                    {
                        doomShield.ShockShieldUsed = true;
                        doomShield.ShockSkipTarget = player.GetPlayerId();
                        doomShield.ShockSkipRound = game.RoundNo + 1;
                    }

                    if (playerIamAttacking.GameCharacter.Passive.Any(x => x.PassiveName == "Близнец"))
                    {
                        var previousHighest = playerIamAttacking.Passives.MonsterTwinHighestJusticeThisRound;
                        if (attackerRealJusticeBeforeFight > previousHighest)
                        {
                            playerIamAttacking.Passives.MonsterTwinHighestJusticeThisRound = attackerRealJusticeBeforeFight;
                            playerIamAttacking.GameCharacter.Justice.SetRealJusticeNow(
                                attackerRealJusticeBeforeFight, "Близнец");
                            playerIamAttacking.Status.AddBonusPoints(
                                attackerRealJusticeBeforeFight - Math.Max(0, previousHighest), "Близнец");
                            game.Phrases.MonsterTwinSteal.SendLog(playerIamAttacking, false);
                        }
                    }
                    else
                    {
                        playerIamAttacking.GameCharacter.Justice.AddJusticeForNextRoundFromFight();
                    }

                    // Web fight entry for block
                    game.WebFightLog.Add(new FightEntryDto
                    {
                        AttackerName = player.DiscordUsername,
                        AttackerCharName = player.GameCharacter.Name,
                        AttackerAvatar = GameStateMapper.GetLocalAvatarUrl(player.GameCharacter.AvatarCurrent ?? player.GameCharacter.Avatar),
                        DefenderName = playerIamAttacking.DiscordUsername,
                        DefenderCharName = playerIamAttacking.GameCharacter.Name,
                        DefenderAvatar = GameStateMapper.GetLocalAvatarUrl(playerIamAttacking.GameCharacter.AvatarCurrent ?? playerIamAttacking.GameCharacter.Avatar),
                        Outcome = "block",
                        WinnerName = playerIamAttacking.DiscordUsername,
                        SkillGainedFromTarget = Math.Round(skillGainedFromTarget, 1),
                        SkillGainedFromClassAttacker = Math.Round(skillGainedFromClassAttacker, 1),
                        SkillGainedFromClassDefender = Math.Round(skillGainedFromClassDefender, 1),
                    });

                    MarkHiddenFight(game, player, playerIamAttacking, globalLogsLenBefore);

                    UnknownBug.TryCommitExploit(game, player, playerIamAttacking, false);

                    //fight Reset
                    await _characterPassives.HandleCharacterAfterFight(player, game, true, false);
                    await _characterPassives.HandleCharacterAfterFight(playerIamAttacking, game, false, true);
                    _characterPassives.HandleDefenseAfterBlockOrFight(playerIamAttacking, player, game);
                    _characterPassives.HandleDefenseAfterBlockOrFightOrSkip(playerIamAttacking, player, game);

                    ResetFight(game, player, playerIamAttacking);

                    continue;
                }


                player.Status.AddFightingData($"IsSkipBreak: {player.Status.IsSkipBreak}");
                player.Status.AddFightingData($"IsSkipEnemy: {playerIamAttacking.Status.IsSkip}");
                playerIamAttacking.Status.AddFightingData($"IsSkip: {playerIamAttacking.Status.IsSkip}");
                playerIamAttacking.Status.AddFightingData($"IsSkipBreakEnemy: {player.Status.IsSkipBreak}");

                // if skip => something
                if (playerIamAttacking.Status.IsSkip && !player.Status.IsSkipBreak
                    && !narutoSummonAutoWin && !isRailgunFight)
                {
                    player.Status.IsTargetSkipped = playerIamAttacking.GetPlayerId();
                    game.SkipPlayersThisRound++;

                    var logMess = " ⟶ *Бой не состоялся...*";
                    if (game.PlayersList.Any(x => x.PlayerType == 1))
                        logMess = " ⟶ *Бой не состоялся (Скип)...*";
                    game.AddGlobalLogs(logMess);

                    // Web fight entry for skip
                    game.WebFightLog.Add(new FightEntryDto
                    {
                        AttackerName = player.DiscordUsername,
                        AttackerCharName = player.GameCharacter.Name,
                        AttackerAvatar = GameStateMapper.GetLocalAvatarUrl(player.GameCharacter.AvatarCurrent ?? player.GameCharacter.Avatar),
                        DefenderName = playerIamAttacking.DiscordUsername,
                        DefenderCharName = playerIamAttacking.GameCharacter.Name,
                        DefenderAvatar = GameStateMapper.GetLocalAvatarUrl(playerIamAttacking.GameCharacter.AvatarCurrent ?? playerIamAttacking.GameCharacter.Avatar),
                        Outcome = "skip",
                        SkillGainedFromTarget = Math.Round(skillGainedFromTarget, 1),
                        SkillGainedFromClassAttacker = Math.Round(skillGainedFromClassAttacker, 1),
                        SkillGainedFromClassDefender = Math.Round(skillGainedFromClassDefender, 1),
                    });

                    MarkHiddenFight(game, player, playerIamAttacking, globalLogsLenBefore);

                    UnknownBug.TryCommitExploit(game, player, playerIamAttacking, false);

                    //fight Reset
                    await _characterPassives.HandleCharacterAfterFight(player, game, true, false);
                    await _characterPassives.HandleCharacterAfterFight(playerIamAttacking, game, false, true);
                    _characterPassives.HandleDefenseAfterBlockOrFightOrSkip(playerIamAttacking, player, game);

                    ResetFight(game, player, playerIamAttacking);

                    continue;
                }

                // Монтировка counts only fights that survived every Block/Skip gate.
                var gordonCrowbarWin = GordonFreeman.BeginResolvedFight(
                    player, playerIamAttacking, out var crowbarGordon);
                if (UnknownBug.Is(player) || UnknownBug.Is(playerIamAttacking))
                    gordonCrowbarWin = false;

                //round 1 (nemesis)

                // Skill target gain (moved after block/skip checks so blocked/skipped targets don't give free skill)
                if (player.GameCharacter.HasSkillTargetOn(playerIamAttacking.GameCharacter))
                {
                    var (text1, text2) = CharacterClass.ClassToFlavorText(playerIamAttacking.FightCharacter.GetSkillClassType());

                    skillGainedFromTarget = player.GameCharacter.AddMainSkill(text1);
                    if (skillGainedFromTarget > 0)
                        player.Passives.AchievementTracker.TargetSkillRounds.Add(game.RoundNo);

                    var known = player.Status.KnownPlayerClass.Find(x => x.EnemyId == playerIamAttacking.GetPlayerId());
                    if (known != null)
                        player.Status.KnownPlayerClass.Remove(known);
                    player.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(playerIamAttacking.GetPlayerId(), text2));
                }

                //check skill text — remove stale known-class info when target doesn't match
                if (!player.GameCharacter.HasSkillTargetOn(playerIamAttacking.GameCharacter))
                {
                    var keyword = CharacterClass.ClassToKnownKeyword(player.GameCharacter.GetSkillClassTargetType());
                    if (keyword != "")
                    {
                        var knownEnemy = player.Status.KnownPlayerClass.Find(
                            x => x.EnemyId == playerIamAttacking.GetPlayerId());
                        if (knownEnemy != null && knownEnemy.Text.Contains(keyword))
                            player.Status.KnownPlayerClass.Remove(knownEnemy);
                    }
                }

                //умный (moved after block/skip checks)
                if (player.FightCharacter.GetSkillClass() == "Интеллект" && playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow() == 0)
                {
                    skillGainedFromClassAttacker = player.GameCharacter.AddExtraSkill(6 * player.GameCharacter.GetClassSkillMultiplier(), "Класс");
                }

                //быстрый
                if (playerIamAttacking.FightCharacter.GetSkillClass() == "Скорость")
                    skillGainedFromClassDefender = playerIamAttacking.GameCharacter.AddExtraSkill(2 * playerIamAttacking.GameCharacter.GetClassSkillMultiplier(), "Класс");

                if (player.FightCharacter.GetSkillClass() == "Скорость")
                    skillGainedFromClassAttacker = player.GameCharacter.AddExtraSkill(2 * player.GameCharacter.GetClassSkillMultiplier(), "Класс");


                //main formula:

                //round 1 (Stats)
                var step1 = _calculateRounds.CalculateStep1(player, playerIamAttacking, true);
                var isTooGoodMe = step1.IsTooGoodMe;
                var isTooGoodEnemy = step1.IsTooGoodEnemy;
                var isTooStronkMe = step1.IsTooStronkMe;
                var isTooStronkEnemy = step1.IsTooStronkEnemy;
                var isStatsBetterMe = step1.IsStatsBetterMe;
                var isStatsBettterEnemy = step1.IsStatsBetterEnemy;
                var pointsWined = step1.PointsWon;
                var isNemesisLost = step1.IsNemesisLost;
                var randomForPoint = step1.RandomForPoint;
                var weighingMachine = step1.WeighingMachine;
                var nemesisMultiplier = step1.NemesisMultiplier;
                var round1PointsWon = step1.PointsWon; // save before step2/step3 modify it
                //end round 1


                if (!player.Status.IsAbleToWin)
                {
                    pointsWined += -50;
                }

                if (!playerIamAttacking.Status.IsAbleToWin)
                {
                    pointsWined += 50;

                }

                player.Status.AddFightingData($"IsAbleToWin: {player.Status.IsAbleToWin}");
                player.Status.AddFightingData($"IsAbleToWinEnemy: {playerIamAttacking.Status.IsAbleToWin}");
                playerIamAttacking.Status.AddFightingData($"IsAbleToWin: {playerIamAttacking.Status.IsAbleToWin}");
                playerIamAttacking.Status.AddFightingData($"IsAbleToWinEnemy: {player.Status.IsAbleToWin}");

                // Котики — Storm trick 1: apply +5 weighing machine nudge
                var stormAppeared = false;
                decimal stormWeighingDelta = 0;
                var stormFlipped = false;
                if (stormRb != null && stormRb.SelectedTrickThisRound == 1 && !stormRb.FightProcessed &&
                    stormRb.FightTargetAttackerId == player.GetPlayerId() &&
                    stormRb.FightTargetDefenderId == playerIamAttacking.GetPlayerId() &&
                    !UnknownBug.Is(player) && !UnknownBug.Is(playerIamAttacking))
                {
                    stormAppeared = true;
                    stormRb.FightProcessed = true;
                    var oldSign = Math.Sign(pointsWined);
                    if (pointsWined < 0)
                    {
                        pointsWined += 5;
                        stormWeighingDelta = 5;
                    }
                    else if (pointsWined > 0)
                    {
                        pointsWined -= 5;
                        stormWeighingDelta = -5;
                    }
                    var newSign = Math.Sign(pointsWined);
                    stormFlipped = oldSign != 0 && newSign != 0 && oldSign != newSign;

                    if (stormCarrier != null)
                    {
                        game.Phrases.KotikiStormFightJump.SendLog(stormCarrier, false);
                        game.AddGlobalLogs($"Штормяк запрыгнул в бой {player.DiscordUsername} vs {playerIamAttacking.DiscordUsername}!");
                    }
                }

                //round 2 (Justice)
                var justiceMe = player.GameCharacter.Justice.GetRealJusticeNow();
                var justiceTarget = playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow();
                var step2Points = _calculateRounds.CalculateStep2(player, playerIamAttacking, true);
                pointsWined += step2Points;
                //end round 2


                //round 3 (Random)
                var usedRandomRoll = false;
                var bfgTriggeredThisFight = false;
                var step3RandomNumber = 0;
                var step3MaxRandom = 0m;
                decimal justiceRandomChange = 0;
                decimal nemesisRandomChange = 0;
                if (pointsWined == 0)
                {
                    var doomGun = player.Passives.DoomGuy;
                    var isBfgPrimary = player.GameCharacter.Name == DoomGuy.CharacterName
                                       && player.GameCharacter.Passive.Any(passive =>
                                           passive.PassiveName == DoomGuy.Gun)
                                       && bfgWaveDirection == 0
                                       && doomGun.GetActive(DoomGuy.Gun) == DoomGuy.Bfg
                                       && doomGun.BfgCharged
                                       && !UnknownBug.Is(playerIamAttacking);
                    var isBfgWaveFight = player.GameCharacter.Name == DoomGuy.CharacterName
                                         && player.GameCharacter.Passive.Any(passive =>
                                             passive.PassiveName == DoomGuy.Gun)
                                         && bfgWaveDirection != 0
                                         && doomGun.GetActive(DoomGuy.Gun) == DoomGuy.Bfg
                                         && !UnknownBug.Is(playerIamAttacking);
                    if (isBfgPrimary || isBfgWaveFight)
                    {
                        if (isBfgPrimary)
                        {
                            doomGun.BfgCharged = false;
                            bfgTriggeredThisFight = true;
                        }
                        usedRandomRoll = true;
                        pointsWined = 1;
                        player.Status.AddInGamePersonalLogs(
                            $"BFG{(isBfgWaveFight ? " — ударная волна" : "")}: этап рандома уничтожен. Победа гарантирована.\n");
                        game.AddGlobalLogs(
                            $"BFG{(isBfgWaveFight ? " — ударная волна" : "")} накрыла бой {player.DiscordUsername} против {playerIamAttacking.DiscordUsername}!");
                    }
                    else
                    {
                        var (step3Points, rndNum, rndMax, justiceRandomChangeL, nemesisRandomChangeL) = _calculateRounds.CalculateStep3(player, playerIamAttacking, randomForPoint, nemesisMultiplier, true);
                        pointsWined += step3Points;
                        usedRandomRoll = true;
                        step3RandomNumber = rndNum;
                        step3MaxRandom = rndMax;
                        justiceRandomChange = justiceRandomChangeL;
                        nemesisRandomChange = nemesisRandomChangeL;
                    }
                }
                //end round 3


                var moral = player.Status.GetPlaceAtLeaderBoard() - playerIamAttacking.Status.GetPlaceAtLeaderBoard();


                //octopus  // playerIamAttacking is octopus
                if (!gordonCrowbarWin && !narutoSummonAutoWin && pointsWined <= 0)
                    pointsWined = await _characterPassives.HandleOctopus(playerIamAttacking, player, game);
                //end octopus

                //izanagi  // playerIamAttacking is Itachi (defender)
                if (!gordonCrowbarWin && !narutoSummonAutoWin && pointsWined >= 1
                    && playerIamAttacking.GameCharacter.Passive.Any(p => p.PassiveName == "Изанаги")
                    && playerIamAttacking.Passives.ItachiIzanagi.UsesRemaining > 0
                    && !UnknownBug.Is(player))
                {
                    playerIamAttacking.Passives.ItachiIzanagi.UsesRemaining--;
                    pointsWined = -1;
                    game.Phrases.ItachiIzanagi.SendLog(playerIamAttacking, false);
                }
                //end izanagi

                // A successful summon is the terminal fight result: it also overrides defensive
                // outcome replacers such as active Pickle Rick, Octopus and Izanagi.
                if (!gordonCrowbarWin && narutoSummonAutoWin)
                    pointsWined = 1;

                // The third resolved fight is Gordon's terminal result, including against
                // Pickle, Octopus, Izanagi and Naruto's summon.
                if (gordonCrowbarWin)
                {
                    pointsWined = crowbarGordon.GetPlayerId() == player.GetPlayerId() ? 1 : -1;
                    crowbarGordon.Status.AddInGamePersonalLogs(
                        $"{GordonFreeman.Crowbar}: третий состоявшийся бой выигран.\n");
                }

                // AutoWin is the final combat invariant: terminal outcome replacers may not
                // turn a resolved unknown_bug fight into a loss from either direction.
                if (UnknownBug.Is(player))
                    pointsWined = 1;
                else if (UnknownBug.Is(playerIamAttacking))
                    pointsWined = -1;

                // BFG wave: a guaranteed primary win starts two outward branches. Each branch
                // advances one leaderboard neighbour only while its previous fight was won.
                if (pointsWined >= 1 && (bfgTriggeredThisFight || bfgWaveDirection != 0))
                {
                    var targetIndexOnBoard = game.PlayersList.IndexOf(playerIamAttacking);
                    var directions = bfgTriggeredThisFight ? new[] { -1, 1 } : new[] { bfgWaveDirection };
                    foreach (var direction in directions)
                    {
                        var nextIndex = targetIndexOnBoard + direction;
                        if (nextIndex < 0 || nextIndex >= game.PlayersList.Count) continue;
                        var nextTarget = game.PlayersList[nextIndex];
                        if (nextTarget.Passives.IsDead || Madara.IsSealed(nextTarget)
                            || UnknownBug.Is(nextTarget)
                            || !bfgWaveVisited.Add(nextTarget.GetPlayerId())) continue;
                        targetsToFight.Add((nextTarget, direction, false));
                    }
                }


                //team
                var teamMate = false;
                if (game.Teams.Count > 0)
                {
                    var playerTeamEntry = game.Teams.Find(x => x.TeamPlayers.Contains(player.Status.PlayerId));
                    var playerIamAttackingTeamEntry = game.Teams.Find(x => x.TeamPlayers.Contains(playerIamAttacking.Status.PlayerId));
                    if (playerTeamEntry != null && playerIamAttackingTeamEntry != null && playerTeamEntry.TeamId == playerIamAttackingTeamEntry.TeamId)
                    {
                        teamMate = true;
                    }
                }

                // Quality resist snapshot (declared before if/else so accessible in FightEntryDto creation)
                var resistIntelBefore = 0;
                var resistStrBefore = 0;
                var resistPsycheBefore = 0;
                var resistIntelAfter = 0;
                var resistStrAfter = 0;
                var resistPsycheAfter = 0;
                var dropsBefore = 0;
                var dropsAfter = 0;
                var intellectualDamage = false; // IntelligenceQualityResist broke (<0)
                var emotionalDamage = false;    // PsycheQualityResist broke (<0)
                var qualityDamageApplied = false;
                var fightJusticeChange = 0; // justice gained by the loser
                // Moral snapshots — capture actual change after AddMoral (passives may block)
                decimal attackerMoralActual = 0;
                decimal defenderMoralActual = 0;

                //CheckIfWin to remove Justice
                if (pointsWined >= 1)
                {
                    // Минька: winner never deals harm — skip quality damage and moral loss on opponent
                    var isHarmless = player.GameCharacter.Passive.Any(x => x.PassiveName == "Минька");
                    var dealsHarm = player.GameCharacter.Name != Madara.CharacterName;

                    var point = 1;
                    var winPointRecipients = new List<Guid>();
                    //сильный
                    if (player.FightCharacter.GetSkillClass() == "Сила")
                        skillGainedFromClassAttacker = player.GameCharacter.AddExtraSkill(4 * player.GameCharacter.GetClassSkillMultiplier(), "Класс");

                    isNemesisLost -= 1;
                    game.AddGlobalLogs($" ⟶ {player.DiscordUsername}");

                    //еврей
                    if (!teamMate)
                    {
                        var jewResult = await _characterPassives.HandleJews(player, playerIamAttacking, game);
                        point = jewResult.Point;
                        winPointRecipients = jewResult.Recipients;
                    }
                    if (point == 0) player.Status.AddInGamePersonalLogs("Евреи...\n");
                    //end еврей


                    //add regular points
                    if (!teamMate)
                    {
                        if (stormFlipped && stormCarrier != null)
                        {
                            // Storm redirects the +1 regular point to Storm's carrier
                            stormCarrier.Status.AddRegularPoints(point, "Штормяк: Запрыгнул в бой!");
                            if (point != 0)
                                winPointRecipients = new List<Guid> { stormCarrier.GetPlayerId() };
                        }
                        else if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Никому не нужен" || x.PassiveName == "INT"))
                        {
                            if (point != 0)
                                winPointRecipients.Clear();
                            var winSourceNeg = "Победа";
                            if (player.GameCharacter.Name == "Геральт")
                                winSourceNeg = player.Passives.GeraltContracts.EnemyTypes.ContainsKey(playerIamAttacking.GetPlayerId())
                                               || Salldorum.FindRandomTargetMagnet(game, player)?.GetPlayerId() == playerIamAttacking.GetPlayerId()
                                    ? "Контракт" : "Лут";
                            player.Status.AddWinPoints(game, player, point * -1, winSourceNeg);
                        }
                        else
                        {
                            var winSource = "Победа";
                            if (player.GameCharacter.Name == "Геральт")
                                winSource = player.Passives.GeraltContracts.EnemyTypes.ContainsKey(playerIamAttacking.GetPlayerId())
                                            || Salldorum.FindRandomTargetMagnet(game, player)?.GetPlayerId() == playerIamAttacking.GetPlayerId()
                                    ? "Контракт" : "Лут";
                            player.Status.AddWinPoints(game, player, point, winSource);
                        }
                    }

                    Salldorum.RecordWinPointRecipients(
                        playerIamAttacking,
                        game.RoundNo,
                        player.GetPlayerId(),
                        winPointRecipients);


                    if (!teamMate)
                        player.GameCharacter.Justice.IsWonThisRound = true;

                    // -5 = 1 - 6
                    if (player.Status.GetPlaceAtLeaderBoard() > playerIamAttacking.Status.GetPlaceAtLeaderBoard() && game.RoundNo > 1)
                    {
                        if (!teamMate)
                        {
                            var atkMoralBefore = player.GameCharacter.GetMoral();
                            var defMoralBefore = playerIamAttacking.GameCharacter.GetMoral();
                            player.GameCharacter.AddMoral(moral, "Победа", isFightMoral:true);
                            if (!isHarmless)
                                playerIamAttacking.GameCharacter.AddMoral(moral * -1, "Поражение", isFightMoral: true);
                            attackerMoralActual = player.GameCharacter.GetMoral() - atkMoralBefore;
                            defenderMoralActual = playerIamAttacking.GameCharacter.GetMoral() - defMoralBefore;
                            player.Status.MoralGainedThisFight = attackerMoralActual;
                            playerIamAttacking.Status.MoralGainedThisFight = defenderMoralActual;

                            player.Status.AddFightingData($"moral: {moral} ({player.Status.GetPlaceAtLeaderBoard()} - {playerIamAttacking.Status.GetPlaceAtLeaderBoard()})");
                            playerIamAttacking.Status.AddFightingData($"moral: {moral * -1} ({player.Status.GetPlaceAtLeaderBoard()} - {playerIamAttacking.Status.GetPlaceAtLeaderBoard()})");
                        }
                    }

                    if (!teamMate && !playerIamAttacking.Passives.SaitamaUnnoticed.PretendedLossThisFight)
                        playerIamAttacking.GameCharacter.Justice.AddJusticeForNextRoundFromFight();

                    player.Status.IsWonThisCalculation = playerIamAttacking.GetPlayerId();
                    playerIamAttacking.Status.IsLostThisCalculation = player.GetPlayerId();
                    game.AnyFightThisRound = true; // a fight resolved this round (Tilted / M8)
                    playerIamAttacking.Status.WhoToLostEveryRound.Add(
                        new InGameStatus.WhoToLostPreviousRoundClass(
                            player.GetPlayerId(), game.RoundNo,
                            isTooGoodMe, isTooStronkMe, isStatsBetterMe,
                            isTooGoodEnemy, isTooStronkEnemy, isStatsBettterEnemy,
                            player.GetPlayerId(), playerIamAttacking.Status.GetPlaceAtLeaderBoard(),
                            player.Status.GetPlaceAtLeaderBoard()));

                    //Quality — snapshot resist values before damage
                    var range = player.GameCharacter.GetSpeedQualityResistInt();
                    range -= playerIamAttacking.GameCharacter.GetSpeedQualityKiteBonus();

                    var placeDiff = player.Status.GetPlaceAtLeaderBoard() - playerIamAttacking.Status.GetPlaceAtLeaderBoard();
                    if (placeDiff < 0)
                        placeDiff *= -1;

                    resistIntelBefore = playerIamAttacking.GameCharacter.GetIntelligenceQualityResistInt();
                    resistStrBefore = playerIamAttacking.GameCharacter.GetStrengthQualityResistInt();
                    resistPsycheBefore = playerIamAttacking.GameCharacter.GetPsycheQualityResistInt();
                    dropsBefore = playerIamAttacking.GameCharacter.GetStrengthQualityDropTimes();

                    if (placeDiff <= range && !isHarmless && dealsHarm)
                    {
                        // TheBoys Butcher — normal Кочерга is (1 + poker) Harm; СуперМудень doubles
                        // the complete result, not only the poker bonus: Кочерга #4 = 5 → 10 Harm.
                        var butcherState = player.Passives.TheBoysButcher;
                        var isButcher = !butcherState.ButcherLeft
                                        && player.GameCharacter.Passive.Any(x => x.PassiveName == "Butcher");
                        var superDick = isButcher && butcherState.SuperDickActive;
                        var harmRepeat = isButcher ? 1 + butcherState.PokerCount : 1;
                        if (superDick) harmRepeat *= 2;

                        var queuedExtraHarms = 0;
                        var extraHarmsApplied = 0;
                        var appliedHarmCalls = 0;

                        int ApplyButcherHarm()
                        {
                            if (superDick && (butcherState.SuperDickDropsThisTurn >= 50
                                              || playerIamAttacking.Status.GetScore() <= 0))
                                return 0;

                            appliedHarmCalls++;
                            var dropsBeforeHit = playerIamAttacking.GameCharacter.GetStrengthQualityDropTimes();
                            var brokenResists = playerIamAttacking.GameCharacter
                                .LowerQualityResist(playerIamAttacking, game, player,
                                    superDick ? 50 - butcherState.SuperDickDropsThisTurn : int.MaxValue);
                            if (superDick)
                            {
                                var dropsAfterHit = playerIamAttacking.GameCharacter.GetStrengthQualityDropTimes();
                                butcherState.SuperDickDropsThisTurn += Math.Max(0, dropsAfterHit - dropsBeforeHit);
                            }
                            return brokenResists;
                        }

                        for (var h = 0; h < harmRepeat; h++)
                        {
                            if (superDick && (butcherState.SuperDickDropsThisTurn >= 50
                                              || playerIamAttacking.Status.GetScore() <= 0))
                                break;
                            if (superDick)
                                queuedExtraHarms += ApplyButcherHarm();
                            else
                                ApplyButcherHarm();
                        }

                        // Every actual negative resist effect (Skill −10%, Moral −20%, or Drop) queues
                        // one more Harm. New breaks recurse. Only 50 Drops may land per turn, and zero
                        // score stops the chain; the counter resets when the next turn is prepared.
                        while (superDick && queuedExtraHarms > 0
                               && butcherState.SuperDickDropsThisTurn < 50
                               && playerIamAttacking.Status.GetScore() > 0)
                        {
                            queuedExtraHarms--;
                            queuedExtraHarms += ApplyButcherHarm();
                            extraHarmsApplied++;
                        }

                        if (extraHarmsApplied > 0)
                        {
                            game.AddGlobalLogs($"**{playerIamAttacking.DiscordUsername}**: Что это?! Что он со мной сделал?!");
                            game.AddGlobalLogs("**Butcher**: Отправил на больничную койку.");
                        }

                        if (superDick && appliedHarmCalls > 0 && Madara.IsMadara(playerIamAttacking))
                            player.Passives.AchievementTracker.DamagedReanimatedMadaraWithSuperDick = true;
                        qualityDamageApplied = true;
                    }

                    resistIntelAfter = playerIamAttacking.GameCharacter.GetIntelligenceQualityResistInt();
                    resistStrAfter = playerIamAttacking.GameCharacter.GetStrengthQualityResistInt();
                    resistPsycheAfter = playerIamAttacking.GameCharacter.GetPsycheQualityResistInt();
                    dropsAfter = playerIamAttacking.GameCharacter.GetStrengthQualityDropTimes();
                    var achievementDrops = Math.Max(0, dropsAfter - dropsBefore);
                    player.Passives.AchievementTracker.DropsCaused += achievementDrops;
                    if (player.Passives.TheBoysButcher.SuperDickActive)
                        player.Passives.AchievementTracker.SuperDickDropsCaused += achievementDrops;
                    // Detect if intel/psyche resist broke (went below 0 and was reset)
                    intellectualDamage = qualityDamageApplied && resistIntelAfter > resistIntelBefore;
                    emotionalDamage = qualityDamageApplied && resistPsycheAfter > resistPsycheBefore;

                    // TheBoys Butcher — +1 regular point (×2 SuperDick) for EACH Drop of a marked sup.
                    // (finding M7: the point is for every "Скинуть", not merely one payout per fight).
                    // The +10 Skill for hunting a sup stays in the CP "Butcher" case (win or loss).
                    if (dropsAfter > dropsBefore
                        && player.GameCharacter.Passive.Any(x => x.PassiveName == "Butcher")
                        && !player.Passives.TheBoysButcher.ButcherLeft
                        && playerIamAttacking.Passives.TheBoysSupMark)
                    {
                        var dropReward = (dropsAfter - dropsBefore)
                                         * (player.Passives.TheBoysButcher.SuperDickActive ? 2 : 1);
                        player.Status.AddRegularPoints(dropReward, "Butcher");
                    }

                    // Justice: loser (defender) gains +1 justice
                    if (!teamMate && !playerIamAttacking.Passives.SaitamaUnnoticed.PretendedLossThisFight)
                        fightJusticeChange = 1;

                    //end Quality
                }
                else
                {
                    //сильный
                    if (playerIamAttacking.FightCharacter.GetSkillClass() == "Сила")
                        skillGainedFromClassDefender = playerIamAttacking.GameCharacter.AddExtraSkill(4 * playerIamAttacking.GameCharacter.GetClassSkillMultiplier(), "Класс");

                    if (isTooGoodEnemy && !isTooStronkEnemy)
                        player.Status.AddInGamePersonalLogs($"{playerIamAttacking.DiscordUsername} is __TOO GOOD__ for you\n");
                    if (isTooStronkEnemy)
                        player.Status.AddInGamePersonalLogs($"{playerIamAttacking.DiscordUsername} is __TOO STONK__ for you\n");

                    isNemesisLost += 1;


                    game.AddGlobalLogs($" ⟶ {playerIamAttacking.DiscordUsername}");

                    var defenderWinPointRecipients = new List<Guid>();
                    if (!teamMate)
                    {
                        if (stormFlipped && stormCarrier != null)
                        {
                            stormCarrier.Status.AddRegularPoints(1, "Штормяк: Запрыгнул в бой!");
                            defenderWinPointRecipients.Add(stormCarrier.GetPlayerId());
                        }
                        else if (playerIamAttacking.GameCharacter.Passive.Any(x => x.PassiveName == "INT"))
                            // Toxic Mate "INT": "Побеждая — теряет очки" applies on a defence win too (finding M4);
                            // HardKitty's "Никому не нужен" stays attacker-only ("если напал и победил").
                            playerIamAttacking.Status.AddWinPoints(game, playerIamAttacking, -1, "Победа");
                        else if (!playerIamAttacking.GameCharacter.Passive.Any(x => x.PassiveName == "На мели"))
                        {
                            var defWinSource = "Победа";
                            if (playerIamAttacking.GameCharacter.Name == "Геральт")
                                defWinSource = playerIamAttacking.Passives.GeraltContracts.EnemyTypes.ContainsKey(player.GetPlayerId())
                                               || Salldorum.FindRandomTargetMagnet(game, playerIamAttacking)?.GetPlayerId() == player.GetPlayerId()
                                    ? "Контракт" : "Лут";
                            playerIamAttacking.Status.AddWinPoints(game, playerIamAttacking, 1, defWinSource);
                            if (!UnknownBug.Is(playerIamAttacking))
                                defenderWinPointRecipients.Add(playerIamAttacking.GetPlayerId());
                        }
                    }

                    Salldorum.RecordWinPointRecipients(
                        player,
                        game.RoundNo,
                        playerIamAttacking.GetPlayerId(),
                        defenderWinPointRecipients);



                    if (!teamMate)
                        playerIamAttacking.GameCharacter.Justice.IsWonThisRound = true;

                    if (player.Status.GetPlaceAtLeaderBoard() < playerIamAttacking.Status.GetPlaceAtLeaderBoard() && game.RoundNo > 1)
                    {
                        if (!teamMate)
                        {
                            var atkMoralBefore = player.GameCharacter.GetMoral();
                            var defMoralBefore = playerIamAttacking.GameCharacter.GetMoral();
                            player.GameCharacter.AddMoral(moral, "Поражение", isFightMoral: true);
                            playerIamAttacking.GameCharacter.AddMoral(moral * -1, "Победа", isFightMoral: true);
                            attackerMoralActual = player.GameCharacter.GetMoral() - atkMoralBefore;
                            defenderMoralActual = playerIamAttacking.GameCharacter.GetMoral() - defMoralBefore;
                            player.Status.MoralGainedThisFight = attackerMoralActual;
                            playerIamAttacking.Status.MoralGainedThisFight = defenderMoralActual;

                            player.Status.AddFightingData($"moral: {moral} ({player.Status.GetPlaceAtLeaderBoard()} - {playerIamAttacking.Status.GetPlaceAtLeaderBoard()})");
                            playerIamAttacking.Status.AddFightingData($"moral: {moral * -1} ({player.Status.GetPlaceAtLeaderBoard()} - {playerIamAttacking.Status.GetPlaceAtLeaderBoard()})");
                        }
                    }

                    if (playerIamAttacking.GameCharacter.Passive.Any(x => x.PassiveName == "Раммус мейн") && playerIamAttacking.Status.IsBlock)
                        if (!teamMate)
                            playerIamAttacking.GameCharacter.Justice.IsWonThisRound = false;

                    if (!teamMate)
                    {
                        player.GameCharacter.Justice.AddJusticeForNextRoundFromFight();
                        fightJusticeChange = 1; // loser (attacker) gains +1 justice
                    }

                    playerIamAttacking.Status.IsWonThisCalculation = player.GetPlayerId();
                    player.Status.IsLostThisCalculation = playerIamAttacking.GetPlayerId();
                    game.AnyFightThisRound = true; // a fight resolved this round (Tilted / M8)
                    player.Status.WhoToLostEveryRound.Add(
                        new InGameStatus.WhoToLostPreviousRoundClass(
                            playerIamAttacking.GetPlayerId(), game.RoundNo,
                            isTooGoodEnemy, isTooStronkEnemy, isStatsBettterEnemy,
                            isTooGoodMe, isTooStronkMe, isStatsBetterMe,
                            player.GetPlayerId(), player.Status.GetPlaceAtLeaderBoard(),
                            playerIamAttacking.Status.GetPlaceAtLeaderBoard()));
                }

                // ── Collect structured fight data for web animation ──
                {
                    var me = player.GameCharacter;
                    var target = playerIamAttacking.GameCharacter;
                    var attackerWon = pointsWined >= 1;

                    // Resist/drop data — only set when attacker won (quality damage only applies to loser)
                    var resistIntelDmg = 0;
                    var resistStrDmg = 0;
                    var resistPsycheDmg = 0;
                    var fightDrops = 0;
                    var fightDroppedPlayer = "";
                    var fightQualityApplied = false;
                    var fightIntellectualDmg = false;
                    var fightEmotionalDmg = false;

                    if (attackerWon)
                    {
                        resistIntelDmg = resistIntelBefore - resistIntelAfter;
                        resistStrDmg = resistStrBefore - resistStrAfter;
                        resistPsycheDmg = resistPsycheBefore - resistPsycheAfter;
                        fightDrops = dropsAfter - dropsBefore; // actual drops from StrengthQualityDropTimes
                        fightDroppedPlayer = fightDrops > 0 ? playerIamAttacking.DiscordUsername : "";
                        fightQualityApplied = qualityDamageApplied;
                        fightIntellectualDmg = intellectualDamage;
                        fightEmotionalDmg = emotionalDamage;
                    }

                    game.WebFightLog.Add(new FightEntryDto
                    {
                        AttackerName = player.DiscordUsername,
                        AttackerCharName = me.Name,
                        AttackerAvatar = GameStateMapper.GetLocalAvatarUrl(me.AvatarCurrent ?? me.Avatar),
                        DefenderName = playerIamAttacking.DiscordUsername,
                        DefenderCharName = target.Name,
                        DefenderAvatar = GameStateMapper.GetLocalAvatarUrl(target.AvatarCurrent ?? target.Avatar),
                        Outcome = attackerWon ? "win" : "loss",
                        WinnerName = attackerWon ? player.DiscordUsername : playerIamAttacking.DiscordUsername,
                        AttackerClass = step1.AttackerClass,
                        DefenderClass = step1.DefenderClass,
                        AttackerOriginalClass = attackerOriginalClass != step1.AttackerClass ? attackerOriginalClass : "",
                        DefenderOriginalClass = defenderOriginalClass != step1.DefenderClass ? defenderOriginalClass : "",
                        VersatilityIntel = step1.VersatilityIntel,
                        VersatilityStr = step1.VersatilityStr,
                        VersatilitySpeed = step1.VersatilitySpeed,
                        ScaleMe = Math.Round(step1.ScaleMe, 2),
                        ScaleTarget = Math.Round(step1.ScaleTarget, 2),
                        IsNemesisMe = step1.IsNemesisMe,
                        IsNemesisTarget = step1.IsNemesisTarget,
                        NemesisMultiplier = nemesisMultiplier,
                        SkillMultiplierMe = (int)step1.SkillMultiplierMe,
                        SkillMultiplierTarget = (int)step1.SkillMultiplierTarget,
                        PsycheDifference = player.FightCharacter.GetPsyche() - playerIamAttacking.FightCharacter.GetPsyche(),
                        WeighingMachine = Math.Round(weighingMachine, 2),
                        IsTooGoodMe = isTooGoodMe,
                        IsTooGoodEnemy = isTooGoodEnemy,
                        IsTooStronkMe = isTooStronkMe,
                        IsTooStronkEnemy = isTooStronkEnemy,
                        IsStatsBetterMe = isStatsBetterMe,
                        IsStatsBetterEnemy = isStatsBettterEnemy,
                        RandomForPoint = Math.Round(randomForPoint, 2),
                        // Round 1 per-step deltas
                        NemesisWeighingDelta = Math.Round(step1.NemesisWeighingDelta, 2),
                        ScaleWeighingDelta = Math.Round(step1.ScaleWeighingDelta, 2),
                        VersatilityWeighingDelta = Math.Round(step1.VersatilityWeighingDelta, 2),
                        PsycheWeighingDelta = Math.Round(step1.PsycheWeighingDelta, 2),
                        SkillWeighingDelta = Math.Round(step1.SkillWeighingDelta, 2),
                        JusticeWeighingDelta = Math.Round(step1.JusticeWeighingDelta, 2),
                        // Round 3 random modifiers
                        TooGoodRandomChange = Math.Round(step1.TooGoodRandomChange, 2),
                        TooStronkRandomChange = Math.Round(step1.TooStronkRandomChange, 2),
                        JusticeRandomChange = Math.Round(justiceRandomChange, 2),
                        NemesisRandomChange = Math.Round(nemesisRandomChange, 2),
                        // Round results
                        Round1PointsWon = round1PointsWon,
                        JusticeMe = (int)justiceMe,
                        JusticeTarget = (int)justiceTarget,
                        PointsFromJustice = step2Points,
                        UsedRandomRoll = usedRandomRoll,
                        RandomNumber = step3RandomNumber,
                        MaxRandomNumber = step3MaxRandom,
                        TotalPointsWon = pointsWined,
                        MoralChange = moral,
                        AttackerMoralChange = Math.Round(attackerMoralActual, 1),
                        DefenderMoralChange = Math.Round(defenderMoralActual, 1),
                        // Resist/drop details
                        ResistIntelDamage = resistIntelDmg,
                        ResistStrDamage = resistStrDmg,
                        ResistPsycheDamage = resistPsycheDmg,
                        Drops = fightDrops,
                        DroppedPlayerName = fightDroppedPlayer,
                        QualityDamageApplied = fightQualityApplied,
                        IntellectualDamage = fightIntellectualDmg,
                        EmotionalDamage = fightEmotionalDmg,
                        JusticeChange = fightJusticeChange,
                        SkillGainedFromTarget = Math.Round(skillGainedFromTarget, 1),
                        SkillGainedFromClassAttacker = Math.Round(skillGainedFromClassAttacker, 1),
                        SkillGainedFromClassDefender = Math.Round(skillGainedFromClassDefender, 1),
                        SkillDifferenceRandomModifier = Math.Round(step1.SkillDifferenceRandomModifier, 2),
                        NemesisMultiplierSkillDifference = Math.Round(step1.NemesisMultiplierSkillDifference, 2),
                        AttackerForOneFightMods = attackerPassiveMods,
                        DefenderForOneFightMods = defenderPassiveMods,
                        StormAppeared = stormAppeared,
                        StormWeighingDelta = stormWeighingDelta,
                        StormFlipped = stormFlipped,
                    });
                }

                var attackerWonThisFight =
                    player.Status.IsWonThisCalculation == playerIamAttacking.GetPlayerId();
                var resolvedWinner = attackerWonThisFight ? player : playerIamAttacking;
                var resolvedLoser = attackerWonThisFight ? playerIamAttacking : player;

                if (gordonCrowbarWin
                    && player.GameCharacter.Name == "TheBoys"
                    && player.Passives.TheBoysButcher.SuperDickActive
                    && GordonFreeman.Is(playerIamAttacking)
                    && resolvedWinner.GetPlayerId() == playerIamAttacking.GetPlayerId())
                    playerIamAttacking.Passives.AchievementTracker.GordonCrowbarStoppedSuperDick = true;

                UnknownBug.RecordResolvedFight(game, resolvedWinner, resolvedLoser);
                UnknownBug.TryCommitExploit(
                    game, player, playerIamAttacking, attackerWonThisFight);

                switch (isNemesisLost)
                {
                    case 3:
                        player.Status.AddInGamePersonalLogs($"Поражение: {playerIamAttacking.DiscordUsername} {GetLostNemesisText(playerIamAttacking, player)}\n");
                        break;
                    case -3:
                        playerIamAttacking.Status.AddInGamePersonalLogs($"Поражение: {player.DiscordUsername} {GetLostNemesisText(player, playerIamAttacking)}\n");
                        break;
                }

                // Set fight context for goblin death percentage calculation
                playerIamAttacking.Status.FightEnemyWasTooGood = isTooGoodMe;
                playerIamAttacking.Status.FightEnemyWasTooStronk = isTooStronkMe;
                player.Status.FightEnemyWasTooGood = isTooGoodEnemy;
                player.Status.FightEnemyWasTooStronk = isTooStronkEnemy;

                //т.е. он получил урон, какие у него дебаффы на этот счет
                _characterPassives.HandleDefenseAfterFight(playerIamAttacking, player, game);
                _characterPassives.HandleDefenseAfterBlockOrFight(playerIamAttacking, player, game);
                _characterPassives.HandleDefenseAfterBlockOrFightOrSkip(playerIamAttacking, player, game);

                //т.е. я его аттакую, какие у меня бонусы на это
                _characterPassives.HandleAttackAfterFight(player, playerIamAttacking, game);

                //fight Reset
                await _characterPassives.HandleCharacterAfterFight(player, game, true, false);
                await _characterPassives.HandleCharacterAfterFight(playerIamAttacking, game, false, true);

                JonSnow.HandleResolvedFight(
                    game,
                    player,
                    playerIamAttacking,
                    resolvedWinner,
                    resolvedLoser);
                Cthulhu.HandleResolvedFight(
                    game,
                    player,
                    playerIamAttacking,
                    resolvedWinner,
                    resolvedLoser);
                
                _characterPassives.HandleShark(game); //used only for shark...

                // Clear fight context flags
                playerIamAttacking.Status.FightEnemyWasTooGood = false;
                playerIamAttacking.Status.FightEnemyWasTooStronk = false;
                player.Status.FightEnemyWasTooGood = false;
                player.Status.FightEnemyWasTooStronk = false;

                // Hide private fights in every outcome path and preserve Dopa's second action as a
                // separately styled shadow row for the participants/admin.
                MarkHiddenFight(game, player, playerIamAttacking, globalLogsLenBefore);

                // Mark Portal Gun swap on the WebFightLog entry
                if (player.Passives.RickPortalGun.SwapActive)
                {
                    var lastFight = game.WebFightLog.LastOrDefault();
                    if (lastFight != null)
                        lastFight.PortalGunSwap = true;
                }

                // ── Achievement tracking: fight wins/losses (before ResetFight clears flags) ──
                var achievementAttackerWon = player.Status.IsWonThisCalculation == playerIamAttacking.GetPlayerId();
                var achievementDefenderWon = playerIamAttacking.Status.IsWonThisCalculation == player.GetPlayerId();

                if (achievementAttackerWon || achievementDefenderWon)
                {
                    player.Passives.AchievementTracker.FoughtCharacterNames.Add(playerIamAttacking.GameCharacter.Name);
                    playerIamAttacking.Passives.AchievementTracker.FoughtCharacterNames.Add(player.GameCharacter.Name);
                }

                if (achievementAttackerWon)
                {
                    var attackerTracker = player.Passives.AchievementTracker;
                    if (player.Status.GetPlaceAtLeaderBoard() == 6
                        && playerIamAttacking.Status.GetPlaceAtLeaderBoard() == 1)
                        attackerTracker.BottomFeederWins++;
                    if (player.FightCharacter.HasNemesisOver(playerIamAttacking.FightCharacter))
                        attackerTracker.NemesisAdvantageWins++;
                    if (justiceMe >= 5)
                        attackerTracker.WonFightWithMaxJustice = true;
                    if (bfgTriggeredThisFight || bfgWaveDirection != 0)
                        attackerTracker.BfgWaveVictimIds.Add(playerIamAttacking.GetPlayerId());

                    if (player.GameCharacter.Name == "Загадочный Спартанец в маске"
                        && game.RoundNo == 10
                        && playerIamAttacking.GameCharacter.Name == "Sirinoks"
                        && playerIamAttacking.GameCharacter.Passive.Any(x => x.PassiveName == "Дракон")
                        && attackerTracker.SpartanDragonSlayerTriggered)
                        attackerTracker.SpartanDragonSlayerDefeated = true;
                }

                if (achievementDefenderWon)
                {
                    var defenderTracker = playerIamAttacking.Passives.AchievementTracker;
                    if (playerIamAttacking.FightCharacter.HasNemesisOver(player.FightCharacter))
                        defenderTracker.NemesisAdvantageWins++;
                    if (justiceTarget >= 5)
                        defenderTracker.WonFightWithMaxJustice = true;
                }

                foreach (var fightParticipant in new[] { player, playerIamAttacking })
                {
                    var t = fightParticipant.Passives.AchievementTracker;
                    if (fightParticipant.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        t.TotalFightsWon++;
                        t.ConsecutiveWins++;
                        if (t.ConsecutiveWins > t.MaxConsecutiveWins)
                            t.MaxConsecutiveWins = t.ConsecutiveWins;
                        var defeatedId = fightParticipant.Status.IsWonThisCalculation;
                        var defeated = game.PlayersList.Find(x => x.GetPlayerId() == defeatedId);
                        if (defeated != null)
                        {
                            t.DefeatedPlayerNames.Add(defeated.DiscordUsername);
                            t.DefeatedPlayerIds.Add(defeated.GetPlayerId());
                            t.DefeatedCharacterNames.Add(defeated.GameCharacter.Name);
                        }
                    }
                    if (fightParticipant.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        t.TotalFightsLost++;
                        t.ConsecutiveWins = 0;
                    }
                }

                // Consume only actual resolved contract fights. The remaining budget was captured
                // when Geralt's contracts injected the fight queue, so blocks/skips never count.
                if (achievementAttackerWon || achievementDefenderWon)
                {
                    var geralt = player.GameCharacter.Name == "Геральт"
                        ? player
                        : playerIamAttacking.GameCharacter.Name == "Геральт"
                            ? playerIamAttacking
                            : null;
                    if (geralt != null)
                    {
                        var opponentId = geralt.GetPlayerId() == player.GetPlayerId()
                            ? playerIamAttacking.GetPlayerId()
                            : player.GetPlayerId();
                        var geraltTracker = geralt.Passives.AchievementTracker;
                        if (geraltTracker.GeraltContractFightsRemaining.TryGetValue(opponentId, out var remaining)
                            && remaining > 0)
                        {
                            geraltTracker.GeraltContractFightsRemaining[opponentId] = remaining - 1;
                            geraltTracker.GeraltContractFightsResolved++;
                        }
                    }
                }

                // The mutual-Psyche scene happens before its first fight. A victory in that same
                // fight is not "later"; the opponent becomes eligible only after this reset point.
                var spartanParticipant = player.GameCharacter.Name == "Загадочный Спартанец в маске"
                    ? player
                    : playerIamAttacking.GameCharacter.Name == "Загадочный Спартанец в маске"
                        ? playerIamAttacking
                        : null;
                var mylorikParticipant = player.GameCharacter.Name == "mylorik"
                    ? player
                    : playerIamAttacking.GameCharacter.Name == "mylorik"
                        ? playerIamAttacking
                        : null;
                if (spartanParticipant != null && mylorikParticipant != null)
                {
                    var spartanTracker = spartanParticipant.Passives.AchievementTracker;
                    if (spartanParticipant.Status.IsWonThisCalculation == mylorikParticipant.GetPlayerId()
                        && spartanTracker.SpartanRespectedOpponentIds.Contains(mylorikParticipant.GetPlayerId())
                        && spartanTracker.SpartanRespectTriggeredThisFight != mylorikParticipant.GetPlayerId())
                        spartanTracker.SpartanDefeatedMylorikAfterRespect = true;
                }

                ResetFight(game, player, playerIamAttacking);
            }
        }

        if (game.RoundNo == 8)
        {
            foreach (var participant in game.PlayersList)
                participant.WebMediaMessages.RemoveAll(entry =>
                    entry.PassiveName == Madara.SusanooClones
                    && entry.FileUrl?.EndsWith("madara_tsukuemi_theme.mp3", StringComparison.OrdinalIgnoreCase) == true);
        }

        // Rumbling is deliberately the first post-fight passive settlement on round 10.
        _characterPassives.HandleRumblingAfterFights(game);
        Naruto.SettleShadowClones(game);
        Cthulhu.ResolveNechtoAttacks(game, _calculateRounds, _charactersPull);
        Cthulhu.HandleEndOfRound(game);















        // ── Achievement tracking: per-round stats (fight wins/losses tracked in fight loop above) ──
        foreach (var player in game.PlayersList)
        {
            if (player.Passives.IsDead) continue;
            var t = player.Passives.AchievementTracker;
            // Track blocks and attacks
            if (player.Status.IsBlock)
            {
                t.TotalBlocksUsed++;
                t.NeverAttacked = t.NeverAttacked; // block doesn't count as attack
            }
            else
            {
                t.NeverBlocked = t.NeverBlocked; // keep checking
            }
            if (player.Status.WhoToAttackThisTurn.Count > 0 && !player.Status.IsBlock && !player.Status.IsSkip)
                t.NeverAttacked = false;
            if (player.Status.IsBlock)
                t.NeverBlocked = false;
            // Position tracking
            var pos = player.Status.GetPlaceAtLeaderBoard();
            if (pos == 1) t.RoundsAtFirst++;
            if (pos == 6) t.RoundsAtLast++;
            // Detect last-to-first and first-to-last transitions
            if (game.RoundNo > 1)
            {
                var history = player.Status.PlaceAtLeaderBoardHistory;
                if (history != null && history.Count > 0)
                {
                    if (history.Any(h => h.Place == 6) && pos == 1)
                        t.CameFromLastToFirst = true;
                    if (history.Any(h => h.Place == 1) && pos == 6)
                        t.WentFromFirstToLast = true;
                }
            }
            // Track justice
            var justice = player.GameCharacter.Justice.GetRealJusticeNow();
            if (justice > t.JusticeReached)
                t.JusticeReached = justice;
        }

        await _characterPassives.HandleEndOfRound(game);

        if (GordonFreeman.PrepareHalfLifeSettlement(game))
        {
            watch.Stop();
            _pendingRounds[game.GameId] = new PendingRoundContinuation(replayRound, watch);
            return false;
        }

        await CompleteRoundAsync(game, replayRound, watch);
        return true;
    }

    private async Task CompleteRoundAsync(
        GameClass game,
        ReplayRoundDto replayRound,
        Stopwatch watch)
    {

        foreach (var player in game.PlayersList)
        {
            player.Status.TimesUpdated = 0;
            player.Status.IsAutoMove = false;
            player.Status.IsBlock = false;
            player.Status.IsSkipBreak = false;
            player.Status.IsArmorBreak = false;
            player.Status.IsAbleToWin = true;
            player.Status.IsSkip = false;
            player.Status.IsReady = false;

            // Auto-ready dead players so they don't block the game
            if (player.Passives.IsDead)
            {
                player.Status.IsReady = true;
                player.Status.IsBlock = true;
                player.Status.ConfirmedPredict = true;
            }

            player.Status.WhoToAttackThisTurn = new List<Guid>();
            player.Status.MoveListPage = 1;
            player.Status.IsAbleToChangeMind = true;
            player.Status.RoundNumber = game.RoundNo+1;

            if (!player.Passives.IsDead)
            {
                player.GameCharacter.SetSpeedResist();
                player.GameCharacter.NormalizeMoral();
                var justicePhrase = player.GameCharacter.Justice.HandleEndOfRoundJustice();
                if (justicePhrase)
                    game.Phrases.JusticePhrase.SendLogSeparateWeb(player, delete:false, isEvent:false);
            }
            else
            {
                if (!UnknownBug.Is(player))
                {
                    player.Status.SetScoresToGiveAtEndOfRound(0, "", false);
                    player.GameCharacter.SetBonusPointsFromMoral(0);
                }
            }

            var scoreBeforeRoundSettlement = player.Status.GetScore();
            if (!player.Passives.IsDead || UnknownBug.Is(player))
                player.Status.CombineRoundScoreAndGameScore(
                    game, GordonFreeman.ConsumeSettlementOverride(player));
            if (game.RoundNo == 10)
                player.Passives.AchievementTracker.RoundTenRegularPoints =
                    player.Status.GetScore() - scoreBeforeRoundSettlement;
            player.Status.ClearInGamePersonalLogs();
            player.Status.InGamePersonalLogsAll += "|||";

        }

        //Возвращение из мертвых
        //game.PlayersList = game.PlayersList.Where(x => !x.Passives.IsDead).ToList();

        if (game.IsKratosEvent
            && game.PlayersList.Count(x => x.Passives.IsDead && x.GameCharacter.Name != "Кратос") == 5)
        {
            game.IsKratosEvent = false;
            game.AddGlobalLogs("Все боги были убиты, открылась коробка Пандоры, стихийные бедствия уничтожили всё живое...");
            game.PlayersList.Find(x => x.GameCharacter.Name == "Кратос")?.Status
                .AddInGamePersonalLogs("By the gods, what have I become?\n");
        }
        //end Возвращение из мертвых

        // Freeze the same round's results before RoundNo++ and HandleNextRound can apply a ban,
        // tilt, score mutation or any other next-turn state to this replay entry (finding M24).
        ReplayService.CaptureRoundResult(replayRound, game, _gameUpdateMess);

        game.SkipPlayersThisRound = 0;
        game.RoundNo++;
        JonSnow.ExpireBlackCastleBeforeScoreSort(game);

        if (game.GameMode == "aram" && game.RoundNo == 2)
        {
            game.TurnLengthInSecond = 300;
        }


        await _characterPassives.HandleNextRound(game);


        // Save Storm bite lock positions BEFORE score sort
        var stormBiteLocks = new Dictionary<Guid, int>();
        {
            var kotikiOwnerBite = game.PlayersList.Find(x => x.GameCharacter.Name == "Котики");
            if (kotikiOwnerBite != null)
            {
                var rbBiteDm = kotikiOwnerBite.Passives.KotikiRandomBehavior;
                if (rbBiteDm.BiteLockActiveUntilRound >= game.RoundNo && rbBiteDm.BiteTargetId != Guid.Empty)
                {
                    var bitePlayer = game.PlayersList.Find(x => x.GetPlayerId() == rbBiteDm.BiteTargetId);
                    if (bitePlayer != null)
                        stormBiteLocks[bitePlayer.GetPlayerId()] = game.PlayersList.IndexOf(bitePlayer);
                }
            }
        }

        // Save ziggurat positions BEFORE score sort so they can be restored after
        var zigguratPositionLocks = new Dictionary<Guid, int>();
        foreach (var pl in game.PlayersList)
        {
            if (pl.Passives.GoblinZiggurat.IsInZiggurat && pl.Passives.GoblinZiggurat.ZigguratStayRoundsLeft > 0)
            {
                zigguratPositionLocks[pl.GetPlayerId()] = game.PlayersList.IndexOf(pl);
            }
        }

        game.PlayersList = Naruto.OrderLeaderboard(game.PlayersList);


        //Тигр топ, а ты холоп
        foreach (var player in game.PlayersList.Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Тигр топ, а ты холоп")).ToList())
        {
            // Banned on the last round ("Стримснайпят и банят...") — the ban neutralises Tigr ("не может
            // действовать"), so his "Тигр топ" swap must not still vault him to first place. Mirrors the
            // round-10 ban carve-out used by the Монстр forced-attack in CheckIfReady.
            if (Tigr.IsRoundTenBanned(player, game.RoundNo))
                continue;

            var tigr = player.Passives.TigrTop;

            if (tigr is { TimeCount: > 0 })
            {
                // Can't swap a player in ziggurat or displace unknown_bug.
                if (game.PlayersList.First().Passives.GoblinZiggurat.IsInZiggurat
                    || UnknownBug.Is(game.PlayersList.First()))
                {
                    if (game.PlayersList.First().Passives.GoblinZiggurat.IsInZiggurat)
                        player.WebMessages.Add("🏛️ Зиккурат Гоблинов защищает первое место!");
                    continue;
                }

                var tigrIndex = game.PlayersList.IndexOf(player);

                game.PlayersList[tigrIndex] = game.PlayersList.First();
                game.PlayersList[0] = player;
                tigr.TimeCount--;
                // game.Phrases.TigrTop.SendLog(tigrTemp);
            }
        }
        //end Тигр топ, а ты холоп


        //Portal Gun position swap
        foreach (var p in game.PlayersList
            .Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Портальная пушка")).ToList())
        {
            var gun = p.Passives.RickPortalGun;
            if (gun.SwapActive)
            {
                var swapTarget = game.PlayersList.Find(x => x.GetPlayerId() == gun.SwappedWith);
                if (swapTarget?.Passives.GoblinZiggurat.IsInZiggurat == true)
                {
                    p.WebMessages.Add("🏛️ Зиккурат защищает позицию цели! Телепортация отменена.");
                    gun.SwapActive = false;
                    gun.SwappedWith = Guid.Empty;
                    continue;
                }
                if (swapTarget != null)
                {
                    var rickIdx = game.PlayersList.IndexOf(p);
                    var targetIdx = game.PlayersList.IndexOf(swapTarget);
                    game.PlayersList[rickIdx] = swapTarget;
                    game.PlayersList[targetIdx] = p;
                }
                gun.SwapActive = false;
                gun.SwappedWith = Guid.Empty;
            }
        }
        //end Portal Gun position swap


        //Никому не нужен
        foreach (var player in game.PlayersList.Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Никому не нужен")).ToList())
        {
            var hardIndex = game.PlayersList.IndexOf(player);

            for (var k = hardIndex; k < game.PlayersList.Count - 1; k++)
                game.PlayersList[k] = game.PlayersList[k + 1];

            game.PlayersList[^1] = player;
        }
        //end Никому не нужен

        // Овца в загоне: Eren remains at the foot of the hill through round 8.
        if (game.RoundNo <= 8)
        {
            var eren = game.PlayersList.Find(x =>
                x.GameCharacter.Name == ErenYeager.CharacterName
                && x.GameCharacter.Passive.Any(y => y.PassiveName == ErenYeager.Sheep));
            if (eren != null)
                ErenYeager.MoveToLast(game.PlayersList, eren);
        }


        //sort
        for (var i = 0; i < game.PlayersList.Count; i++)
        {
            if (game.RoundNo is 3 or 5 or 7 or 9
                && game.PlayersList[i].GameCharacter.Name != Madara.CharacterName)
            {
                game.PlayersList[i].Status.LvlUpPoints++;
                game.PlayersList[i].Status.MoveListPage = 3;
                if (game.PlayersList[i].GameCharacter.Name == DoomGuy.CharacterName
                    && !game.PlayersList[i].Passives.PassiveAbilitiesDisabledByKimiko
                    && game.PlayersList[i].Passives.DoomGuy.RollMode
                    && DoomGuy.ApplyRandomModule(game.PlayersList[i], game, _rand))
                {
                    game.PlayersList[i].Status.LvlUpPoints--;
                    game.PlayersList[i].Status.MoveListPage = 1;
                }
            }
            game.PlayersList[i].Status.SetPlaceAtLeaderBoard(i + 1);
            game.PlayersList[i].GameCharacter.RollSkillTargetForNextRound();
            game.PlayersList[i].Status.PlaceAtLeaderBoardHistory.Add(new InGameStatus.PlaceAtLeaderBoardHistoryClass(game.RoundNo, game.PlayersList[i].Status.GetPlaceAtLeaderBoard()));
        }
        //end sorting

        // Restore ziggurat-locked positions
        foreach (var kvp in zigguratPositionLocks)
        {
            var zigPlayer = game.PlayersList.Find(x => x.GetPlayerId() == kvp.Key);
            if (zigPlayer == null) continue;
            var currentIdx = game.PlayersList.IndexOf(zigPlayer);
            var savedIdx = kvp.Value;
            if (currentIdx != savedIdx && savedIdx < game.PlayersList.Count)
            {
                if (UnknownBug.Is(game.PlayersList[savedIdx]))
                    continue;

                game.PlayersList[currentIdx] = game.PlayersList[savedIdx];
                game.PlayersList[savedIdx] = zigPlayer;
                // Re-assign places
                for (var i = 0; i < game.PlayersList.Count; i++)
                    game.PlayersList[i].Status.SetPlaceAtLeaderBoard(i + 1);
            }
        }

        // Restore Storm bite-locked positions
        foreach (var kvp in stormBiteLocks)
        {
            var biteLockedPlayer = game.PlayersList.Find(x => x.GetPlayerId() == kvp.Key);
            if (biteLockedPlayer == null) continue;
            var currentBiteIdx = game.PlayersList.IndexOf(biteLockedPlayer);
            var savedBiteIdx = kvp.Value;
            if (currentBiteIdx != savedBiteIdx && savedBiteIdx < game.PlayersList.Count)
            {
                // Check Ziggurat immunity on the player in target position
                if (game.PlayersList[savedBiteIdx].Passives.GoblinZiggurat.IsInZiggurat
                    || UnknownBug.Is(biteLockedPlayer)
                    || UnknownBug.Is(game.PlayersList[savedBiteIdx]))
                    continue;

                game.PlayersList[currentBiteIdx] = game.PlayersList[savedBiteIdx];
                game.PlayersList[savedBiteIdx] = biteLockedPlayer;
                for (var i = 0; i < game.PlayersList.Count; i++)
                    game.PlayersList[i].Status.SetPlaceAtLeaderBoard(i + 1);
            }
        }

        // Storm bite swap: on first round of bite, swap target with player above
        {
            var kotikiOwnerBiteSwap = game.PlayersList.Find(x => x.GameCharacter.Name == "Котики");
            if (kotikiOwnerBiteSwap != null)
            {
                var rbBiteSwap = kotikiOwnerBiteSwap.Passives.KotikiRandomBehavior;
                if (rbBiteSwap.SelectedTrickThisRound == 2 && rbBiteSwap.BiteTargetId != Guid.Empty)
                {
                    var biteTarget = game.PlayersList.Find(x => x.GetPlayerId() == rbBiteSwap.BiteTargetId);
                    if (biteTarget != null)
                    {
                        var biteIdx = game.PlayersList.IndexOf(biteTarget);
                        if (biteIdx > 0) // can swap with player above
                        {
                            var aboveIdx = biteIdx - 1;
                            // Check Ziggurat immunity on above player
                            if (!game.PlayersList[aboveIdx].Passives.GoblinZiggurat.IsInZiggurat
                                && !UnknownBug.Is(biteTarget)
                                && !UnknownBug.Is(game.PlayersList[aboveIdx]))
                            {
                                game.PlayersList[biteIdx] = game.PlayersList[aboveIdx];
                                game.PlayersList[aboveIdx] = biteTarget;
                                for (var i = 0; i < game.PlayersList.Count; i++)
                                    game.PlayersList[i].Status.SetPlaceAtLeaderBoard(i + 1);
                            }
                        }
                        rbBiteSwap.BiteLockPosition = biteTarget.Status.GetPlaceAtLeaderBoard();
                    }
                }
            }
        }

        //Quality Drop
        var droppedPlayers = game.PlayersList.Where(x => x.GameCharacter.GetStrengthQualityDropTimes() != 0 && x.Status.GetPlaceAtLeaderBoard() != 6).OrderByDescending(x => x.Status.GetPlaceAtLeaderBoard()).ToList();
        
        foreach (var player in droppedPlayers)
        {
            // Skip drop for players in ziggurat
            if (player.Passives.GoblinZiggurat.IsInZiggurat) continue;

            for (var i = 0; i < player.GameCharacter.GetStrengthQualityDropTimes(); i++)
            {
                var oldIndex = game.PlayersList.IndexOf(player);
                var newIndex = oldIndex + 1;

                if (newIndex == 5 && game.PlayersList[newIndex].GameCharacter.Passive.Any(x => x.PassiveName == "Никому не нужен"))
                    continue;
                // Can't drop onto a player in ziggurat
                if (newIndex < 6 && game.PlayersList[newIndex].Passives.GoblinZiggurat.IsInZiggurat)
                    continue;
                if(newIndex >= 6)
                    continue;
                    
                game.PlayersList[oldIndex] = game.PlayersList[newIndex];
                game.PlayersList[newIndex] = player;
            }
        }

        if (droppedPlayers.Count > 0)
        {
            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                game.PlayersList[i].Status.SetPlaceAtLeaderBoard(i + 1);
            }
        }
        //end //Quality Drop

        // A successful Shen dash keeps its selected cell through the next full action round.
        // Apply after ordinary movers so position-based next-round passives see the held cell.
        Salldorum.ApplyShenPositionHolds(game);

        // Round-10 "Ziggurat at place 1 ⇒ win" is enforced authoritatively in HandleLastRound (finding M1) —
        // it can't fire here because the round-10 ziggurat isn't built until HandleNextRoundAfterSorting below.

        SortGameLogs(game);
        _characterPassives.HandleNextRoundAfterSorting(game);
        Naruto.MoveDispersedClonesToBottom(game.PlayersList);
        if (game.RoundNo == 10)
        {
            var roundTenLast = game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == 6);
            if (roundTenLast != null)
                roundTenLast.Passives.AchievementTracker.OpenedRoundTenAtLast = true;
        }
        _characterPassives.HandleBotPredict(game);
        game.RollExploit();
        ReplayService.FinalizeRound(replayRound, game, _gameUpdateMess);
        game.TimePassed.Reset();
        game.TimePassed.Start();

        if(game.GameMode is "Normal" or "Aram")
            _logs.Info($"Finished calculating game #{game.GameId} (round# {game.RoundNo - 1}). || {watch.Elapsed.TotalSeconds}s");

        watch.Stop();
    }



    public void SortGameLogs(GameClass game)
    {
        var sortedGameLogs = "";
        var extraGameLogs = "\n";
        var logsSplit = game.GetGlobalLogs().Split("\n").ToList();
        logsSplit.RemoveAll(x => x.Length <= 2);
        sortedGameLogs += $"{logsSplit.First()}\n";
        logsSplit.RemoveAt(0);

        for (var i = 0; i < logsSplit.Count; i++)
        {
            if (logsSplit[i].Contains(":war:")) continue;
            extraGameLogs += $"{logsSplit[i]}\n";
            logsSplit.RemoveAt(i);
            i--;
        }

        sortedGameLogs = logsSplit.Aggregate(sortedGameLogs, (current, log) => $"{current}{log}\n");
        /*
        foreach (var player in game.PlayersList)
            for (var i = 0; i < logsSplit.Count; i++)
                if (logsSplit[i].Contains($"{player.DiscordUsername}"))
                {
                    var fightLine = logsSplit[i];

                    var fightLineSplit = fightLine.Split("⟶");

                    var fightLineSplitSplit = fightLineSplit.First().Split("<:war:561287719838547981>");

                    fightLine = fightLineSplitSplit.First().Contains($"{player.DiscordUsername}")
                        ? $"{fightLineSplitSplit.First()} <:war:561287719838547981> {fightLineSplitSplit[1]}"
                        : $"{fightLineSplitSplit[1]} <:war:561287719838547981> {fightLineSplitSplit.First()}";


                    fightLine += $" ⟶ {fightLineSplit[1]}";

                    sortedGameLogs += $"{fightLine}\n";
                    logsSplit.RemoveAt(i);
                    i--;
                }
        */
        sortedGameLogs += extraGameLogs;
        game.SetGlobalLogs(sortedGameLogs);
    }
}
