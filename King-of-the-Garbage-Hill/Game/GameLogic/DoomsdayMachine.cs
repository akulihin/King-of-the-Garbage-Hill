using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.API.Services;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class DoomsdayMachine : IServiceSingleton
{
    private readonly CharacterPassives _characterPassives;
    private readonly LoginFromConsole _logs;
    private readonly CalculateRounds _calculateRounds;

    public DoomsdayMachine(CharacterPassives characterPassives, LoginFromConsole logs, CalculateRounds calculateRounds)
    {
        _characterPassives = characterPassives;
        _logs = logs;
        _calculateRounds = calculateRounds;
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    /*
Intelligence => Speed
Strength => Intelligence
Speed => Strength
*/
    public string GetLostContrText(GamePlayerBridgeClass me, GamePlayerBridgeClass target)
    {
        if (me.GameCharacter.GetSkillClass() == "Интеллект" && target.GameCharacter.GetSkillClass() == "Скорость")
        {
            target.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(me.GetPlayerId(), "(**Умный** ?) "));

            return "вас обманул";
        }

        if (me.GameCharacter.GetSkillClass() == "Сила" && target.GameCharacter.GetSkillClass() == "Интеллект")
        {
            target.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(me.GetPlayerId(), "(**Сильный** ?) "));
            return "вас пресанул";
        }

        if (me.GameCharacter.GetSkillClass() == "Скорость" && target.GameCharacter.GetSkillClass() == "Сила")
        {
            target.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(me.GetPlayerId(), "(**Быстрый** ?) "));
            return "вас обогнал";
        }

        return "буль?";
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

            if (player.Status.IsLostThisCalculation != Guid.Empty && player.Passives.IsExploitable)
            {
               game.TotalExploit++;
            }


            //OneFight Mechanics, use GameCharacter only
            if (player.Status.IsIntelligenceForOneFight)
            {
                player.Status.IsIntelligenceForOneFight = false;
                player.GameCharacter.ResetIntelligenceForOneFight();
            }

            if (player.Status.IsStrengthForOneFight)
            {
                player.Status.IsStrengthForOneFight = false;
                player.GameCharacter.ResetStrengthForOneFight();
            }

            if (player.Status.IsSpeedForOneFight)
            {
                player.Status.IsSpeedForOneFight = false;
                player.GameCharacter.ResetSpeedForOneFight();
            }

            if (player.Status.IsPsycheForOneFight)
            {
                player.Status.IsPsycheForOneFight = false;
                player.GameCharacter.ResetPsycheForOneFight();
            }

            if (player.Status.IsJusticeForOneFight )
            {
                player.Status.IsJusticeForOneFight = false;
                player.GameCharacter.Justice.ResetJusticeForOneFight();
            }

            if (player.Status.IsSkillForOneFight)
            {
                player.Status.IsSkillForOneFight = false;
                player.GameCharacter.ResetSkillForOneFight();
            }
            //end OneFight Mechanics

            player.Status.IsWonThisCalculation = Guid.Empty;
            player.Status.IsLostThisCalculation = Guid.Empty;
            player.Status.IsFighting = Guid.Empty;
            player.Status.IsTargetSkipped = Guid.Empty;
            player.Status.IsTargetBlocked = Guid.Empty;
            player.Status.IsAbleToWin = true;
        }
    }

    public void DeepCopyFightCharactersToGameCharacter(GameClass game)
    {
        foreach (var player in game.PlayersList)
        {
            player.GameCharacter = player.FightCharacter.DeepCopy();
            player.Status.GameCharacter = player.GameCharacter;
        }
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
        foreach (var player in game.PlayersList)
        {
            foreach (var passive in player.GameCharacter.Passive)
            {
                switch (passive.PassiveName)
                {
                        case "PointFunnel":
                            if (player.Status.WhoToAttackThisTurn.Count > 0)
                            {
                                foreach (var targetId in player.Status.WhoToAttackThisTurn)
                                {
                                    var target = game.PlayersList.Find(x => x.GetPlayerId() == targetId);
                                    target.Passives.PointFunneledTo = player.GetPlayerId();
                                }
                            }
                            break;
            } 
            }
        }

    }




    //пристрій судного дня
    public async Task CalculateAllFights(GameClass game)    
    {
        var watch = new Stopwatch();
        watch.Start();

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
                entry.RoundsPlayed++;
                if (entry.RoundsPlayed >= entry.RoundsToPlay)
                    p.WebMediaMessages.RemoveAt(mi);
            }
        }

        // Clear previous round's fight log
        game.WebFightLog.Clear();

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
            p.Status.AddBonusPoints(p.GameCharacter.GetBonusPointsFromMoral(), "Мораль");
            p.GameCharacter.SetBonusPointsFromMoral(0);
        }
        //end Moral

        HandleEventsBeforeCalculation(game);

        /*
        1-4 х1
        5-9 х2
        10  х4
         */

        game.SetGlobalLogs($"\n__**Раунд #{roundNumber}**__:\n\n");

        //FightCharacter == READ ONLY
        //GameCharacter == WRITE ONLY
        //FightCharacter writes cans happens only "for one fight" not for the whole round!
        DeepCopyGameCharacterToFightCharacter(game);

        











        foreach (var player in game.PlayersList)
        {

            player.Status.AddFightingData($"\n\n**Logs for round #{game.RoundNo}:**");
            //if block => no one gets points, and no redundant playerAttacked variable
            if (player.Status.IsBlock || player.Status.IsSkip)
            {
                player.Status.AddFightingData($"IsBlock: {player.Status.IsBlock}");
                player.Status.AddFightingData($"IsSkip: {player.Status.IsSkip}");
                //fight Reset
                await _characterPassives.HandleCharacterAfterFight(player, game, true, false);
                ResetFight(game, player);
                continue;
            }

            foreach (var playerIamAttacking in player.Status.WhoToAttackThisTurn.Select(t => game.PlayersList.Find(x => x.GetPlayerId() == t)))
            {
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


                _characterPassives.HandleDefenseBeforeFight(playerIamAttacking, player, game);
                _characterPassives.HandleAttackBeforeFight(player, playerIamAttacking, game);


                //умный
                if (player.GameCharacter.GetSkillClass() == "Интеллект" && playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow() == 0)
                {
                    skillGainedFromClassAttacker = player.GameCharacter.AddExtraSkill(6 * player.GameCharacter.GetClassSkillMultiplier(), "Класс");
                }




                game.AddGlobalLogs($"{player.DiscordUsername} <:war:561287719838547981> {playerIamAttacking.DiscordUsername}", "");

                if (player.GameCharacter.GetCurrentSkillClassTarget() == playerIamAttacking.GameCharacter.GetSkillClass())
                {
                    string text1;
                    string text2;

                    if (playerIamAttacking.GameCharacter.GetSkillClass() == "Интеллект")
                    {
                        text1 = "**умного**";
                        text2 = "(**Умный** ?) ";
                    }
                    else if (playerIamAttacking.GameCharacter.GetSkillClass() == "Сила")
                    {
                        text1 = "**сильного**";
                        text2 = "(**Сильный** ?) ";
                    }
                    else if (playerIamAttacking.GameCharacter.GetSkillClass() == "Скорость")
                    {
                        text1 = "**быстрого**";
                        text2 = "(**Быстрый** ?) ";
                    }
                    else
                    {
                        text1 = "**буля**";
                        text2 = "(**БУЛЬ** ?!) ";
                    }

                    var skillBefore = player.GameCharacter.GetSkill();
                    skillGainedFromTarget = player.GameCharacter.AddMainSkill(text1);

                    var known = player.Status.KnownPlayerClass.Find(x => x.EnemyId == playerIamAttacking.GetPlayerId());
                    if (known != null)
                        player.Status.KnownPlayerClass.Remove(known);
                    player.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(playerIamAttacking.GetPlayerId(), text2));
                }


                //check skill text
                switch (player.GameCharacter.GetCurrentSkillClassTarget())
                {
                    case "Интеллект":
                        if (playerIamAttacking.GameCharacter.GetSkillClass() != "Интеллект")
                        {
                            var knownEnemy =
                                player.Status.KnownPlayerClass.Find(
                                    x => x.EnemyId == playerIamAttacking.GetPlayerId());
                            if (knownEnemy != null)
                                if (knownEnemy.Text.Contains("Умный"))
                                    player.Status.KnownPlayerClass.Remove(knownEnemy);
                        }

                        break;
                    case "Сила":
                        if (playerIamAttacking.GameCharacter.GetSkillClass() != "Сила")
                        {
                            var knownEnemy =
                                player.Status.KnownPlayerClass.Find(
                                    x => x.EnemyId == playerIamAttacking.GetPlayerId());
                            if (knownEnemy != null)
                                if (knownEnemy.Text.Contains("Сильный"))
                                    player.Status.KnownPlayerClass.Remove(knownEnemy);
                        }

                        break;
                    case "Скорость":
                        if (playerIamAttacking.GameCharacter.GetSkillClass() != "Скорость")
                        {
                            var knownEnemy =
                                player.Status.KnownPlayerClass.Find(
                                    x => x.EnemyId == playerIamAttacking.GetPlayerId());
                            if (knownEnemy != null)
                                if (knownEnemy.Text.Contains("Быстрый"))
                                    player.Status.KnownPlayerClass.Remove(knownEnemy);
                        }

                        break;
                    case "Буль":
                        if (playerIamAttacking.GameCharacter.GetSkillClass() != "Буль")
                        {
                            var knownEnemy =
                                player.Status.KnownPlayerClass.Find(
                                    x => x.EnemyId == playerIamAttacking.GetPlayerId());
                            if (knownEnemy != null)
                                if (knownEnemy.Text.Contains("БУЛЬ"))
                                    player.Status.KnownPlayerClass.Remove(knownEnemy);
                        }

                        break;
                }


                player.Status.AddFightingData($"IsArmorBreak: {player.Status.IsArmorBreak}");
                player.Status.AddFightingData($"IsBlockEnemy: {playerIamAttacking.Status.IsBlock}");
                playerIamAttacking.Status.AddFightingData($"IsBlock: {playerIamAttacking.Status.IsBlock}");
                playerIamAttacking.Status.AddFightingData($"IsArmorBreakEnemy: {player.Status.IsArmorBreak}");

                //if block => no one gets points
                if (playerIamAttacking.Status.IsBlock && !player.Status.IsArmorBreak)
                {
                    player.Status.IsTargetBlocked = playerIamAttacking.GetPlayerId();
                    // var logMess =  await _characterPassives.HandleBlock(player, playerIamAttacking, game);

                    var logMess = " ⟶ *Бой не состоялся...*";
                    if (game.PlayersList.Any(x => x.PlayerType == 1))
                        logMess = " ⟶ *Бой не состоялся (Блок)...*";
                    game.AddGlobalLogs(logMess);


                    player.Status.AddBonusPoints(-1, "Блок");

                    playerIamAttacking.GameCharacter.Justice.AddJusticeForNextRoundFromFight();

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
                if (playerIamAttacking.Status.IsSkip && !player.Status.IsSkipBreak)
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

                    //fight Reset
                    await _characterPassives.HandleCharacterAfterFight(player, game, true, false);
                    await _characterPassives.HandleCharacterAfterFight(playerIamAttacking, game, false, true);
                    _characterPassives.HandleDefenseAfterBlockOrFightOrSkip(playerIamAttacking, player, game);

                    ResetFight(game, player, playerIamAttacking);

                    continue;
                }

                //round 1 (contr)


                //быстрый
                if (playerIamAttacking.GameCharacter.GetSkillClass() == "Скорость")
                    skillGainedFromClassDefender = playerIamAttacking.GameCharacter.AddExtraSkill(2 * playerIamAttacking.GameCharacter.GetClassSkillMultiplier(), "Класс");

                if (player.GameCharacter.GetSkillClass() == "Скорость")
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
                var isContrLost = step1.IsContrLost;
                var randomForPoint = step1.RandomForPoint;
                var weighingMachine = step1.WeighingMachine;
                var contrMultiplier = step1.ContrMultiplier;
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

                
                //round 2 (Justice)
                var justiceMe = player.GameCharacter.Justice.GetRealJusticeNow();
                var justiceTarget = playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow();
                var step2Points = _calculateRounds.CalculateStep2(player, playerIamAttacking, true);
                pointsWined += step2Points;
                //end round 2


                //round 3 (Random)
                var usedRandomRoll = false;
                var step3RandomNumber = 0;
                var step3MaxRandom = 0m;
                decimal justiceRandomChange = 0;
                decimal contrRandomChange = 0;
                if (pointsWined == 0)
                {
                    var (step3Points, rndNum, rndMax, justiceRandomChangeL, contrRandomChangeL) = _calculateRounds.CalculateStep3(player, playerIamAttacking, randomForPoint, contrMultiplier, true);
                    pointsWined += step3Points;
                    usedRandomRoll = true;
                    step3RandomNumber = rndNum;
                    step3MaxRandom = rndMax;
                    justiceRandomChange = justiceRandomChangeL;
                    contrRandomChange = contrRandomChangeL;
                }
                //end round 3


                var moral = player.Status.GetPlaceAtLeaderBoard() - playerIamAttacking.Status.GetPlaceAtLeaderBoard();


                //octopus  // playerIamAttacking is octopus
                if (pointsWined <= 0) 
                    pointsWined = await _characterPassives.HandleOctopus(playerIamAttacking, player, game);
                //end octopus


                //team
                var teamMate = false;
                if (game.Teams.Count > 0)
                {
                    var playerTeam = game.Teams.Find(x => x.TeamPlayers.Contains(player.Status.PlayerId)).TeamId;
                    var playerIamAttackingTeam = game.Teams.Find(x => x.TeamPlayers.Contains(playerIamAttacking.Status.PlayerId)).TeamId;
                    if (playerTeam == playerIamAttackingTeam)
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
                    var point = 1;
                    //сильный
                    if (player.GameCharacter.GetSkillClass() == "Сила")
                        skillGainedFromClassAttacker = player.GameCharacter.AddExtraSkill(4 * player.GameCharacter.GetClassSkillMultiplier(), "Класс");

                    isContrLost -= 1;
                    game.AddGlobalLogs($" ⟶ {player.DiscordUsername}");

                    //еврей
                    if (!teamMate)
                        point = await _characterPassives.HandleJews(player, playerIamAttacking, game);
                    if (point == 0) player.Status.AddInGamePersonalLogs("Евреи...\n");
                    //end еврей


                    //add regular points
                    if (!teamMate)
                        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Никому не нужен"))
                        {
                            player.Status.AddWinPoints(game, player, point * -1, "Победа");
                        }
                        else
                        {
                            player.Status.AddWinPoints(game, player, point, "Победа");
                        }


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
                            playerIamAttacking.GameCharacter.AddMoral(moral * -1, "Поражение", isFightMoral: true);
                            attackerMoralActual = player.GameCharacter.GetMoral() - atkMoralBefore;
                            defenderMoralActual = playerIamAttacking.GameCharacter.GetMoral() - defMoralBefore;

                            player.Status.AddFightingData($"moral: {moral} ({player.Status.GetPlaceAtLeaderBoard()} - {playerIamAttacking.Status.GetPlaceAtLeaderBoard()})");
                            playerIamAttacking.Status.AddFightingData($"moral: {moral * -1} ({player.Status.GetPlaceAtLeaderBoard()} - {playerIamAttacking.Status.GetPlaceAtLeaderBoard()})");
                        }
                    }

                    if (!teamMate)
                        playerIamAttacking.GameCharacter.Justice.AddJusticeForNextRoundFromFight();

                    player.Status.IsWonThisCalculation = playerIamAttacking.GetPlayerId();
                    playerIamAttacking.Status.IsLostThisCalculation = player.GetPlayerId();
                    playerIamAttacking.Status.WhoToLostEveryRound.Add(new InGameStatus.WhoToLostPreviousRoundClass(player.GetPlayerId(), game.RoundNo, isTooGoodMe, isStatsBetterMe, isTooGoodEnemy, isStatsBettterEnemy, player.GetPlayerId()));

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

                    if (placeDiff <= range)
                    {
                        playerIamAttacking.GameCharacter.LowerQualityResist(playerIamAttacking, game, player);
                        qualityDamageApplied = true;
                    }

                    resistIntelAfter = playerIamAttacking.GameCharacter.GetIntelligenceQualityResistInt();
                    resistStrAfter = playerIamAttacking.GameCharacter.GetStrengthQualityResistInt();
                    resistPsycheAfter = playerIamAttacking.GameCharacter.GetPsycheQualityResistInt();
                    dropsAfter = playerIamAttacking.GameCharacter.GetStrengthQualityDropTimes();
                    // Detect if intel/psyche resist broke (went below 0 and was reset)
                    intellectualDamage = qualityDamageApplied && resistIntelAfter > resistIntelBefore;
                    emotionalDamage = qualityDamageApplied && resistPsycheAfter > resistPsycheBefore;
                    // Justice: loser (defender) gains +1 justice
                    if (!teamMate) fightJusticeChange = 1;

                    //end Quality
                }
                else
                {
                    //сильный
                    if (playerIamAttacking.GameCharacter.GetSkillClass() == "Сила")
                        skillGainedFromClassDefender = playerIamAttacking.GameCharacter.AddExtraSkill(4 * playerIamAttacking.GameCharacter.GetClassSkillMultiplier(), "Класс");

                    if (isTooGoodEnemy && !isTooStronkEnemy)
                        player.Status.AddInGamePersonalLogs($"{playerIamAttacking.DiscordUsername} is __TOO GOOD__ for you\n");
                    if (isTooStronkEnemy)
                        player.Status.AddInGamePersonalLogs($"{playerIamAttacking.DiscordUsername} is __TOO STONK__ for you\n");

                    isContrLost += 1;


                    game.AddGlobalLogs($" ⟶ {playerIamAttacking.DiscordUsername}");

                    if (!teamMate)
                        playerIamAttacking.Status.AddWinPoints(game, playerIamAttacking, 1, "Победа");



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
                    player.Status.WhoToLostEveryRound.Add(new InGameStatus.WhoToLostPreviousRoundClass(playerIamAttacking.GetPlayerId(), game.RoundNo, isTooGoodEnemy, isStatsBettterEnemy, isTooGoodMe, isStatsBetterMe, player.GetPlayerId()));
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
                        AttackerClass = me.GetSkillClass(),
                        DefenderClass = target.GetSkillClass(),
                        WhoIsBetterIntel = step1.WhoIsBetterIntel,
                        WhoIsBetterStr = step1.WhoIsBetterStr,
                        WhoIsBetterSpeed = step1.WhoIsBetterSpeed,
                        ScaleMe = Math.Round(step1.ScaleMe, 2),
                        ScaleTarget = Math.Round(step1.ScaleTarget, 2),
                        IsContrMe = me.GetWhoIContre() == target.GetSkillClass(),
                        IsContrTarget = target.GetWhoIContre() == me.GetSkillClass(),
                        ContrMultiplier = contrMultiplier,
                        SkillMultiplierMe = (int)step1.SkillMultiplierMe,
                        SkillMultiplierTarget = (int)step1.SkillMultiplierTarget,
                        PsycheDifference = me.GetPsyche() - target.GetPsyche(),
                        WeighingMachine = Math.Round(weighingMachine, 2),
                        IsTooGoodMe = isTooGoodMe,
                        IsTooGoodEnemy = isTooGoodEnemy,
                        IsTooStronkMe = isTooStronkMe,
                        IsTooStronkEnemy = isTooStronkEnemy,
                        IsStatsBetterMe = isStatsBetterMe,
                        IsStatsBetterEnemy = isStatsBettterEnemy,
                        RandomForPoint = Math.Round(randomForPoint, 2),
                        // Round 1 per-step deltas
                        ContrWeighingDelta = Math.Round(step1.ContrWeighingDelta, 2),
                        ScaleWeighingDelta = Math.Round(step1.ScaleWeighingDelta, 2),
                        WhoIsBetterWeighingDelta = Math.Round(step1.WhoIsBetterWeighingDelta, 2),
                        PsycheWeighingDelta = Math.Round(step1.PsycheWeighingDelta, 2),
                        SkillWeighingDelta = Math.Round(step1.SkillWeighingDelta, 2),
                        JusticeWeighingDelta = Math.Round(step1.JusticeWeighingDelta, 2),
                        // Round 3 random modifiers
                        TooGoodRandomChange = Math.Round(step1.TooGoodRandomChange, 2),
                        TooStronkRandomChange = Math.Round(step1.TooStronkRandomChange, 2),
                        JusticeRandomChange = Math.Round(justiceRandomChange, 2),
                        ContrRandomChange = Math.Round(contrRandomChange, 2),
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
                        ContrMultiplierSkillDifference = Math.Round(step1.ContrMultiplierSkillDifference, 2),
                    });
                }

                switch (isContrLost)
                {
                    case 3:
                        player.Status.AddInGamePersonalLogs($"Поражение: {playerIamAttacking.DiscordUsername} {GetLostContrText(playerIamAttacking, player)}\n");
                        break;
                    case -3:
                        playerIamAttacking.Status.AddInGamePersonalLogs($"Поражение: {player.DiscordUsername} {GetLostContrText(player, playerIamAttacking)}\n");
                        break;
                }

                //т.е. он получил урон, какие у него дебаффы на этот счет 
                _characterPassives.HandleDefenseAfterFight(playerIamAttacking, player, game);
                _characterPassives.HandleDefenseAfterBlockOrFight(playerIamAttacking, player, game);
                _characterPassives.HandleDefenseAfterBlockOrFightOrSkip(playerIamAttacking, player, game);

                //т.е. я его аттакую, какие у меня бонусы на это
                _characterPassives.HandleAttackAfterFight(player, playerIamAttacking, game);

                //fight Reset
                await _characterPassives.HandleCharacterAfterFight(player, game, true, false);
                await _characterPassives.HandleCharacterAfterFight(playerIamAttacking, game, false, true);
                
                _characterPassives.HandleShark(game); //used only for shark...

                ResetFight(game, player, playerIamAttacking);
            }
        }














        //AFTER Fight, use only GameCharacter.
        //DeepCopyFightCharactersToGameCharacter(game);

        await _characterPassives.HandleEndOfRound(game);

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
            player.Status.WhoToAttackThisTurn = new List<Guid>();
            player.Status.MoveListPage = 1;
            player.Status.IsAbleToChangeMind = true;
            player.Status.RoundNumber = game.RoundNo+1;

            player.GameCharacter.SetSpeedResist();
            player.GameCharacter.NormalizeMoral();
            var justicePhrase = player.GameCharacter.Justice.HandleEndOfRoundJustice();
            if (justicePhrase)
            {
                game.Phrases.JusticePhrase.SendLogSeparateWeb(player, delete:false, isEvent:false);
            }

            player.Status.CombineRoundScoreAndGameScore(game);
            player.Status.ClearInGamePersonalLogs();
            player.Status.InGamePersonalLogsAll += "|||";

            player.Passives.PointFunneledTo = Guid.Empty;
        }

        //Возвращение из мертвых
        //game.PlayersList = game.PlayersList.Where(x => !x.Passives.KratosIsDead).ToList();

        if (game.PlayersList.Count(x => x.Passives.KratosIsDead && x.GameCharacter.Name != "Кратос") == 5)
        {
            game.IsKratosEvent = false;
            game.AddGlobalLogs("Все боги были убиты, открылась коробка Пандоры, стихийные бедствия уничтожили всё живое...");
            game.PlayersList[0].Status.AddInGamePersonalLogs("By the gods, what have I become?\n");
        }
        //end Возвращение из мертвых

        game.SkipPlayersThisRound = 0;
        game.RoundNo++;

        if (game.GameMode == "aram" && game.RoundNo == 2)
        {
            game.TurnLengthInSecond = 300;
        }


        await _characterPassives.HandleNextRound(game);



        game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();


        //Тигр топ, а ты холоп
        foreach (var player in game.PlayersList.Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Тигр топ, а ты холоп")).ToList())
        {
            var tigr = player.Passives.TigrTop;

            if (tigr is { TimeCount: > 0 })
            {
                var tigrIndex = game.PlayersList.IndexOf(player);

                game.PlayersList[tigrIndex] = game.PlayersList.First();
                game.PlayersList[0] = player;
                tigr.TimeCount--;
                // game.Phrases.TigrTop.SendLog(tigrTemp);
            }
        }
        //end Тигр топ, а ты холоп


        //Никому не нужен
        foreach (var player in game.PlayersList.Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Никому не нужен")).ToList())
        {
            var hardIndex = game.PlayersList.IndexOf(player);

            for (var k = hardIndex; k < game.PlayersList.Count - 1; k++)
                game.PlayersList[k] = game.PlayersList[k + 1];

            game.PlayersList[^1] = player;
        }
        //end Никому не нужен


        //sort
        for (var i = 0; i < game.PlayersList.Count; i++)
        {
            if (game.RoundNo is 3 or 5 or 7 or 9)
            {
                game.PlayersList[i].Status.LvlUpPoints++;
                game.PlayersList[i].Status.MoveListPage = 3;
            }
            game.PlayersList[i].Status.SetPlaceAtLeaderBoard(i + 1);
            game.PlayersList[i].GameCharacter.RollSkillTargetForNextRound();
            game.PlayersList[i].Status.PlaceAtLeaderBoardHistory.Add(new InGameStatus.PlaceAtLeaderBoardHistoryClass(game.RoundNo, game.PlayersList[i].Status.GetPlaceAtLeaderBoard()));
        }
        //end sorting

        //Quality Drop
        var droppedPlayers = game.PlayersList.Where(x => x.GameCharacter.GetStrengthQualityDropTimes() != 0 && x.Status.GetPlaceAtLeaderBoard() != 6).OrderByDescending(x => x.Status.GetPlaceAtLeaderBoard()).ToList();
        
        foreach (var player in droppedPlayers)
        {
            for (var i = 0; i < player.GameCharacter.GetStrengthQualityDropTimes(); i++)
            {
                var oldIndex = game.PlayersList.IndexOf(player);
                var newIndex = oldIndex + 1;

                if (newIndex == 5 && game.PlayersList[newIndex].GameCharacter.Passive.Any(x => x.PassiveName == "Никому не нужен"))
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

        SortGameLogs(game);
        _characterPassives.HandleNextRoundAfterSorting(game);
        _characterPassives.HandleBotPredict(game);
        game.RollExploit();
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