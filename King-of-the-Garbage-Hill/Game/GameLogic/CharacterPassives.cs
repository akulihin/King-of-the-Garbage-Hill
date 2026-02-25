using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class CharacterPassives : IServiceSingleton
{
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly HelperFunctions _help;
    private readonly LoginFromConsole _log;
    private readonly SecureRandom _rand;
    private readonly CharactersPull _charactersPull;

    public CharacterPassives(SecureRandom rand, HelperFunctions help,
        LoginFromConsole log, GameUpdateMess gameUpdateMess, CharactersPull charactersPull)
    {
        _rand = rand;
        _help = help;
        _log = log;
        _gameUpdateMess = gameUpdateMess;
        _charactersPull = charactersPull;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    public List<GamePlayerBridgeClass> HandleEventsBeforeFirstRound(List<GamePlayerBridgeClass> playersList)
    {
        foreach (var player in playersList.ToList())
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "God Of War":
                    player.Status.AddInGamePersonalLogs("**Zeus! Your son has returned. I bring the destruction of Olympus!**\n");
                    break;

                case "Похищение души":
                    player.GameCharacter.SetClassSkillMultiplier(2);
                    break;

                case "Искусство":
                    player.Status.AddInGamePersonalLogs(
                        "*Какая честь - умереть на поле боя... Начнем прямо сейчас!*\n");
                    break;

                case "Повторяет за myloran":
                    player.GameCharacter.AddIntelligence(player.GameCharacter.GetIntelligence() * -1 + 0, "Повторяет за myloran");
                    break;

                case "DeepList Pet":
                    if (playersList.Any(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Weedwick Pet")))
                    {
                        player.Status.AddInGamePersonalLogs("**Чья эта безуманя собака?**: +4 Психики\n");
                        player.GameCharacter.AddPsyche(4, "Чья эта безуманя собака?", false);
                    }

                    break;

                case "Weedwick Pet":
                    if (playersList.Any(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "DeepList Pet")))
                    {
                        player.Status.AddInGamePersonalLogs("**Чья эта безуманя собака?**: +4 Психики\n");
                        player.GameCharacter.AddPsyche(4, "Чья эта безуманя собака?", false);
                    }

                    player.Status.AddInGamePersonalLogs("Она всегда со мной, куда бы я не пошел...\n");
                    break;

                case "Первая кровь":
                    player.GameCharacter.SetAnySkillMultiplier(1);
                    break;

                case "Им это не понравится":
                    Guid enemy1;
                    Guid enemy2;

                    do
                    {
                        var randIndex = _rand.Random(0, playersList.Count - 1);
                        enemy1 = playersList[randIndex].GetPlayerId();
                        if (playersList[randIndex].GameCharacter.Name is "Злой Школьник" or "Глеб" or "mylorik"
                            or "Загадочный Спартанец в маске")
                            enemy1 = player.GetPlayerId();
                    } while (enemy1 == player.GetPlayerId());

                    // Most wanted: force Rick as enemy1
                    var rickMw1 = playersList.Find(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Most wanted"));
                    if (rickMw1 != null && rickMw1.GetPlayerId() != player.GetPlayerId())
                    {
                        enemy1 = rickMw1.GetPlayerId();
                        var mwPhrases = new[] {
                            "Да чего этим федералам надо от меня?!",
                            "Вся вселенная гоняется за рецептом моего особого топлива...",
                            "Боже! Может умнейший человек во вселенной просто спокойно провести время с внуком?!"
                        };
                        rickMw1.Status.AddInGamePersonalLogs($"|>Phrase<|Most wanted: {mwPhrases[_rand.Random(0, mwPhrases.Length - 1)]}\n");
                    }

                    do
                    {
                        var randIndex = _rand.Random(0, playersList.Count - 1);
                        enemy2 = playersList[randIndex].GetPlayerId();
                        if (playersList[randIndex].GameCharacter.Name is "Злой Школьник" or "Глеб" or "mylorik"
                            or "Загадочный Спартанец в маске")
                            enemy2 = player.GetPlayerId();
                        if (enemy2 == enemy1)
                            enemy2 = player.GetPlayerId();
                    } while (enemy2 == player.GetPlayerId());

                    player.Passives.SpartanMark.FriendList.Add(enemy1);
                    player.Passives.SpartanMark.FriendList.Add(enemy2);
                    break;

                case "Никому не нужен":
                    player.Status.HardKittyMinus(-30, "Никому не нужен");
                    player.Status.AddInGamePersonalLogs("Никому не нужен: -30 *Морали*\n");
                    var playerIndex = playersList.IndexOf(player);

                    for (var i = playerIndex; i < playersList.Count - 1; i++)
                        playersList[i] = playersList[i + 1];

                    playersList[^1] = player;
                    break;

                case "Тигр топ, а ты холоп":
                    var tigr = player.Passives.TigrTop;

                    if (tigr is { TimeCount: > 0 })
                    {
                        var tigrIndex = playersList.IndexOf(player);

                        playersList[tigrIndex] = playersList.First();
                        playersList[0] = player;
                        tigr.TimeCount--;
                        //game.Phrases.TigrTop.SendLog(tigrTemp);
                    }

                    break;

                case "Дерзкая школота":
                    player.GameCharacter.AddExtraSkill(100, "Дерзкая школота");
                    player.GameCharacter.SetIntelligence(9,"Дерзкая школота", false);
                    player.GameCharacter.SetStrength(9, "Дерзкая школота", false);
                    player.GameCharacter.SetSpeed(9, "Дерзкая школота", false);
                    player.GameCharacter.SetPsyche(9, "Дерзкая школота", false);
                    break;

                case "Main Ирелия":
                    player.GameCharacter.SetIntelligence(8, "Main Ирелия", false);
                    player.GameCharacter.SetStrength(8, "Main Ирелия", false);
                    player.GameCharacter.SetSpeed(8, "Main Ирелия", false);
                    player.GameCharacter.SetPsyche(8, "Main Ирелия", false);
                    break;

                    case "Много выебывается": 
                        //first place
                    playerIndex = playersList.IndexOf(player);
                    playersList[playerIndex] = playersList.First();
                    playersList[0] = player;

                    //x3 class for target
                    //player.GameCharacter.SetTargetSkillMultiplier(2);
                    break;

                case "Лысина":
                    player.GameCharacter.AddExtraSkill(1000, "Лысина");
                    player.Status.AddInGamePersonalLogs("*100 отжиманий. 100 приседаний. 100 подъёмов корпуса. 10 км бега. КАЖДЫЙ ДЕНЬ. Побочный эффект - потеря волос.*\n");
                    break;

                case "Гигантские бобы":
                    player.Passives.RickGiantBeans.BaseIntelligence = player.GameCharacter.GetIntelligence();
                    break;

                case "Гений":
                    player.GameCharacter.AddIntelligence(4, "Гений");
                    break;

                case "L":
                    // Pick random enemy as L (prefer human players)
                    var lCandidates = playersList
                        .Where(x => x.GetPlayerId() != player.GetPlayerId() && x.PlayerType != 404)
                        .ToList();
                    if (lCandidates.Count == 0)
                        lCandidates = playersList.Where(x => x.GetPlayerId() != player.GetPlayerId()).ToList();

                    var lTarget = lCandidates[_rand.Random(0, lCandidates.Count - 1)].GetPlayerId();

                    // Most wanted: force Rick as L target
                    var rickMwL = playersList.Find(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Most wanted"));
                    if (rickMwL != null && rickMwL.GetPlayerId() != player.GetPlayerId())
                        lTarget = rickMwL.GetPlayerId();

                    player.Passives.KiraL.LPlayerId = lTarget;
                    player.Status.AddInGamePersonalLogs($"Эй, Лайт, это Бог Смерти. Тебе выпал интересный противник: **{playersList.Find(x => x.GetPlayerId() == lTarget)!.DiscordUsername}** - это L.\n");
                    break;

                case "Неприметность":
                    // Compute top 2 enemies by combat power (Skill) to fight seriously against
                    var saitamaUnnoticed = player.Passives.SaitamaUnnoticed;
                    saitamaUnnoticed.SeriousTargets = playersList
                        .Where(x => x.GetPlayerId() != player.GetPlayerId())
                        .OrderByDescending(x => x.GameCharacter.GetSkill())
                        .Take(2)
                        .Select(x => x.GetPlayerId())
                        .ToList();
                    break;

                // Toxic Mate — "Fuck this game, I'm done.": start with -1000 moral
                case "Fuck this game, I'm done.":
                    player.GameCharacter.AddMoral(-1000, "Fuck this game, I'm done.");
                    break;

                // Toxic Mate — "FF 20": start with -20 bonus points
                case "FF 20":
                    player.Status.AddBonusPoints(-20, "FF 20");
                    break;

                // Toxic Mate — "INT": announce to all players
                case "INT":
                    foreach (var p in playersList)
                        p.Status.AddInGamePersonalLogs("**U are FoCKING retards!**\n");
                    break;

                case "Гоблины":
                    var gobPop = player.Passives.GoblinPopulation;
                    player.GameCharacter.SetStrength(gobPop.Warriors, "Гоблины");
                    player.GameCharacter.SetIntelligence(gobPop.Hobs, "Гоблины");
                    player.GameCharacter.SetPsyche(5 + gobPop.Hobs, "Гоблины");
                    player.Status.AddInGamePersonalLogs($"Стая Гоблинов: {gobPop.TotalGoblins} гоблинов (⚔️{gobPop.Warriors} 🧙{gobPop.Hobs} ⛏️{gobPop.Workers})\n");
                    break;

                // TheBoys — Заказ Француза: shuffle opponents and assign first order
                case "Заказ Француза":
                    var francie = player.Passives.TheBoysFrancie;
                    var opponents = playersList
                        .Where(x => x.GetPlayerId() != player.GetPlayerId())
                        .Select(x => x.GetPlayerId())
                        .OrderBy(_ => Guid.NewGuid())
                        .ToList();
                    francie.RemainingTargets = opponents;
                    if (francie.RemainingTargets.Count > 0)
                    {
                        francie.OrderTarget = francie.RemainingTargets[0];
                        francie.RemainingTargets.RemoveAt(0);
                        francie.OrderHistory.Add(francie.OrderTarget);
                        francie.OrderRoundsLeft = 2;
                        var targetName = playersList.Find(x => x.GetPlayerId() == francie.OrderTarget)?.DiscordUsername ?? "???";
                        player.Status.AddInGamePersonalLogs($"Заказ Француза: Цель — {targetName}. 2 хода на выполнение.\n");
                    }
                    break;

                // Salldorum — initialize chronicler position history
                case "Великий летописец":
                    if (player.GameCharacter.Name == "Salldorum")
                    {
                        player.Passives.SalldorumChronicler.PositionHistory = new List<int>();
                    }
                    break;

                // Геральт — assign special first contracts for matching characters
                case "Ведьмачий Заказ":
                    if (player.GameCharacter.Name == "Геральт")
                    {
                        var geraltContracts = player.Passives.GeraltContracts;
                        var specialContracts = new Dictionary<string, string>
                        {
                            { "Sirinoks", "Дракон" },
                            { "Weedwick", "Лютоволк" },
                            { "Вампур", "Вампур" },
                            { "Стая Гоблинов", "Бес" }
                        };
                        foreach (var enemy in playersList.Where(x => x.GetPlayerId() != player.GetPlayerId()))
                        {
                            if (specialContracts.TryGetValue(enemy.GameCharacter.Name, out var monsterType))
                            {
                                var enemyId = enemy.GetPlayerId();
                                if (!geraltContracts.ContractMap.ContainsKey(enemyId))
                                    geraltContracts.ContractMap[enemyId] = new List<string>();
                                geraltContracts.ContractMap[enemyId].Add(monsterType);
                                enemy.Passives.GeraltContractsOnMe.Add(monsterType);
                                enemy.Passives.GeraltContractOwnerId = player.GetPlayerId();
                            }
                        }
                    }
                    break;
            }

        return playersList;
    }


    //handle during fight
    public void HandleDefenseBeforeFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Следит за игрой":
                    foreach (var metaPlayer in target.Passives.YongGlebMetaClass)
                    {
                        if (target.GetPlayerId() == metaPlayer && target.Status.IsBlock)
                        {
                            target.Status.AddBonusPoints(1, "Следит за игрой");
                            game.Phrases.YongGlebMeta.SendLog(target, true);
                        }
                    }
                    break;

                case "Оборотень":
                    /*
                    var myTempStrength = me.GameCharacter.GetStrength();
                    var targetTempStrength = target.GameCharacter.GetStrength();
                    me.GameCharacter.SetStrengthForOneFight(targetTempStrength, "Оборотень");
                    target.GameCharacter.SetStrengthForOneFight(myTempStrength, "Оборотень");*/

                    /*var myTempSkillMain = me.GameCharacter.GetSkillForOneFight();
                    var targetTempSkill = target.GameCharacter.GetSkillForOneFight();
                    me.GameCharacter.SetSkillForOneFight(targetTempSkill, "Оборотень");
                    target.GameCharacter.SetSkillForOneFight(myTempSkillMain, "Оборотень");*/
                    break;

                case "Сомнительная тактика":
                    var deep = target.Passives.DeepListDoubtfulTactic;

                    if (!deep.FriendList.Contains(me.GetPlayerId()))
                        target.Status.IsAbleToWin = false;
                    break;

                case "Неуязвимость":
                    me.FightCharacter.SetStrengthForOneFight(0, "Неуязвимость");
                    break;

                case "Панцирь":
                    var сraboRackShell = target.Passives.CraboRackShell;
                    if (сraboRackShell != null)
                        if (!сraboRackShell.FriendList.Contains(me.GetPlayerId()))
                        {
                            сraboRackShell.FriendList.Add(me.GetPlayerId());
                            сraboRackShell.CurrentAttacker = me.GetPlayerId();
                            target.GameCharacter.AddMoral(3, "Панцирь");
                            target.GameCharacter.AddExtraSkill(33, "Панцирь");
                            target.Status.IsBlock = true;
                        }

                    break;

                case "Хождение боком":
                    me.FightCharacter.SetSpeedForOneFight(0, "Хождение боком");
                    break;

                case "Ничего не понимает":

                    var shark = target.Passives.SharkBoole;

                    if (!shark.FriendList.Contains(me.GetPlayerId()))
                    {
                        shark.FriendList.Add(me.GetPlayerId());
                        me.GameCharacter.AddIntelligence(-1, "Ничего не понимает");
                    }

                    me.FightCharacter.SetIntelligenceForOneFight(0, "Ничего не понимает");
                    break;

                case "Я щас приду":
                    if (_rand.Luck(1, 9))
                    {
                        var acc = target.Passives.GlebChallengerTriggeredWhen;


                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                            return;


                        if (!target.Status.IsSkip)
                        {
                            target.Status.IsSkip = true;
                            target.Passives.GlebSkip = true;
                            game.Phrases.GlebComeBackPhrase.SendLog(target, true);

                            // Enemy sees message in their log
                            game.Phrases.GlebComeBackEnemy.SendLog(target, false);

                            var glebSkipFriendList = target.Passives.GlebSkipFriendList;
                            if (!glebSkipFriendList.FriendList.Contains(me.GetPlayerId()))
                                glebSkipFriendList.FriendList.Add(me.GetPlayerId());
                        }
                    }

                    break;

                case "Гребанные ассассины":
                    var ok = true;

                    //Сомнительная тактика
                    if (me.GameCharacter.Name == "DeepList")
                    {
                        deep = me.Passives.DeepListDoubtfulTactic;
                        if (!deep.FriendList.Contains(me.GetPlayerId()))
                            ok = false;
                    }
                    //end Сомнительная тактика

                    //10-7
                    if (me.FightCharacter.GetStrength() - target.FightCharacter.GetStrength() >= 3 && !target.Status.IsBlock && !target.Status.IsSkip && ok)
                    {
                        target.Status.IsAbleToWin = false;
                        game.Phrases.LeCrispAssassinsPhrase.SendLog(target, false);
                    }

                    break;

                case "Раммус мейн":
                    if (target.Status.IsBlock && game.RoundNo <= 10)
                    {
                        // target.Status.IsBlock = false;
                        me.Status.IsAbleToWin = false;
                        me.Status.IsArmorBreak = true;
                        var tolya = target.Passives.TolyaRammusTimes;
                        tolya.FriendList.Add(me.GetPlayerId());
                    }
                    break;

                case "Одиночество":
                    var hard = target.Passives.HardKittyLoneliness;
                    if (hard is { Activated: false })
                    {
                        target.Status.AddRegularPoints(1, "Одиночество");
                        game.Phrases.HardKittyLonelyPhrase.SendLog(target, true);
                        //uncomment it when DeepList desides to make it 1 per round again...
                        //hard.Activated = true;
                        var hardEnemy = hard.AttackHistory.Find(x => x.EnemyId == me.GetPlayerId());
                        if (hardEnemy == null)
                        {
                            hard.AttackHistory.Add(new HardKitty.LonelinessSubClass(me.GetPlayerId()));
                            hardEnemy = hard.AttackHistory.Find(x => x.EnemyId == me.GetPlayerId());
                        }

                        switch (game.RoundNo)
                        {
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                                hardEnemy!.Times += 1;
                                break;
                            case 5:
                            case 6:
                            case 7:
                            case 8:
                            case 9:
                                hardEnemy!.Times += 2;
                                break;
                            case 10:
                                hardEnemy!.Times += 4;
                                break;
                        }
                    }

                    break;

                case "Запах мусора":
                    var mitsuki = target.Passives.MitsukiGarbageList;


                    var found = mitsuki.Training.Find(x => x.EnemyId == me.GetPlayerId());
                    if (found != null)
                        found.Times++;
                    else
                        mitsuki.Training.Add(new Mitsuki.GarbageSubClass(me.GetPlayerId()));

                    break;

                case "Неприметность":
                    if (game.RoundNo  >= 10) break;
                    var saitamaAtkUnnoticedAfter = target.Passives.SaitamaUnnoticed;
                    // Saitama holds back against enemies NOT in top 2 — they appear to win
                    var saitamaDefUnnoticed = target.Passives.SaitamaUnnoticed;
                    if (!saitamaDefUnnoticed.SeriousTargets.Contains(me.GetPlayerId()))
                    {
                        target.Status.IsAbleToWin = false;
                        game.Phrases.SaitamaHoldsBack.SendLog(target, false);
                        
                        // Defer the win point (remove 1 from pending score)
                        target.Status.AddRegularPoints(-1, "Неприметность");
                        saitamaAtkUnnoticedAfter.DeferredPoints += 1;
                        
                        // Defer moral too (underdog moral only applies when we had worse place)
                        var moralGain = target.Status.GetPlaceAtLeaderBoard() - me.Status.GetPlaceAtLeaderBoard();
                        if (moralGain > 0 && game.RoundNo > 1)
                        {
                            target.GameCharacter.AddMoral(-moralGain, "Неприметность", isFightMoral: true);
                            saitamaAtkUnnoticedAfter.DeferredMoral += moralGain;
                        }
                    }
                    else
                    {
                        game.Phrases.SaitamaSerious.SendLog(target, false);
                    }
                    break;

                case "Огурчик Рик":
                    if (target.Passives.RickPickle.PickleTurnsRemaining > 0)
                    {
                        target.Passives.RickPickle.WasAttackedAsPickle = true;
                        me.Status.IsAbleToWin = false;
                    }
                    break;

                // Вороны (defense): reduce attacker speed by crow count on attacker
                case "Вороны":
                    var crowsDef = target.Passives.ItachiCrows;
                    if (crowsDef.CrowCounts.TryGetValue(me.GetPlayerId(), out var crowCountDef) && crowCountDef > 0)
                    {
                        me.FightCharacter.AddSpeedForOneFight(-crowCountDef);
                    }
                    break;

                // Аматерасу (defense): auto-win if attacker effective speed < Itachi's speed
                case "Аматерасу":
                    var itachiSpeedDef = target.FightCharacter.GetSpeed();
                    var attackerEffectiveSpeedDef = me.FightCharacter.GetSpeed();
                    if (attackerEffectiveSpeedDef < itachiSpeedDef)
                    {
                        me.Status.IsAbleToWin = false;
                        game.Phrases.ItachiAmaterasu.SendLog(target, false);
                    }
                    break;

                // Napoleon — Мирный договор: enforce treaty from previous round
                case "Мирный договор":
                    if (target.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(me.GetPlayerId()))
                    {
                        me.Status.IsAbleToWin = false;
                        target.Passives.NapoleonPeaceTreaty.TreatyEnemies.Remove(me.GetPlayerId());
                        game.Phrases.NapoleonPeaceTreaty.SendLog(target, false);
                    }
                    break;

                // Napoleon — Меня надо знать в лицо: auto-win first fight vs each unique attacker
                case "Меня надо знать в лицо":
                    var napFirstFight = target.Passives.NapoleonFirstFightList;
                    if (!napFirstFight.FriendList.Contains(me.GetPlayerId()))
                    {
                        napFirstFight.FriendList.Add(me.GetPlayerId());
                        me.Status.IsAbleToWin = false;
                        game.Phrases.NapoleonFace.SendLog(target, false);
                    }
                    break;

                case "Тоннели Гоблинов":
                    // 33% chance to escape if goblin speed >= enemy speed + 2
                    if (target.FightCharacter.GetSpeed() >= me.FightCharacter.GetSpeed() + 2)
                    {
                        if (_rand.Random(0, 99) < 50)
                        {
                            me.Status.IsAbleToWin = false;
                            game.Phrases.GoblinTunnelEscape.SendLog(target, false);
                            target.Status.AddInGamePersonalLogs("Тоннели Гоблинов: Сбежали!\n");
                        }
                    }
                    break;

                case "Гоблины":
                    // Stats already include warrior/hob bonuses via Set calls
                    break;

                // TheBoys — Регенерация Кимико: reduce attacker's justice when defending
                case "Регенерация Кимико":
                    var kimikoDefBefore = target.Passives.TheBoysKimiko;
                    if (!kimikoDefBefore.IsDisabled && kimikoDefBefore.RegenLevel > 0)
                    {
                        var currentJustice = me.FightCharacter.Justice.GetRealJusticeNow();
                        var reduction = Math.Min(currentJustice, kimikoDefBefore.RegenLevel);
                        if (reduction > 0)
                        {
                            me.FightCharacter.Justice.SetJusticeForOneFight(
                                Math.Max(0, currentJustice - reduction), "Регенерация Кимико");
                            kimikoDefBefore.TotalJusticeBlocked += reduction;
                            target.Status.AddInGamePersonalLogs(
                                $"Kimiko поглотила {reduction} Справедливости\n");
                            game.Phrases.TheBoysKimikoRegen.SendLog(target, false);
                        }
                    }
                    break;

                // Геральт — Плотва: speed bonus when defending at lower position vs higher position attacker
                case "Плотва":
                    if (target.GameCharacter.Name == "Геральт")
                    {
                        var geraltDefPos = target.Status.GetPlaceAtLeaderBoard();
                        var attackerDefPos = me.Status.GetPlaceAtLeaderBoard();
                        if (geraltDefPos >= 4 && attackerDefPos <= 3)
                        {
                            var plotvaDefSpeed = geraltDefPos - attackerDefPos;
                            target.FightCharacter.AddSpeedForOneFight(plotvaDefSpeed);
                            game.Phrases.GeraltPlotva.SendLog(target, false);
                        }
                    }
                    break;
            }

        // Napoleon ally treaty: if defender is Napoleon's ally, enforce treaty
        var napoleonForAlly = game.PlayersList.Find(x =>
            x.GameCharacter.Passive.Any(p => p.PassiveName == "Мирный договор") &&
            x.Passives.NapoleonAlliance.AllyId == target.GetPlayerId());
        if (napoleonForAlly != null && napoleonForAlly.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(me.GetPlayerId()))
        {
            me.Status.IsAbleToWin = false;
            napoleonForAlly.Passives.NapoleonPeaceTreaty.TreatyEnemies.Remove(me.GetPlayerId());
            game.Phrases.NapoleonPeaceTreaty.SendLog(target, false);
        }
    }

    public void HandleDefenseAfterBlockOrFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                // Napoleon — Мирный договор: register treaty when enemy attacks Napoleon's block
                case "Мирный договор":
                    if (target.Status.IsBlock)
                    {
                        if (!target.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(me.GetPlayerId()))
                            target.Passives.NapoleonPeaceTreaty.TreatyEnemies.Add(me.GetPlayerId());
                    }
                    break;

                case "Гоблины тупые, но не идиоты":
                    // Ziggurat build logic moved to HandleEndOfRound (fires on block regardless of attacker)
                    break;
            }

        // Napoleon ally treaty: if defender is Napoleon's ally and is blocking, register treaty on Napoleon
        if (target.Status.IsBlock)
        {
            var napoleonForAllyBlock = game.PlayersList.Find(x =>
                x.GameCharacter.Passive.Any(p => p.PassiveName == "Мирный договор") &&
                x.Passives.NapoleonAlliance.AllyId == target.GetPlayerId());
            if (napoleonForAllyBlock != null && !napoleonForAllyBlock.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(me.GetPlayerId()))
                napoleonForAllyBlock.Passives.NapoleonPeaceTreaty.TreatyEnemies.Add(me.GetPlayerId());
        }
    }


    public void HandleDefenseAfterBlockOrFightOrSkip(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Гребанные ассассины":
                    //5-2 = 3
                    if (me.FightCharacter.GetStrength() - target.FightCharacter.GetStrength() < 3)
                    {
                        var leCrip = target.Passives.LeCrispAssassins;
                        leCrip.AdditionalPsycheForNextRound += 1;
                    }

                    break;
            }
    }

    public void HandleDefenseAfterFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Я щас приду":
                    var glebSkipFriendList = target.Passives.GlebSkipFriendList;
                    var glebSkipFriendListDone = target.Passives.GlebSkipFriendListDone;

                    if (glebSkipFriendList.FriendList.Contains(me.GetPlayerId()) &&
                        !glebSkipFriendListDone.FriendList.Contains(me.GetPlayerId()))
                    {
                        glebSkipFriendListDone.FriendList.Add(me.GetPlayerId());
                        me.GameCharacter.AddMoral(9, "Я щас приду", false);
                        me.Status.AddInGamePersonalLogs("Я щас приду: +9 *Морали*. Вы дождались Глеба!!! Празднуем!");
                    }

                    break;

                case "Импакт":
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var lePuska = target.Passives.LeCrispImpact;

                        lePuska.IsLost = true;
                    }

                    break;

                case "Mute":
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var hardKittyMute = target.Passives.HardKittyMute;

                        if (!hardKittyMute.UniquePlayers.Contains(me.GetPlayerId()))
                        {
                            hardKittyMute.UniquePlayers.Add(me.GetPlayerId());
                            me.Status.AddRegularPoints(1, "Mute");
                            game.Phrases.HardKittyMutedPhrase.SendLog(target, false);
                        }
                    }

                    break;

                case "Доебаться":
                    var hardKittyDoebatsya = target.Passives.HardKittyDoebatsya;

                    var found = hardKittyDoebatsya.LostSeriesCurrent.Find(x => x.EnemyPlayerId == me.GetPlayerId());
                    if (found != null) hardKittyDoebatsya.EnemyPlayersLostTo.Add(me.GetPlayerId());
                    //found.Series = 0;
                    //game.Phrases.HardKittyDoebatsyaAnswerPhrase.SendLog(target, false);
                    break;

                case "Гигантские бобы":
                    var beansDefAfter = target.Passives.RickGiantBeans;
                    if (beansDefAfter.IngredientsActive && beansDefAfter.IngredientTargets.Contains(me.GetPlayerId())
                        && target.Status.IsWonThisCalculation == me.GetPlayerId())
                    {
                        beansDefAfter.IngredientTargets.Remove(me.GetPlayerId());
                        beansDefAfter.BeanStacks++;
                        target.GameCharacter.AddStrength(-1, "Гигантские бобы");
                        target.GameCharacter.AddSpeed(-1, "Гигантские бобы");
                        target.MinusPsycheLog(target.GameCharacter, game, -1, "Гигантские бобы");
                        var oldFakeBeansD = beansDefAfter.FakeIntelligence;
                        beansDefAfter.FakeIntelligence = beansDefAfter.BaseIntelligence * beansDefAfter.BeanStacks;
                        target.GameCharacter.AddIntelligence(beansDefAfter.FakeIntelligence - oldFakeBeansD, "Гигантские бобы");
                        game.Phrases.RickGiantBeansDrink.SendLog(target, false);
                        // Portal gun invention is handled by HandleEndOfRound (not here)
                        // to prevent the gun from auto-firing on the same fight it was invented
                    }
                    break;

                case "Парень с сюрпризом":
                    // Салдорум defending — lost to higher-ranked attacker → mark attacker as Хохол
                    if (target.Status.IsLostThisCalculation != Guid.Empty
                        && me.Status.GetPlaceAtLeaderBoard() < target.Status.GetPlaceAtLeaderBoard())
                    {
                        if (!target.Passives.SaldorumKhokholList.MarkedEnemies.Contains(me.GetPlayerId()))
                            target.Passives.SaldorumKhokholList.MarkedEnemies.Add(me.GetPlayerId());
                        target.GameCharacter.AddMoral(5, "Парень с сюрпризом");
                        if (_rand.Random(0, 99) < 33)
                            target.GameCharacter.AddMoral(5, "Парень с сюрпризом");
                        game.Phrases.SaldorumSurprise.SendLog(target, me, delete: true);
                    }
                    break;

                case "Сало":
                    {
                        // target = Салдорум (defender), me = attacker
                        var isKhokhol = target.Passives.SaldorumKhokholList.MarkedEnemies.Contains(me.GetPlayerId())
                                        || me.GameCharacter.Name is "mylorik" or "Sirinoks";
                        if (isKhokhol && target.Status.IsWonThisCalculation != Guid.Empty)
                        {
                            target.GameCharacter.Justice.AddJusticeForNextRoundFromSkill();
                            var moral = Math.Abs(me.Status.GetPlaceAtLeaderBoard() - target.Status.GetPlaceAtLeaderBoard());
                            if (moral > 0 && target.Status.GetPlaceAtLeaderBoard() > me.Status.GetPlaceAtLeaderBoard())
                                target.GameCharacter.AddMoral(moral, "Сало");
                            game.Phrases.SaldorumSalo.SendLog(target, me, delete: true);
                        }
                    }
                    break;

                case "Гоблины":
                    // Goblins die when losing on defense (percentage-based)
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var gobDefLossPop = target.Passives.GoblinPopulation;
                        var defDeathPct = 10 + 0.5*game.RoundNo*game.RoundNo/3;
                        if (target.Status.FightEnemyWasTooGood) defDeathPct += 5;
                        if (target.Status.FightEnemyWasTooStronk) defDeathPct += 5;
                        var defDeathCount = Math.Max(1, (int)Math.Ceiling(gobDefLossPop.TotalGoblins * defDeathPct / 100.0));
                        gobDefLossPop.TotalGoblins = Math.Max(1, gobDefLossPop.TotalGoblins - defDeathCount);
                        game.Phrases.GoblinDeath.SendLog(target, false);
                        target.Status.AddInGamePersonalLogs($"Гоблины: -{defDeathCount} ({defDeathPct}%). Осталось: {gobDefLossPop.TotalGoblins}\n");
                    }
                    break;

                case "Близнец":
                    // target = Монстр (defender/blocker), me = attacker
                    if (target.Status.IsBlock)
                    {
                        var stolenJustice = me.FightCharacter.Justice.GetRealJusticeNow();
                        if (stolenJustice > 0)
                        {
                            me.GameCharacter.Justice.SetRealJusticeNow(0, "Близнец");
                            target.GameCharacter.Justice.AddRealJusticeNow(stolenJustice);
                            target.Status.AddBonusPoints(stolenJustice, "Близнец");
                            //target.Status.AddInGamePersonalLogs($"Близнец: Украл {stolenJustice} Справедливости у {me.DiscordUsername}. +{stolenJustice} бонусных очков\n");
                            //me.Status.AddInGamePersonalLogs($"Близнец: {target.DiscordUsername} украл всю твою Справедливость!\n");
                            game.Phrases.MonsterTwinSteal.SendLog(target, false);
                        }
                    }
                    break;

                // TheBoys — Регенерация Кимико: disable on defense loss
                case "Регенерация Кимико":
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        target.Passives.TheBoysKimiko.DisabledNextRound = true;
                        game.Phrases.TheBoysKimikoDisabled.SendLog(target, false);
                    }
                    break;

                // Salldorum — Очко: +1 bonus point when attacked by someone lower on leaderboard
                case "Очко":
                    if (target.GameCharacter.Name == "Salldorum"
                        && me.Status.GetPlaceAtLeaderBoard() > target.Status.GetPlaceAtLeaderBoard())
                    {
                        target.Status.AddBonusPoints(1, "Очко");
                        game.Phrases.SalldorumOchko.SendLog(target, false);
                    }
                    break;

                // Геральт — Ведьмачий Заказ (defense after fight): remove contract from attacker when they beat Geralt
                case "Ведьмачий Заказ":
                    if (target.GameCharacter.Name == "Геральт" && me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        var geraltDefContracts = target.Passives.GeraltContracts;
                        var attackerId = me.GetPlayerId();
                        if (geraltDefContracts.ContractMap.ContainsKey(attackerId) && geraltDefContracts.ContractMap[attackerId].Count > 0)
                        {
                            var removedDef = geraltDefContracts.ContractMap[attackerId][0];
                            geraltDefContracts.ContractMap[attackerId].RemoveAt(0);
                            if (me.Passives.GeraltContractsOnMe.Contains(removedDef))
                                me.Passives.GeraltContractsOnMe.Remove(removedDef);
                            if (geraltDefContracts.ContractMap[attackerId].Count == 0)
                            {
                                geraltDefContracts.ContractMap.Remove(attackerId);
                                me.Passives.GeraltContractsOnMe.Clear();
                                me.Passives.GeraltContractOwnerId = Guid.Empty;
                            }
                            game.Phrases.GeraltContractLost.SendLog(target, false);
                        }
                    }
                    break;

            }
    }

    public void HandleAttackBeforeFight(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
    {
        // Seller forced loss: marked player loses next attack
        if (me.Passives.SellerForcedLossNextAttack)
            me.Status.IsAbleToWin = false;

        foreach (var passive in me.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "AutoWin":
                    target.Status.IsAbleToWin = false;
                    me.Status.IsArmorBreak = true;
                    me.Status.IsSkipBreak = true;
                    break;

                case "Глаза бога смерти":
                    var eyes = me.Passives.KiraShinigamiEyes;
                    if (eyes.EyesActiveForNextAttack)
                    {
                        if (target.GameCharacter.Passive.Any(x => x.PassiveName == "Выдуманный персонаж"))
                        {
                            me.Status.AddInGamePersonalLogs("Глаза бога смерти: У этого монстра нет имени...\n");
                        }
                        else if (target.GetPlayerId() == me.Passives.KiraL.LPlayerId)
                        {
                            // Don't consume eyes on L — keep them for a useful target
                            me.Status.AddInGamePersonalLogs("Глаза бога смерти: Ты не можешь увидеть имя L...\n");
                        }
                        else
                        {
                            eyes.EyesActiveForNextAttack = false;
                            me.Status.AddInGamePersonalLogs($"Глаза бога смерти: {target.DiscordUsername} - это **{target.GameCharacter.Name}**\n");
                            if (!eyes.RevealedPlayers.Contains(target.GetPlayerId()))
                                eyes.RevealedPlayers.Add(target.GetPlayerId());
                            game.Phrases.KiraShinigamiEyes.SendLog(me, false);
                        }
                    }
                    break;

                case "Следит за игрой":
                    foreach (var metaPlayer in me.Passives.YongGlebMetaClass)
                    {
                        if (target.GetPlayerId() == metaPlayer)
                        {
                           me.Status.AddBonusPoints(1, "Следит за игрой");
                           game.Phrases.YongGlebMeta.SendLog(me, true);
                        }
                    }
                    break;

                case "Коммуникация":
                    if (game.RoundNo == 6)
                    {
                        if (target.GameCharacter.Passive.Any(x => x.PassiveName == "Выдуманный персонаж"))
                        {
                            me.Status.AddInGamePersonalLogs("Коммуникация: Не удалось просветить\n");
                            break;
                        }
                        var commLogSnippet = $"Пиквард просветил {target.GameCharacter.Name}";
                        game.AddGlobalLogs(commLogSnippet);
                        game.KiraHiddenLogSnippets.Add(commLogSnippet);
                        game.Phrases.YongGlebCommunication.SendLog(me, false);
                    }
                    break;

                case "Сомнительная тактика":
                    var deep = me.Passives.DeepListDoubtfulTactic;

                    if (!deep.FriendList.Contains(target.GetPlayerId()))
                        me.Status.IsAbleToWin = false;

                    break;

                case "Возвращение из мертвых":
                    if (game.RoundNo >= 10)
                    {
                        me.Status.IsArmorBreak = true;
                        me.Status.IsSkipBreak = true;
                    }

                    break;

                case "Охота на богов":
                    if (me.GameCharacter.HasSkillTargetOn(target.GameCharacter))
                    {
                        game.Phrases.KratosTarget.SendLog(me, false);
                        me.FightCharacter.SetSkillFightMultiplier(2);
                        if (game.IsKratosEvent && game.RoundNo > 10)
                            me.FightCharacter.SetSkillFightMultiplier(4);
                    }

                    break;

                case "Подсчет":
                    var tolya = me.Passives.TolyaCount;

                    if (tolya.IsReadyToUse && me.Status.WhoToAttackThisTurn.Count != 0)
                    {
                        tolya.TargetList.Add(new Tolya.TolyaCountSubClass(target.GetPlayerId(), game.RoundNo));
                        tolya.IsReadyToUse = false;
                        tolya.Cooldown = _rand.Random(2, 3);
                    }

                    break;

                case "Оборотень":
                    var myTempStrength = me.FightCharacter.GetStrength();
                    var targetTempStrength = target.FightCharacter.GetStrength();
                    me.FightCharacter.SetStrengthForOneFight(targetTempStrength, "Оборотень");
                    target.FightCharacter.SetStrengthForOneFight(myTempStrength, "Оборотень");

                    /*var myTempSkillMain = me.GameCharacter.GetSkillForOneFight();
                    var targetTempSkill = target.GameCharacter.GetSkillForOneFight();
                    me.GameCharacter.SetSkillForOneFight(targetTempSkill, "Оборотень");
                    target.GameCharacter.SetSkillForOneFight(myTempSkillMain, "Оборотень");*/
                    break;

                case "Безжалостный охотник":
                    me.Status.IsArmorBreak = true;
                    me.Status.IsSkipBreak = true;
                    if (target.Status.IsBlock || target.Status.IsSkip)
                        game.Phrases.WeedwickRuthlessHunter.SendLog(me, false);

                    // Most wanted: always sense Rick regardless of Justice
                    var isMostWantedHunter = target.GameCharacter.Passive.Any(x => x.PassiveName == "Most wanted");
                    if (target.GameCharacter.Justice.GetRealJusticeNow() == 0 || isMostWantedHunter)
                    {
                        var tempSpeed = me.FightCharacter.GetSpeed() * 2;
                        me.FightCharacter.SetSpeedForOneFight(tempSpeed, "Безжалостный охотник");
                    }

                    break;

                case "Им это не понравится":
                    var spartanMark = me.Passives.SpartanMark;
                    if (spartanMark != null)
                        if (target.Status.IsBlock && spartanMark.FriendList.Contains(target.GetPlayerId()))
                        {
                            spartanMark.BlockedPlayer = target.GetPlayerId();
                            me.Status.IsArmorBreak = true;
                            target.Status.IsAbleToWin = false;
                            game.Phrases.SpartanTheyWontLikeIt.SendLog(me, false);
                        }

                    break;

                case "DragonSlayer":
                    if (game.RoundNo == 10)
                        if (target.GameCharacter.Passive.Any(x => x.PassiveName == "Дракон"))
                        {
                            var isBuffed = game.PlayersList.Any(p =>
                                p.GameCharacter.Passive.Any(x => x.PassiveName == "Buffing") &&
                                p.Passives.SupportPremade.MarkedPlayerId == target.GetPlayerId());

                            if (isBuffed)
                            {
                                game.AddGlobalLogs("**DragonSlayer**: Дракон под защитой Суппорта!\n");
                                break;
                            }

                            target.Status.IsAbleToWin = false;
                            game.AddGlobalLogs("**Я DRAGONSLAYER!**\n" +
                                               $"{me.DiscordUsername} побеждает дракона и забирает **1000 голды**!");
                            foreach (var p in game.PlayersList) game.Phrases.SpartanDragonSlayer.SendLog(p, false);
                        }

                    break;

                case "Первая кровь":
                    var pant = me.Passives.SpartanFirstBlood;
                    if (pant.FriendList.Count == 0) pant.FriendList.Add(target.GetPlayerId());
                    break;

                case "Они позорят военное искусство":
                    var spartan = me.Passives.SpartanShame;

                    if (target.GameCharacter.Name == "mylorik" && !spartan.FriendList.Contains(target.GetPlayerId()))
                    {
                        spartan.FriendList.Add(target.GetPlayerId());
                        me.GameCharacter.AddPsyche(1, "ОН уважает военное искусство!");
                        target.GameCharacter.AddPsyche(1, "ОН уважает военное искусство!");
                        game.Phrases.SpartanShameMylorik.SendLog(me, false);
                    }

                    if (target.GameCharacter.Name == "Кратос" && !spartan.FriendList.Contains(target.GetPlayerId()))
                    {
                        spartan.FriendList.Add(target.GetPlayerId());
                        me.GameCharacter.AddPsyche(1, "Отец?");
                        target.GameCharacter.AddPsyche(1, "Boi?");
                        game.Phrases.SpartanShameMylorik.SendLog(me, false);
                    }

                    if (!spartan.FriendList.Contains(target.GetPlayerId()))
                    {
                        spartan.FriendList.Add(target.GetPlayerId());
                        target.GameCharacter.AddStrength(-1, "Они позорят военное искусство");
                        target.GameCharacter.AddSpeed(-1, "Они позорят военное искусство");
                    }

                    break;

                case "Я за чаем":
                    var geblTea = me.Passives.GlebTea;

                    if (geblTea.Ready && me.Status.WhoToAttackThisTurn.Count != 0)
                    {
                        geblTea.Ready = false;
                        target.Passives.GlebTeaTriggeredWhen = new WhenToTriggerClass(game.RoundNo + 1);
                        me.Status.AddRegularPoints(1, "Я за чаем");
                        game.Phrases.GlebTeaPhrase.SendLog(me, true);
                    }

                    break;

                case "Спокойствие":
                    var yongGlebTea = me.Passives.YongGlebTea;

                    if (yongGlebTea.IsReadyToUse && me.Status.WhoToAttackThisTurn.Count != 0)
                    {
                        yongGlebTea.IsReadyToUse = false;
                        yongGlebTea.Cooldown = 2;

                        target.Passives.GlebTeaTriggeredWhen = new WhenToTriggerClass(game.RoundNo + 1);
                        me.Status.AddRegularPoints(1, "Спокойствие");
                        game.Phrases.YongGlebTea.SendLog(me, true);
                    }
                    break;

                case "Заводить друзей":
                    var siri = me.Passives.SirinoksFriendsList;
                    var siriAttack = me.Passives.SirinoksFriendsAttack;

                    if (siri != null && siriAttack != null)
                        if (siri.FriendList.Contains(target.GetPlayerId()))
                            if (target.Status.IsBlock || target.Status.IsSkip)
                            {
                                siriAttack.EnemyId = target.GetPlayerId();
                                me.Status.IsArmorBreak = true;
                                me.Status.IsSkipBreak = true;
                            }


                    if (!siri!.FriendList.Contains(target.GetPlayerId()))
                    {
                        siri.FriendList.Add(target.GetPlayerId());
                        me.Status.AddRegularPoints(1, "Заводить друзей");
                        game.Phrases.SirinoksFriendsPhrase.SendLog(me, true);
                    }

                    break;

                case "Научите играть":
                    var awdka = me.Passives.AwdkaTeachToPlay;
                    var awdkaHistory = me.Passives.AwdkaTeachToPlayHistory;

                    var player2Stats = new List<Sirinoks.TrainingSubClass>
                    {
                        new(1, target.FightCharacter.GetIntelligence()),
                        new(2, target.FightCharacter.GetStrength()),
                        new(3, target.FightCharacter.GetSpeed()),
                        new(4, target.FightCharacter.GetPsyche())
                    };
                    var sup = player2Stats.OrderByDescending(x => x.StatNumber).ToList().First();

                    awdka.Training.Add(new Sirinoks.TrainingSubClass(sup.StatIndex, sup.StatNumber));


                    var enemy = awdkaHistory.History.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                    if (enemy == null)
                    {
                        awdkaHistory.History.Add(new Awdka.TeachToPlayHistoryListClass(target.GetPlayerId(),
                            $"{sup.StatIndex}", sup.StatNumber));
                    }
                    else
                    {
                        enemy.Text = $"{sup.StatIndex}";
                        enemy.Stat = sup.StatNumber;
                    }

                    break;

                case "Я пытаюсь!":
                    var awdkaTrying = me.Passives.AwdkaTryingList;
                    var awdkaTryingTarget = awdkaTrying?.TryingList.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                    if (awdkaTryingTarget is { IsUnique: true }) me.FightCharacter.SetSkillFightMultiplier(2);
                    break;


                case "Падальщик":
                    if (target.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                        if (target.GameCharacter.Justice.GetRealJusticeNow() > 0)
                        {
                            var howMuchIgnores = 1;
                            target.Passives.VampyrIgnoresOneJustice = howMuchIgnores;
                            target.GameCharacter.Justice.SetJusticeForOneFight(target.GameCharacter.Justice.GetRealJusticeNow() - howMuchIgnores, "Падальщик");
                        }

                    break;
                case "Спарта":
                    var mylorikSpartan = me.Passives.MylorikSpartan;
                    var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                    if (mylorikEnemy == null)
                    {
                        mylorikSpartan.Enemies.Add(new Mylorik.MylorikSpartanSubClass(target.GetPlayerId()));
                        mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                    }

                    if (me.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                        //set FightMultiplier
                    {
                        switch (mylorikEnemy!.LostTimes)
                        {
                            case 1:
                                me.FightCharacter.SetSkillFightMultiplier(2);
                                break;
                            case 2:
                                me.FightCharacter.SetSkillFightMultiplier(4);
                                break;
                            case 3:
                                me.FightCharacter.SetSkillFightMultiplier(8);
                                break;
                            case 4:
                            case 5:
                            case 6:
                            case 7:
                            case 8:
                            case 9:
                            case 10:
                                me.FightCharacter.SetSkillFightMultiplier(16);
                                game.AddGlobalLogs(
                                    $"mylorik: Айсик, можно тортик? У меня {me.GameCharacter.GetSkill()} *Скилла*!");
                                break;
                            default:
                                me.FightCharacter.SetSkillFightMultiplier();
                                break;
                        }

                        if (me.FightCharacter.GetSkillFightMultiplier() > 1)
                            me.Status.AddInGamePersonalLogs(
                                $"Спарта: {(int)(me.FightCharacter.GetSkill())} *Скилла* против {target.DiscordUsername}\n");
                    }

                    break;

                case "Питается водорослями":
                    if (target.Status.GetPlaceAtLeaderBoard() >= 4) me.Status.AddBonusPoints(1, "Питается водорослями");
                    break;

                case "Неприметность":
                    // Saitama holds back against enemies NOT in top 2
                    //if (game.RoundNo  >= 10) break;
                    //var saitamaAtkUnnoticed = me.Passives.SaitamaUnnoticed;
                    //if (!saitamaAtkUnnoticed.SeriousTargets.Contains(target.GetPlayerId()))
                    //{
                    //    me.Status.IsAbleToWin = false;
                    //    game.Phrases.SaitamaHoldsBack.SendLog(me, false);
                    //}
                    //else
                    //{
                    //    game.Phrases.SaitamaSerious.SendLog(me, false);
                    //}
                    break;

                case "Портальная пушка":
                    var gunAtk = me.Passives.RickPortalGun;
                    if (gunAtk.Invented && gunAtk.Charges > 0)
                    {
                        target.Status.IsAbleToWin = false;
                        me.Status.IsArmorBreak = true;
                        me.Status.IsSkipBreak = true;
                    }
                    break;

                // Вороны: reduce target speed by crow count
                case "Вороны":
                    var crowsAtk = me.Passives.ItachiCrows;
                    if (crowsAtk.CrowCounts.TryGetValue(target.GetPlayerId(), out var crowCount) && crowCount > 0)
                    {
                        target.FightCharacter.AddSpeedForOneFight(-crowCount);
                    }
                    break;

                // Аматерасу: auto-win if target effective speed < Itachi's speed
                case "Аматерасу":
                    var itachiSpeedAtk = me.FightCharacter.GetSpeed();
                    var targetEffectiveSpeedAtk = target.FightCharacter.GetSpeed();
                    if (targetEffectiveSpeedAtk < itachiSpeedAtk)
                    {
                        target.Status.IsAbleToWin = false;
                        game.Phrases.ItachiAmaterasu.SendLog(me, false);
                    }
                    break;

                case "Впарить говна":
                    var sellerVparit = me.Passives.SellerVparitGovna;
                    if (sellerVparit.Cooldown <= 0)
                    {
                        // Add 500 skill BEFORE enabling siphon (so 500 is excluded)
                        var savedSiphon = target.GameCharacter.SkillSiphonBox;
                        target.GameCharacter.SkillSiphonBox = null;
                        target.GameCharacter.AddExtraSkill(500, "Впарить говна");
                        target.GameCharacter.SkillSiphonBox = savedSiphon ?? 0; // enable/restore siphon

                        // Track total skill added (for removal when mark expires)
                        target.Passives.SellerVparitGovnaTotalSkill += 500;
                        target.Passives.SellerVparitGovnaRoundsLeft = 4;

                        // Track in seller's list
                        if (!sellerVparit.MarkedPlayers.Contains(target.GetPlayerId()))
                            sellerVparit.MarkedPlayers.Add(target.GetPlayerId());

                        sellerVparit.Cooldown = 2;
                        game.Phrases.SellerVparit.SendLog(me, false);
                        game.Phrases.SellerVparitEnemy.SendLog(target, false);
                        target.Passives.SellerForcedLossNextAttack = true;
                    }
                    break;

                case "Макро":
                    me.Passives.DopaMacro.FightsProcessed++;
                    if (me.Passives.DopaMacro.FightsProcessed > 1)
                        me.Status.HideCurrentFight = true;
                    break;

                case "Ниндзя":
                    me.Passives.SaldorumNinjaHidden = false;
                    if (_rand.Random(0, 99) < 50)
                    {
                        me.Passives.SaldorumNinjaHidden = true;
                        me.Status.HideCurrentFight = true;
                    }
                    break;

                // Napoleon — Вступить в союз: form alliance on first attack; check joint attacks
                case "Вступить в союз":
                    var napAlliance = me.Passives.NapoleonAlliance;
                    if (napAlliance.AllyId == Guid.Empty)
                    {
                        napAlliance.AllyId = target.GetPlayerId();
                        target.Status.AddInGamePersonalLogs(
                            "Napoleon Wonnafuck предлагает вам вступить в союз, нападайте вместе на одну цель, для избежания поражений\n");
                        break;
                    }
                    var napAlly = game.PlayersList.Find(x => x.GetPlayerId() == napAlliance.AllyId);
                    if (napAlly != null && napAlly.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                    {
                        target.Status.IsAbleToWin = false;
                        me.GameCharacter.AddMoral(3, "Вступить в союз");
                    }
                    break;

                // Toxic Mate — "Aggress": if IsSkip or IsBlock was set by other passives, clear them
                case "Aggress":
                    if (me.Status.IsSkip || me.Status.IsBlock)
                    {
                        game.Phrases.ToxicMateAggressWontStop.SendLog(me, false);
                        me.Status.IsSkip = false;
                        me.Status.IsBlock = false;
                    }
                    break;

                // Таинственный Суппорт — "Premade": mark first target as partner
                case "Premade":
                    if (me.Passives.SupportPremade.MarkedPlayerId == Guid.Empty)
                    {
                        me.Passives.SupportPremade.MarkedPlayerId = target.GetPlayerId();
                        me.Status.AddInGamePersonalLogs($"Premade: {target.DiscordUsername} теперь твой напарник\n");
                        game.Phrases.SupportPremadeMark.SendLog(me, false);
                    }
                    break;

                // Таинственный Суппорт — "Buffing": buff marked player's lowest stat
                case "Buffing":
                    if (target.GetPlayerId() == me.Passives.SupportPremade.MarkedPlayerId)
                    {
                        var bInt = target.GameCharacter.GetIntelligence();
                        var bStr = target.GameCharacter.GetStrength();
                        var bSpd = target.GameCharacter.GetSpeed();
                        var bPsy = target.GameCharacter.GetPsyche();
                        var bMin = Math.Min(Math.Min(bInt, bStr), Math.Min(bSpd, bPsy));

                        if (bMin == bInt)
                            target.GameCharacter.AddIntelligence(2, "Buffing", isLog: false);
                        else if (bMin == bStr)
                            target.GameCharacter.AddStrength(2, "Buffing", isLog: false);
                        else if (bMin == bSpd)
                            target.GameCharacter.AddSpeed(2, "Buffing", isLog: false);
                        else
                            target.GameCharacter.AddPsyche(2, "Buffing", isLog: false);

                        me.Status.AddInGamePersonalLogs($"Buffing: Усилил {target.DiscordUsername}\n");
                    }
                    break;

                case "Гоблины":
                    me.Passives.GoblinLastAttackedPlayer = target.GetPlayerId();
                    break;

                case "Отличный рудник":
                    // Attacking mine position (1, 2, or 6) — raid for bonus points
                    var targetPlace = target.Status.GetPlaceAtLeaderBoard();
                    if (targetPlace is 1 or 2 or 6)
                    {
                        var raidWorkers = me.Passives.GoblinPopulation.Workers;
                        if (raidWorkers > 0)
                        {
                            me.Status.AddBonusPoints(raidWorkers, "Отличный рудник");
                            game.Phrases.GoblinMine.SendLog(me, false);
                            //me.Status.AddInGamePersonalLogs($"Рудник: Обчистили на {raidWorkers} очков!\n");
                        }
                    }
                    break;

                case "Близнец":
                    // me = Монстр (attacker). If any stat matches target → -1 Psyche
                    var meInt = me.FightCharacter.GetIntelligence();
                    var meStr = me.FightCharacter.GetStrength();
                    var meSpd = me.FightCharacter.GetSpeed();
                    var mePsy = me.FightCharacter.GetPsyche();
                    var tInt = target.FightCharacter.GetIntelligence();
                    var tStr = target.FightCharacter.GetStrength();
                    var tSpd = target.FightCharacter.GetSpeed();
                    var tPsy = target.FightCharacter.GetPsyche();
                    if (meInt == tInt || meStr == tStr || meSpd == tSpd || mePsy == tPsy)
                    {
                        me.MinusPsycheLog(me.GameCharacter, game, -1, "Близнец");
                        //me.Status.AddInGamePersonalLogs("Близнец: Ваши статы совпали с врагом...");
                    }
                    break;

                // TheBoys — Кочерга Бучера: multiply skill during attack
                case "Кочерга Бучера":
                    var pokerCount = me.Passives.TheBoysButcher.PokerCount;
                    if (pokerCount > 0)
                    {
                        me.FightCharacter.SetSkillFightMultiplier(1 + pokerCount);
                        game.Phrases.TheBoysPoker.SendLog(me, false);
                    }
                    break;

                // Геральт — Ведьмачое Масло: per-type oil stacking on contract fights
                case "Ведьмачое Масло":
                    var geraltOilContracts = me.Passives.GeraltContracts;
                    geraltOilContracts.LastOilBonusCount = 0;
                    if (geraltOilContracts.LambertMixup)
                    {
                        me.FightCharacter.SetSkillForOneFight(0, "Ведьмачое Масло");
                        game.Phrases.GeraltLambertMixup.SendLog(me, false);
                        geraltOilContracts.LambertMixup = false;
                        break;
                    }
                    if (geraltOilContracts.OilsActivated
                        && !geraltOilContracts.WasAttackedDuringMeditation
                        && geraltOilContracts.CurrentRoundFightQueue.TryGetValue(target.GetPlayerId(), out var fightQueue)
                        && fightQueue.Count > 0)
                    {
                        var contractType = fightQueue.Dequeue();
                        var matchingOils = geraltOilContracts.OilInventory.Count(o => o == contractType);
                        if (matchingOils > 0)
                        {
                            me.FightCharacter.AddExtraSkill(100 * matchingOils, "Ведьмачое Масло", false);
                            me.FightCharacter.SetSkillFightMultiplier(
                                (int)me.FightCharacter.GetSkillFightMultiplier() + matchingOils);
                            var targetJustice = target.FightCharacter.Justice.GetRealJusticeNow();
                            if (targetJustice > 0)
                                target.FightCharacter.Justice.SetJusticeForOneFight(
                                    Math.Max(0, targetJustice - matchingOils), "Ведьмачое Масло");
                            geraltOilContracts.LastOilBonusCount = matchingOils;
                            game.Phrases.GeraltOilUsed.SendLog(me, false);
                        }
                    }
                    break;

                // Геральт — Плотва: speed bonus when attacking from lower position vs higher position target
                case "Плотва":
                    if (me.GameCharacter.Name == "Геральт")
                    {
                        var geraltAtkPos = me.Status.GetPlaceAtLeaderBoard();
                        var targetAtkPos = target.Status.GetPlaceAtLeaderBoard();
                        if (geraltAtkPos >= 4 && targetAtkPos <= 3)
                        {
                            var plotvaAtkSpeed = geraltAtkPos - targetAtkPos;
                            me.FightCharacter.AddSpeedForOneFight(plotvaAtkSpeed);
                            game.Phrases.GeraltPlotva.SendLog(me, false);
                        }
                    }
                    break;
            }
    }

    public void HandleAttackAfterFight(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
    {
        // Seller: mark target as "outplay" after forced loss
        if (me.Passives.SellerForcedLossNextAttack)
        {
            if (me.Status.IsLostThisCalculation != Guid.Empty &&
                !me.Passives.SellerOutplayTargets.Contains(target.GetPlayerId()))
                me.Passives.SellerOutplayTargets.Add(target.GetPlayerId());
            me.Passives.SellerForcedLossNextAttack = false;
        }

        foreach (var passive in me.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Exploit":
                    if (target.Passives.IsExploitable)
                    {
                        game.TotalExploit++;
                        target.Passives.IsExploitable = false;
                        target.Passives.IsExploitFixed = true;
                        if (game.TotalExploit > 0)
                        {
                            me.Status.AddInGamePersonalLogs($"```fix\nExploit patched: {target.DiscordUsername}\n> +{game.TotalExploit} points (accumulated)\n```\n");
                            me.Status.AddRegularPoints(game.TotalExploit, "Exploit", true);
                        }
                        game.TotalExploit = 0;
                    }
                    break;

                case "Много выебывается":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        if (me.GameCharacter.HasSkillTargetOn(target.GameCharacter))
                        {
                            me.GameCharacter.AddExtraSkill(40, "Много выебывается");
                        }
                    }
                    break;

                case "Выгодная сделка":
                    if (game.RoundNo == 10 && me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        var debt = target.Passives.SellerTacticBonusEarned;
                        if (debt > 0)
                        {
                            var stolen = Math.Ceiling(debt / 2);
                            target.Status.AddBonusPoints(-stolen, "Выгодная сделка");
                            me.Status.AddBonusPoints(stolen, "Выгодная сделка");
                        }
                    }
                    break;

                case "Монстр":
                    target.Passives.MonsterNoEscape = true;
                    break;

                case "Возвращение из мертвых":
                    if (game.IsKratosEvent && game.RoundNo > 10)
                        if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        {
                            // Goblins are immune to kill effects
                            if (target.GameCharacter.Name == "Стая Гоблинов") break;
                            game.AddGlobalLogs($"{me.GameCharacter.Name} **УБИЛ** {target.GameCharacter.Name}!");
                            game.AddGlobalLogs($"Они скинули **{target.DiscordUsername}**! Сволочи!");
                            game.Phrases.KratosEventKill.SendLog(me, true, isRandomOrder:false);
                            target.Passives.IsDead = true;
                            target.Passives.DeathSource = "Kratos";
                            // Achievement: Kratos kill
                            me.Passives.AchievementTracker.EnemiesKilledAsKratos++;
                            target.Passives.AchievementTracker.WasKilledByKratos = true;
                            // Монстр без имени: +1 regular point per death
                            foreach (var mp in game.PlayersList.Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Монстр")))
                            {
                                mp.Status.AddRegularPoints(1, "Монстр");
                                game.Phrases.MonsterDeath.SendLog(mp, false);
                            }
                        }
                    break;

                case "Weed":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        if (target.Passives.WeedwickWeed > 0)
                        {
                            me.GameCharacter.AddMoral(target.Passives.WeedwickWeed, "Weed");

                            switch (target.Passives.WeedwickWeed)
                            {
                                case 1:
                                    game.Phrases.WeedwickWeedYes1.SendLog(me, false);
                                    break;
                                case 2:
                                    game.Phrases.WeedwickWeedYes2.SendLog(me, false);
                                    break;
                                case 3:
                                    game.Phrases.WeedwickWeedYes3.SendLog(me, false);
                                    break;
                                case 4:
                                    game.Phrases.WeedwickWeedYes4.SendLog(me, false);
                                    break;
                                case 5:
                                    game.Phrases.WeedwickWeedYes5.SendLog(me, false);
                                    break;
                                case 6:
                                    game.Phrases.WeedwickWeedYes6.SendLog(me, false);
                                    break;
                                case 7:
                                    game.Phrases.WeedwickWeedYes7.SendLog(me, false);
                                    break;
                                case 8:
                                    game.Phrases.WeedwickWeedYes8.SendLog(me, false);
                                    break;
                                case 9:
                                    game.Phrases.WeedwickWeedYes9.SendLog(me, false);
                                    break;
                                case 10:
                                    game.Phrases.WeedwickWeedYes10.SendLog(me, false);
                                    break;
                                default:
                                    game.Phrases.WeedwickWeedYes11.SendLog(me, false);
                                    break;
                            }

                            target.Passives.WeedwickWeed = 0;
                            me.Passives.WeedwickLastRoundWeed = game.RoundNo;
                        }

                    break;

                case "Ценная добыча":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        var preyWinStreak = target.FightCharacter.GetWinStreak();
                        if (preyWinStreak > 0)
                        {
                            if (me.Status.GetPlaceAtLeaderBoard() > target.Status.GetPlaceAtLeaderBoard())
                            {
                                me.Status.AddRegularPoints(preyWinStreak, "Ценная добыча");
                            }
                            else
                            {
                                me.Status.AddBonusPoints(preyWinStreak, "Ценная добыча");
                            }
                        }

                        switch (preyWinStreak)
                        {
                            case 0:
                                break;
                            case 1:
                                break;
                            case 2:
                                game.Phrases.WeedwickValuablePreyPoints1.SendLog(me, false);
                                break;
                            case 3:
                                game.Phrases.WeedwickValuablePreyPoints2.SendLog(me, false);
                                break;
                            case 4:
                                game.Phrases.WeedwickValuablePreyPoints3.SendLog(me, false);
                                break;
                            case 5:
                                game.Phrases.WeedwickValuablePreyPoints4.SendLog(me, false);
                                break;
                            case 6:
                                game.Phrases.WeedwickValuablePreyPoints5.SendLog(me, false);
                                break;
                            case 7:
                                game.Phrases.WeedwickValuablePreyPoints6.SendLog(me, false);
                                break;
                            default:
                                game.Phrases.WeedwickValuablePreyPoints7.SendLog(me, false);
                                break;
                        }

                        //calculate range
                        var range = me.FightCharacter.GetSpeedQualityResistInt();
                        // ReSharper disable once RedundantAssignment
                        range -= target.FightCharacter.GetSpeedQualityKiteBonus();

                        var placeDiff = me.Status.GetPlaceAtLeaderBoard() - target.Status.GetPlaceAtLeaderBoard();
                        if (placeDiff < 0)
                            placeDiff *= -1;
                        //end calculate range

                        //WeedWick ignores range, so you calculated it for nothing! :)
                        range = 10;

                        if (placeDiff <= range && game.RoundNo > 1)
                        {
                            //обычный дроп (его тут нет, просто так тут это написал)
                            var harm = 0;

                            // 1/место в таблице.
                            if (_rand.Luck(1, target.Status.GetPlaceAtLeaderBoard()))
                            {
                                harm++;
                                target.GameCharacter.LowerQualityResist(target, game, me);
                                game.Phrases.WeedwickValuablePreyDrop.SendLog(me, false);
                            }

                            // 1/5
                            if (_rand.Luck(1, 5))
                            {
                                harm++;
                                target.GameCharacter.LowerQualityResist(target, game, me);
                                game.Phrases.WeedwickValuablePreyDrop.SendLog(me, false);
                            }

                            // 1/3 если враг топ1
                            if (_rand.Luck(1, 3) && target.Status.GetPlaceAtLeaderBoard() == 1)
                            {
                                harm++;
                                target.GameCharacter.LowerQualityResist(target, game, me);
                                game.Phrases.WeedwickValuablePreyDrop.SendLog(me, false);
                            }

                            if (harm > 0)
                            {
                                var bongs = $"Вы нанесли {harm} дополнительного вреда по {target.DiscordUsername} ";
                                for (var i = 0; i < harm; i++) bongs += "<:bong:1046462826539130950>";
                                me.Status.AddInGamePersonalLogs($"*{bongs}*\n");
                            }
                        }
                    }

                    break;

                case "Им это не понравится":
                    var spartanMark = me.Passives.SpartanMark;
                    if (spartanMark != null)
                        if (spartanMark.BlockedPlayer == target.GetPlayerId())
                        {
                            target.Status.IsBlock = true;
                            spartanMark.BlockedPlayer = Guid.Empty;
                        }

                    break;

                case "Падальщик":
                    if (target.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                        if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                            me.GameCharacter.AddMoral(3, "Падальщик");
                    break;

                case "Вампуризм":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        me.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(target.GameCharacter.Justice.GetRealJusticeNow() + target.Passives.VampyrIgnoresOneJustice);
                    target.Passives.VampyrIgnoresOneJustice = 0;
                    break;

                case "Неуязвимость":
                    if (me.Status.IsLostThisCalculation != Guid.Empty) me.Passives.OctopusInvulnerabilityList.Count++;
                    break;

                case "Обучение":
                    var siri = me.Passives.SirinoksTraining;

                    if (me.Status.IsLostThisCalculation != Guid.Empty &&
                        me.Status.WhoToAttackThisTurn.Contains(me.Status.IsLostThisCalculation))
                    {
                        var playerSheLostLastTime =
                            game.PlayersList.Find(x => x.GetPlayerId() == me.Status.IsLostThisCalculation);
                        var intel = new List<Sirinoks.StatsClass>
                        {
                            new(1, playerSheLostLastTime!.GameCharacter.GetIntelligence()),
                            new(2, playerSheLostLastTime.GameCharacter.GetStrength()),
                            new(3, playerSheLostLastTime.GameCharacter.GetSpeed()),
                            new(4, playerSheLostLastTime.GameCharacter.GetPsyche())
                        };

                        var intel2 = new List<Sirinoks.StatsClass>();
                        foreach (var i in intel)
                            switch (i.Index)
                            {
                                case 1:
                                    if (me.GameCharacter.GetIntelligence() < i.Number)
                                        intel2.Add(i);
                                    break;
                                case 2:
                                    if (me.GameCharacter.GetStrength() < i.Number)
                                        intel2.Add(i);
                                    break;
                                case 3:
                                    if (me.GameCharacter.GetSpeed() < i.Number)
                                        intel2.Add(i);
                                    break;
                                case 4:
                                    if (me.GameCharacter.GetPsyche() < i.Number)
                                        intel2.Add(i);
                                    break;
                            }

                        if (intel2.Count > 0)
                        {
                            var best = intel2.OrderByDescending(x => x.Number).ToList().First();


                            if (siri.Training.Count == 0)
                            {
                                siri.Training.Add(new Sirinoks.TrainingSubClass(best.Index, best.Number));
                                siri.EnemyId = playerSheLostLastTime.GetPlayerId();
                            }
                        }
                    }

                    break;

                case "Заводить друзей":
                    var siriAttack = me.Passives.SirinoksFriendsAttack;

                    if (siriAttack != null)
                        if (siriAttack.EnemyId == target.GetPlayerId())
                            siriAttack.EnemyId = Guid.Empty;
                    break;

                case "Повезло":
                    var darscsi = me.Passives.DarksciLuckyList;

                    if (!darscsi.TouchedPlayers.Contains(target.GetPlayerId()))
                        darscsi.TouchedPlayers.Add(target.GetPlayerId());

                    if (darscsi.TouchedPlayers.Count == game.PlayersList.Count - 1 && darscsi.Triggered == false)
                    {
                        var darksciType = me.Passives.DarksciTypeList;
                        var darksciUnstableMultiplier = 1;
                        if (darksciType.IsStableType)
                        {
                            me.Status.AddBonusPoints(me.Status.GetScore(), "Повезло");
                        }
                        else
                        {
                            darksciUnstableMultiplier = 2;
                            me.Status.AddBonusPoints(me.Status.GetScore() * 2, "Повезло");
                        }

                        me.GameCharacter.AddPsyche(2 * darksciUnstableMultiplier, "Повезло");
                        me.GameCharacter.AddMoral(2 * darksciUnstableMultiplier, "Повезло");
                        darscsi.Triggered = true;
                        game.Phrases.DarksciLucky.SendLog(me, true);
                    }

                    break;

                case "Спарта":
                    if (me.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                    {
                        var mylorikSpartan = me.Passives.MylorikSpartan;
                        var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                        if (mylorikEnemy == null)
                        {
                            mylorikSpartan.Enemies.Add(new Mylorik.MylorikSpartanSubClass(target.GetPlayerId()));
                            mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                        }

                        if (me.Status.WhoToAttackThisTurn.Contains(me.Status.IsLostThisCalculation))
                            mylorikEnemy!.LostTimes++;

                        if (me.Status.WhoToAttackThisTurn.Contains(me.Status.IsWonThisCalculation))
                            mylorikEnemy!.LostTimes = 0;
                    }

                    //Спарта reset FightMultiplier
                    me.FightCharacter.SetSkillFightMultiplier();
                    break;

                case "Я пытаюсь!":
                    //Я пытаюсь reset FightMultiplier
                    me.FightCharacter.SetSkillFightMultiplier();
                    break;

                case "На мели":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        // Bonus point for beating the skill-class target
                        if (me.GameCharacter.HasSkillTargetOn(target.GameCharacter))
                        {
                            me.Status.AddBonusPoints(1, "На мели");
                            game.Phrases.SaitamaBroke.SendLog(me, false);
                        }

                        // Extra point if nobody else attacked this target this round
                        var othersAttackedTarget = game.PlayersList.Any(p =>
                            p.GetPlayerId() != me.GetPlayerId() &&
                            p.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()));

                        if (!othersAttackedTarget)
                        {
                            me.Status.AddBonusPoints(1, "На мели");
                            if (game.RoundNo >= 10 && target.GameCharacter.Passive.Any(x => x.PassiveName == "Дракон"))
                                me.Status.AddInGamePersonalLogs("На мели (монстр): Уровень Опасности: Дракон\n");
                            else
                                game.Phrases.SaitamaBrokeMonster.SendLog(me, false);

                            // Hide this fight from non-admin players
                            me.Status.HideCurrentFight = true;
                        }
                    }
                    break;

                case "Неприметность":
                    // If Saitama won against someone who was also attacked by another player,
                    // defer his points and moral (they go into the "box")
                    if (game.RoundNo  >= 10) break;

                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        var saitamaAtkUnnoticedAfter = me.Passives.SaitamaUnnoticed;

                        // Check if another player also attacked this same target
                        var anotherAttacker = game.PlayersList.Any(p =>
                            p.GetPlayerId() != me.GetPlayerId() &&
                            p.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()));

                        if (anotherAttacker)
                        {
                            // Defer the win point (remove 1 from pending score)
                            me.Status.AddRegularPoints(-1, "Неприметность");
                            saitamaAtkUnnoticedAfter.DeferredPoints += 1;

                            // Defer moral too (underdog moral only applies when we had worse place)
                            var moralGain = me.Status.GetPlaceAtLeaderBoard() - target.Status.GetPlaceAtLeaderBoard();
                            if (moralGain > 0 && game.RoundNo > 1)
                            {
                                me.GameCharacter.AddMoral(-moralGain, "Неприметность", isFightMoral: true);
                                saitamaAtkUnnoticedAfter.DeferredMoral += moralGain;
                            }

                            game.Phrases.SaitamaUnnoticed.SendLog(me, false);
                        }
                    }
                    break;

                case "Гигантские бобы":
                    var beansAfter = me.Passives.RickGiantBeans;
                    if (beansAfter.IngredientsActive && beansAfter.IngredientTargets.Contains(target.GetPlayerId())
                        && me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        beansAfter.IngredientTargets.Remove(target.GetPlayerId());
                        beansAfter.BeanStacks++;
                        me.GameCharacter.AddStrength(-1, "Гигантские бобы");
                        me.GameCharacter.AddSpeed(-1, "Гигантские бобы");
                        me.MinusPsycheLog(me.GameCharacter, game, -1, "Гигантские бобы");
                        var oldFakeBeans = beansAfter.FakeIntelligence;
                        beansAfter.FakeIntelligence = beansAfter.BaseIntelligence * beansAfter.BeanStacks;
                        me.GameCharacter.AddIntelligence(beansAfter.FakeIntelligence - oldFakeBeans, "Гигантские бобы");
                        game.Phrases.RickGiantBeansDrink.SendLog(me, false);
                        // Portal gun invention is handled by HandleEndOfRound (not here)
                        // to prevent the gun from auto-firing on the same fight it was invented
                    }
                    break;

                case "Портальная пушка":
                    var gunAfter = me.Passives.RickPortalGun;
                    if (gunAfter.Invented && gunAfter.Charges > 0 && me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        gunAfter.Charges--;
                        gunAfter.SwapActive = true;
                        gunAfter.SwappedWith = target.GetPlayerId();
                        gunAfter.FiredThisRound = true;
                        foreach (var p in game.PlayersList)
                            for (int i = 0; i < p.Status.WhoToAttackThisTurn.Count; i++)
                            {
                                if (p.Status.WhoToAttackThisTurn[i] == me.GetPlayerId())
                                    p.Status.WhoToAttackThisTurn[i] = target.GetPlayerId();
                                else if (p.Status.WhoToAttackThisTurn[i] == target.GetPlayerId())
                                    p.Status.WhoToAttackThisTurn[i] = me.GetPlayerId();
                            }

                        // Most wanted: headhunters follow Rick through the portal
                        foreach (var hunter in game.PlayersList.Where(p =>
                            p.GetPlayerId() != me.GetPlayerId() &&
                            p.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()) &&
                            p.GameCharacter.Passive.Any(pas =>
                                pas.PassiveName is "Им это не понравится" or "Безжалостный охотник"
                                    or "Подсчет" or "Сверхразум" or "Заводить друзей")))
                        {
                            for (int i = 0; i < hunter.Status.WhoToAttackThisTurn.Count; i++)
                            {
                                if (hunter.Status.WhoToAttackThisTurn[i] == target.GetPlayerId())
                                    hunter.Status.WhoToAttackThisTurn[i] = me.GetPlayerId();
                            }
                            me.Status.AddInGamePersonalLogs($"**{hunter.DiscordUsername} проследовал за Риком в портал.**\n");
                            game.Phrases.RickMostWantedPortalFollow.SendLog(me, false);
                        }

                        game.Phrases.RickPortalGunFired.SendLog(me, false);
                    }
                    break;

                case "Пассивный импакт":
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                        me.Passives.DopaWonThisRound = true;
                    break;

                case "Доминация":
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        me.GameCharacter.AddExtraSkill(20, "Доминация");
                        target.Status.AddBonusPoints(-1, "Доминация");
                        if (_rand.Luck(1, 3))
                            target.MinusPsycheLog(target.GameCharacter, game, -1, "Доминация");
                        game.Phrases.DopaDomination.SendLog(me, false);
                    }
                    break;

                case "Роум":
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var myPlace = me.Status.GetPlaceAtLeaderBoard();
                        var targetPlace = target.Status.GetPlaceAtLeaderBoard();
                        if (Math.Abs(myPlace - targetPlace) > 1)
                        {
                            target.Status.AddBonusPoints(-1, "Роум");
                            me.Status.AddBonusPoints(1, "Роум");
                            target.GameCharacter.AddMoral(-3, "Роум");
                            me.GameCharacter.AddMoral(3, "Роум");
                            game.Phrases.DopaRoam.SendLog(me, false);
                        }
                    }
                    break;

                case "Парень с сюрпризом":
                    // Lost to higher-ranked enemy → mark as Хохол
                    if (me.Status.IsLostThisCalculation != Guid.Empty
                        && target.Status.GetPlaceAtLeaderBoard() < me.Status.GetPlaceAtLeaderBoard())
                    {
                        if (!me.Passives.SaldorumKhokholList.MarkedEnemies.Contains(target.GetPlayerId()))
                            me.Passives.SaldorumKhokholList.MarkedEnemies.Add(target.GetPlayerId());
                        me.GameCharacter.AddMoral(5, "Парень с сюрпризом");
                        if (_rand.Random(0, 99) < 33)
                            me.GameCharacter.AddMoral(5, "Парень с сюрпризом");
                        game.Phrases.SaldorumSurprise.SendLog(me, target, delete: true);
                    }
                    break;

                case "Сало":
                    {
                        var isKhokhol = me.Passives.SaldorumKhokholList.MarkedEnemies.Contains(target.GetPlayerId())
                                        || target.GameCharacter.Name is "mylorik" or "Sirinoks";
                        if (isKhokhol && me.Status.IsWonThisCalculation != Guid.Empty)
                        {
                            me.GameCharacter.Justice.AddJusticeForNextRoundFromSkill();
                            // Doubled moral: add extra equal to standard fight moral
                            var moral = Math.Abs(me.Status.GetPlaceAtLeaderBoard() - target.Status.GetPlaceAtLeaderBoard());
                            if (moral > 0 && me.Status.GetPlaceAtLeaderBoard() > target.Status.GetPlaceAtLeaderBoard())
                                me.GameCharacter.AddMoral(moral, "Сало");
                            game.Phrases.SaldorumSalo.SendLog(me, target, delete: true);
                        }
                        // +1 Moral when attacking mylorik (regardless of win/loss)
                        if (target.GameCharacter.Name == "mylorik")
                            me.GameCharacter.AddMoral(1, "Сало");
                    }
                    break;

                case "Ниндзя":
                    // Stealth kill bonus
                    if (me.Passives.SaldorumNinjaHidden && me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        me.GameCharacter.AddExtraSkill(5, "Ниндзя");
                        game.Phrases.SaldorumNinja.SendLog(me, target, delete: true);
                    }
                    me.Passives.SaldorumNinjaHidden = false;
                    break;

                // Napoleon — Завоеватель: bonus point for winning vs enemy between Napoleon and Ally
                case "Завоеватель":
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var napAllyConq = me.Passives.NapoleonAlliance;
                        if (napAllyConq.AllyId != Guid.Empty)
                        {
                            var allyConq = game.PlayersList.Find(x => x.GetPlayerId() == napAllyConq.AllyId);
                            if (allyConq != null)
                            {
                                var napPlace = me.Status.GetPlaceAtLeaderBoard();
                                var allyPlace = allyConq.Status.GetPlaceAtLeaderBoard();
                                var enemyPlace = target.Status.GetPlaceAtLeaderBoard();
                                var minPlace = Math.Min(napPlace, allyPlace);
                                var maxPlace = Math.Max(napPlace, allyPlace);
                                if (enemyPlace > minPlace && enemyPlace < maxPlace)
                                {
                                    me.Status.AddBonusPoints(1, "Завоеватель");
                                    game.Phrases.NapoleonConqueror.SendLog(me, false);
                                }
                            }
                        }
                    }
                    break;

                // Salldorum — Великий летописец: tripled Skill vs player who won most 3 rounds ago
                case "Великий летописец":
                    if (me.GameCharacter.Name == "Salldorum" && game.RoundNo > 3)
                    {
                        var chroniclerRound = game.RoundNo - 3;
                        // WhoToLostEveryRound is on each player, recording WHO they lost to (EnemyId)
                        // Count how often each player appears as EnemyId in that round = their wins
                        var winCountsChron = new Dictionary<Guid, int>();
                        foreach (var p in game.PlayersList)
                        {
                            foreach (var loss in p.Status.WhoToLostEveryRound.Where(x => x.RoundNo == chroniclerRound))
                            {
                                if (!winCountsChron.ContainsKey(loss.EnemyId))
                                    winCountsChron[loss.EnemyId] = 0;
                                winCountsChron[loss.EnemyId]++;
                            }
                        }

                        if (winCountsChron.Count > 0)
                        {
                            var maxWins = winCountsChron.Values.Max();
                            var topWinners = winCountsChron.Where(x => x.Value == maxWins).Select(x => x.Key).ToList();
                            if (topWinners.Contains(target.GetPlayerId()))
                            {
                                me.FightCharacter.SetSkillFightMultiplier(3);
                                game.Phrases.SalldorumChroniclerTriple.SendLog(me, false);
                            }
                        }
                    }
                    break;

                case "Гоблины":
                    var gobAtkAfterPop = me.Passives.GoblinPopulation;
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        // Win: x2 growth, +1 vs tooGood, +2 vs tooStronk
                        var growth = 2 * gobAtkAfterPop.GrowthThisRound;
                        if (me.Status.FightEnemyWasTooGood) growth += 1;
                        if (me.Status.FightEnemyWasTooStronk) growth += 2;
                        gobAtkAfterPop.TotalGoblins += growth;
                        game.Phrases.GoblinGrowthAttack.SendLog(me, false);
                        me.Status.AddInGamePersonalLogs($"Гоблины: +{growth} гоблинов! Всего: {gobAtkAfterPop.TotalGoblins}\n");
                    }
                    else if (me.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        // Loss: kill goblins (percentage-based)
                        var atkDeathPct = 10 + 0.5*game.RoundNo*game.RoundNo/3;;
                        if (me.Status.FightEnemyWasTooGood) atkDeathPct += 5;
                        if (me.Status.FightEnemyWasTooStronk) atkDeathPct += 5;
                        var atkDeathCount = Math.Max(1, (int)Math.Ceiling(gobAtkAfterPop.TotalGoblins * atkDeathPct / 100.0));
                        gobAtkAfterPop.TotalGoblins = Math.Max(1, gobAtkAfterPop.TotalGoblins - atkDeathCount);
                        game.Phrases.GoblinDeath.SendLog(me, false);
                        me.Status.AddInGamePersonalLogs($"Гоблины: -{atkDeathCount} ({atkDeathPct}%). Осталось: {gobAtkAfterPop.TotalGoblins}\n");
                    }
                    break;

                // Геральт — Ведьмачий Заказ (attack after fight): contract win/loss
                case "Ведьмачий Заказ":
                    if (me.GameCharacter.Name == "Геральт")
                    {
                        var geraltAtkAfter = me.Passives.GeraltContracts;
                        var targetId = target.GetPlayerId();
                        if (geraltAtkAfter.ContractMap.ContainsKey(targetId) && geraltAtkAfter.ContractMap[targetId].Count > 0)
                        {
                            if (me.Status.IsWonThisCalculation == targetId)
                            {
                                game.Phrases.GeraltContractWin.SendLog(me, false);
                            }
                            else if (me.Status.IsLostThisCalculation == targetId)
                            {
                                // Remove one contract on loss
                                var removedContract = geraltAtkAfter.ContractMap[targetId][0];
                                geraltAtkAfter.ContractMap[targetId].RemoveAt(0);
                                if (target.Passives.GeraltContractsOnMe.Contains(removedContract))
                                    target.Passives.GeraltContractsOnMe.Remove(removedContract);
                                if (geraltAtkAfter.ContractMap[targetId].Count == 0)
                                {
                                    geraltAtkAfter.ContractMap.Remove(targetId);
                                    target.Passives.GeraltContractsOnMe.Clear();
                                    target.Passives.GeraltContractOwnerId = Guid.Empty;
                                }
                                game.Phrases.GeraltContractLost.SendLog(me, false);
                            }
                        }
                    }
                    break;

                // Геральт — Ведьмачое Масло (attack after fight): undo oil buff using LastOilBonusCount
                case "Ведьмачое Масло":
                    if (me.GameCharacter.Name == "Геральт")
                    {
                        var geraltOilAfter = me.Passives.GeraltContracts;
                        if (geraltOilAfter.LastOilBonusCount > 0)
                        {
                            me.FightCharacter.AddExtraSkill(-100 * geraltOilAfter.LastOilBonusCount, "Ведьмачое Масло", false);
                            me.FightCharacter.SetSkillFightMultiplier(
                                (int)Math.Max(0, me.FightCharacter.GetSkillFightMultiplier() - geraltOilAfter.LastOilBonusCount));
                            geraltOilAfter.LastOilBonusCount = 0;
                        }
                    }
                    break;
            }
    }


    public async Task HandleCharacterAfterFight(GamePlayerBridgeClass player, GameClass game, bool attack, bool defense)
    {
        foreach (var p in game.PlayersList)
        foreach (var passive in p.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Подсчет":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var tolyaCount = p.Passives.TolyaCount;

                        if (tolyaCount.TargetList.Any(x =>
                                x.RoundNumber == game.RoundNo - 1 && x.Target == player.GetPlayerId()))
                        {
                            p.Status.AddRegularPoints(2, "Подсчет");
                            p.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(2);
                            game.Phrases.TolyaCountPhrase.SendLog(p, false);
                        }
                    }

                    break;

                case "Впарить говна":
                    if (p.GetPlayerId() != player.GetPlayerId())
                    {
                        // +1 bonus for wins over outplay-marked enemies only
                        if (player.Passives.SellerVparitGovnaRoundsLeft > 0 &&
                            player.Passives.SellerOutplayTargets.Count > 0 &&
                            player.Passives.SellerOutplayTargets.Contains(player.Status.IsWonThisCalculation))
                        {
                            player.Status.AddBonusPoints(1, "Впарить говна");
                            player.Passives.SellerTacticBonusEarned++;
                        }
                    }
                    break;

                case "Выгодная сделка":
                    // p = seller (has this passive), player = fight participant
                    if (p.GetPlayerId() != player.GetPlayerId() &&
                        player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        bool isMarked = player.Passives.SellerVparitGovnaRoundsLeft > 0;
                        bool hasTactic = player.GameCharacter.Passive.Any(x => x.PassiveName == "Сомнительная тактика");
                        if (isMarked || hasTactic)
                        {
                            p.Passives.SellerProfitableDealsThisRound++;
                            p.GameCharacter.AddMoral(5, "Выгодная сделка");
                        }
                    }
                    break;

                case "Большой куш":
                    // p = seller, player = fight participant who attacked seller and won
                    if (attack && player.Status.IsWonThisCalculation == p.GetPlayerId())
                    {
                        if (_rand.Luck(1, 10))
                        {
                            player.Status.AddBonusPoints(2, "Большой куш");
                            p.Status.AddBonusPoints(-2, "Большой куш");
                            game.Phrases.SellerBolshoiKushEnemy.SendLog(player, false);
                        }
                    }
                    break;

                // Таинственный Суппорт — "Premade": gain/lose points based on marked player's fights
                case "Premade":
                    if (p.Passives.SupportPremade.MarkedPlayerId != Guid.Empty &&
                        player.GetPlayerId() == p.Passives.SupportPremade.MarkedPlayerId)
                    {
                        if (player.Status.IsWonThisCalculation != Guid.Empty)
                            p.Status.AddRegularPoints(1, "Premade");
                        if (player.Status.IsLostThisCalculation != Guid.Empty)
                            p.Status.AddRegularPoints(-1, "Premade");

                        // Transfer carry's fight moral to support
                        var carryMoral = player.Status.MoralGainedThisFight;
                        if (carryMoral != 0)
                            p.GameCharacter.AddMoral(carryMoral, "Premade", isFightMoral: true);
                    }
                    break;

                // Toxic Mate — "Get cancer": transfer cancer when holder wins a fight
                case "Get cancer":
                    // p = Toxic Mate (cancer owner), player = fight participant
                    var cancerAll = p.Passives.ToxicMateCancer;
                    if (cancerAll.IsActive && !cancerAll.TransferredThisRound && attack && player.Status.IsWonThisCalculation != Guid.Empty
                        && player.Passives.HasToxicMateCancer && player.Passives.ToxicMateCancerSourceId == p.GetPlayerId())
                    {
                        var cancerTarget = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        if (cancerTarget != null)
                        {
                            // Remove cancer from current holder
                            player.Passives.HasToxicMateCancer = false;
                            player.Passives.ToxicMateCancerSourceId = Guid.Empty;
                            player.GameCharacter.BlockMoralGain = false;
                            cancerAll.TransferCount++;
                            cancerAll.TransferredThisRound = true;

                            if (cancerTarget.GetPlayerId() == p.GetPlayerId())
                            {
                                // Cancer returned to Toxic Mate — award bonus points and deactivate
                                var cancerBonus = cancerAll.TransferCount * 2;
                                p.Status.AddBonusPoints(cancerBonus, "Get cancer");
                                cancerAll.IsActive = false;
                                cancerAll.CurrentHolder = Guid.Empty;
                                game.Phrases.ToxicMateCancerReturn.SendLog(p, false);
                            }
                            else
                            {
                                // Transfer cancer to new victim
                                cancerTarget.Passives.HasToxicMateCancer = true;
                                cancerTarget.Passives.ToxicMateCancerSourceId = p.GetPlayerId();
                                cancerTarget.GameCharacter.BlockMoralGain = true;
                                cancerAll.CurrentHolder = cancerTarget.GetPlayerId();

                                var infectPhrases = game.Phrases.ToxicMateCancerInfect.PassiveLogRus;
                                var infectPhrase = infectPhrases[_rand.Random(0, infectPhrases.Count - 1)];
                                infectPhrase = infectPhrase.Replace("{name}", cancerTarget.DiscordUsername);
                                game.AddGlobalLogs($"Get cancer: {infectPhrase}");
                            }
                        }
                    }
                    break;
            }

        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Возвращение из мертвых":
                    //failed
                    if (game.RoundNo > 10 && game.IsKratosEvent && player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        player.Passives.IsDead = true;
                        player.Passives.DeathSource = "Kratos";
                        player.Passives.AchievementTracker.WasKilledByKratos = true;
                        // Track Kratos kills for the Kratos player
                        var kratosPlayer = game.PlayersList.Find(x => x.GameCharacter.Name == "Кратос");
                        if (kratosPlayer != null) kratosPlayer.Passives.AchievementTracker.EnemiesKilledAsKratos++;
                        // Монстр без имени: +1 regular point per death
                        foreach (var mp in game.PlayersList.Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Монстр")))
                        {
                            mp.Status.AddRegularPoints(1, "Монстр");
                            game.Phrases.MonsterDeath.SendLog(mp, false);
                        }
                    }

                    //start
                    else if (!game.IsKratosEvent && game.RoundNo == 10 && player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        game.IsKratosEvent = true;
                        game.AddGlobalLogs("Бегите! На Гору Мусорной Горы идёт Кратос и НИЧТО его не остановит!");
                        foreach (var p in game.PlayersList.Where(x => !x.IsBot()))
                            await game.Phrases.KratosEventYes.SendLogSeparateWithFile(p, false, "DataBase/sound/Kratos.mp3", false, 15000, roundsToPlay: 5);

                        player.GameCharacter.SetClassSkillMultiplier(4);
                    }

                    break;

                case "Охота на богов":
                    player.FightCharacter.SetSkillFightMultiplier();
                    break;

                case "Панцирь":
                    var сraboRackShell = player.Passives.CraboRackShell;
                    if (сraboRackShell != null)
                        if (сraboRackShell.CurrentAttacker != Guid.Empty)
                        {
                            сraboRackShell.CurrentAttacker = Guid.Empty;
                            player.Status.IsBlock = false;
                        }

                    break;

                case "Сомнительная тактика":
                    var deep = player.Passives.DeepListDoubtfulTactic;


                    if (!deep.FriendList.Contains(player.Status.IsFighting) &&
                        player.Status.IsLostThisCalculation == player.Status.IsFighting)
                    {
                        deep.FriendList.Add(player.Status.IsFighting);
                        game.Phrases.DeepListDoubtfulTacticFirstLostPhrase.SendLog(player, false);
                    }

                    if (deep.FriendList.Contains(player.Status.IsFighting))
                        if (player.Status.IsWonThisCalculation != Guid.Empty)
                        {
                            player.Status.AddRegularPoints(1, "Сомнительная тактика");
                            //me.Status.AddBonusPoints(1, "Сомнительная тактика");
                            game.Phrases.DeepListDoubtfulTacticPhrase.SendLog(player, false);
                        }

                    break;

                case "Стёб":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var target = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        //Стёб
                        var currentDeepList = player.Passives.DeepListMockeryList;

                        var currentDeepList2 =
                            currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == target!.GetPlayerId());

                        if (currentDeepList2 != null)
                        {
                            currentDeepList2.Times++;

                            if (currentDeepList2.Times == 2 && !currentDeepList2.Triggered)
                            {
                                currentDeepList2.Triggered = true;

                                var howMuchToAdd = -1;

                                if (target!.GameCharacter.Name == "Злой Школьник")
                                {
                                    howMuchToAdd = -2;
                                    target.Status.AddInGamePersonalLogs(
                                        "MitSUKI: __Да сука, я щас ливну, заебали токсики!__\nDeepList: *хохочет*\n");
                                }

                                if (target.GameCharacter.Name != "LeCrisp")
                                {
                                    target.MinusPsycheLog(target.GameCharacter, game, howMuchToAdd, "Стёб");
                                }


                                player.Status.AddRegularPoints(1, "Стёб");
                                game.Phrases.DeepListPokePhrase.SendLog(player, true);

                                // БОЛЬШЕ МОЛОКА ДЛЯ ХАРДКИТТИ!
                                if (target!.GameCharacter.Name == "HardKitty")
                                    game.Phrases.DeepListMockeryHardKittyMilk.SendLog(player, false);

                                if (target.GameCharacter.GetPsyche() < 4)
                                    if (target.GameCharacter.Justice.GetRealJusticeNow() > 0)
                                        if (target.GameCharacter.Name != "LeCrisp")
                                            target.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(-1);
                            }
                        }
                        else
                        {
                            currentDeepList.WhoWonTimes.Add(new DeepList.MockerySub(target!.GetPlayerId(), 1));
                        }


                        //end Стёб
                    }

                    break;

                case "Месть":
                    //enemyIdLostTo may be 0
                    var mylorik = player.Passives.MylorikRevenge;

                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        //check if very first lost
                        if (mylorik.EnemyListPlayerIds.All(x => x.EnemyPlayerId != player.Status.IsLostThisCalculation))
                        {
                            mylorik.EnemyListPlayerIds.Add(
                                new Mylorik.MylorikRevengeClassSub(player.Status.IsLostThisCalculation, game.RoundNo));
                            game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                        }
                    }
                    else
                    {
                        var find = mylorik?.EnemyListPlayerIds.Find(x =>
                            x.EnemyPlayerId == player.Status.IsWonThisCalculation && x.IsUnique);

                        if (find != null && find.RoundNumber != game.RoundNo)
                        {
                            player.Status.AddRegularPoints(2, "Месть");
                            player.GameCharacter.AddMoral(3, "Месть");
                            player.GameCharacter.AddPsyche(1, "Месть");
                            find.IsUnique = false;
                            game.Phrases.MylorikRevengeVictoryPhrase.SendLog(player, true);
                        }
                    }

                    break;

                case "Испанец":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var boole = player.Passives.MylorikSpanish;

                        if (_rand.Luck(1, 2))
                        {
                            boole.Times = 0;
                            player.GameCharacter.AddExtraSkill(10, "Испанец");
                            player.MinusPsycheLog(player.GameCharacter, game, -1, "Испанец");
                            game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                        }
                        else
                        {
                            boole.Times++;

                            if (boole.Times == 2)
                            {
                                boole.Times = 0;
                                player.GameCharacter.AddExtraSkill(10, "Испанец");
                                player.MinusPsycheLog(player.GameCharacter, game, -1, "Испанец");
                                game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                            }
                        }
                    }

                    break;

                case "Спящее хуйло":
                    if (player.Passives.GlebSkip && player.Status.WhoToAttackThisTurn.Count != 0)
                    {
                        player.Status.IsSkip = false;
                        player.Passives.GlebSkip = false;

                        // 33% chance "POSTAV ROLI" when waking up and NOT in Challenger mode
                        var glebChallenger = player.Passives.GlebChallengerTriggeredWhen;
                        if (!glebChallenger.WhenToTrigger.Contains(game.RoundNo) && _rand.Luck(1, 3))
                        {
                            game.Phrases.GlebWakeUpRoli.SendLog(player, false);
                        }
                    }

                    break;

                case "Импакт":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var lePuska = player.Passives.LeCrispImpact;


                        player.GameCharacter.AddMoral(lePuska.ImpactTimes + 1, "Импакт");
                    }

                    break;


                case "Доебаться":
                    var hardKitty = player.Passives.HardKittyDoebatsya;

                    if (player.Status.WhoToAttackThisTurn.Count != 0 && attack)
                        if (player.Status.WhoToAttackThisTurn.Contains(player.Status.IsLostThisCalculation) ||
                            player.Status.WhoToAttackThisTurn.Contains(player.Status.IsTargetBlocked) ||
                            player.Status.WhoToAttackThisTurn.Contains(player.Status.IsTargetSkipped))
                        {
                            var found = hardKitty.LostSeriesCurrent.Find(x =>
                                player.Status.WhoToAttackThisTurn.Contains(x.EnemyPlayerId));

                            if (found != null)
                                found.Series++;
                            else
                                hardKitty.LostSeriesCurrent.Add(
                                    new HardKitty.DoebatsyaSubClass(player.Status.WhoToAttackThisTurn[0]));
                        }

                    if (player.Status.IsWonThisCalculation != Guid.Empty &&
                        player.Status.WhoToAttackThisTurn.Contains(player.Status.IsWonThisCalculation) && attack)
                    {
                        var found = hardKitty.LostSeriesCurrent.Find(x =>
                            player.Status.WhoToAttackThisTurn.Contains(x.EnemyPlayerId));
                        if (found is { Series: > 0 })
                        {
                            if (found.Series >= 7) 
                                found.Series += 10;

                            player.Status.AddRegularPoints(found.Series * 2, "Доебаться");

                            if (found.Series >= 7)
                                game.Phrases.HardKittyDoebatsyaLovePhrase.SendLog(player, false);
                            else
                                game.Phrases.HardKittyDoebatsyaPhrase.SendLog(player, false);
                            found.Series = 0;
                        }
                    }

                    break;

                case "Произошел троллинг":
                    if (player.Status.IsWonThisCalculation != Guid.Empty &&
                        player.Status.WhoToAttackThisTurn.Contains(player.Status.IsWonThisCalculation))
                    {
                        var awdka = player.Passives.AwdkaTrollingList;

                        var enemy = awdka.EnemyList.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);

                        if (enemy == null)
                            awdka.EnemyList.Add(new Awdka.TrollingSubClass(player.Status.IsWonThisCalculation,
                                game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)!
                                    .Status
                                    .GetScore()));
                        else
                            enemy.Score =
                                game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)!
                                    .Status.GetScore();
                    }

                    break;

                case "Я пытаюсь!":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var awdka = player.Passives.AwdkaTryingList;


                        var enemy = awdka.TryingList.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);
                        if (enemy == null)
                            awdka.TryingList.Add(new Awdka.TryingSubClass(player.Status.IsLostThisCalculation));
                        else
                            enemy.Times++;
                    }

                    break;

                case "Привет со дна":

                    /*//привет со дна
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var moral = me.Status.GetPlaceAtLeaderBoard() - game.PlayersList
                            .Find(x => x.GetPlayerId() == me.Status.IsWonThisCalculation).Status.GetPlaceAtLeaderBoard();
                        if (moral > 0)
                            me.FightCharacter.AddMoral(moral, "Привет со дна");
                    }
                    //end привет со дна*/

                    break;

                case "Не повезло":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        //LOL GOD, EXAMPLE:
                        /*
                        if (game.PlayersList.All(x => x.GameCharacter.Name != "Бог ЛоЛа") || _gameGlobal.LolGodUdyrList.Any(
                                x =>
                                    x.GameId == game.GameId && x.EnemyDiscordId == me.GetPlayerId()))
                        {
                            me.FightCharacter.AddPsyche(-1);
                            me.MinusPsycheLog(game);
                            game.Phrases.DarksciNotLucky.SendLog(me);
                        }
                        else
                            game.Phrases.ThirdСommandment.SendLog(me);*/
                        player.MinusPsycheLog(player.GameCharacter, game, -1, "Не повезло");
                        game.Phrases.DarksciNotLucky.SendLog(player, false);
                    }

                    break;

                case "3-0 обоссан":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var tigr = player.Passives.TigrThreeZeroList;
                        tigr.WhoToWinThisRound.Add(player.Status.IsWonThisCalculation);
                    }

                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var tigr = player.Passives.TigrThreeZeroList;
                        tigr.WhoToLostThisRound.Add(player.Status.IsLostThisCalculation);
                    }
                    break;

                case "Челюсти":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var shark = player.Passives.SharkJawsWin;


                        if (!shark.FriendList.Contains(player.Status.IsWonThisCalculation))
                        {
                            shark.FriendList.Add(player.Status.IsWonThisCalculation);
                            player.GameCharacter.AddSpeed(1, "Челюсти");
                        }
                    }

                    break;

                case "Первая кровь":
                    var spartan = player.Passives.SpartanFirstBlood;

                    if (spartan.FriendList.Count == 1)
                    {
                        if (spartan.FriendList.Contains(player.Status.IsWonThisCalculation))
                        {
                            player.GameCharacter.AddSpeed(1, "Первая кровь");
                            game.Phrases.SpartanFirstBlood.SendLog(player, false);
                            game.AddGlobalLogs("Они познают войну!");
                        }
                        else if (spartan.FriendList.Contains(player.Status.IsLostThisCalculation))
                        {
                            var ene = game.PlayersList.Find(x =>
                                x.GetPlayerId() == player.Status.IsLostThisCalculation);
                            ene!.GameCharacter.AddSpeed(1, "Первая кровь");
                        }

                        spartan.FriendList.Add(Guid.Empty);
                    }

                    break;

                case "Это привилегия - умереть от моей руки":
                    if (player.Status.IsWonThisCalculation != Guid.Empty && game.RoundNo > 4)
                    {
                        game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)!.GameCharacter
                            .Justice.AddJusticeForNextRoundFromSkill();
                        player.GameCharacter.AddIntelligence(-1, "Это привилегия");
                    }

                    break;

                case "Им это не понравится":
                    var spartanTheyWontLikeIt = player.Passives.SpartanMark;

                    if (spartanTheyWontLikeIt.FriendList.Contains(player.Status.IsWonThisCalculation))
                    {
                        player.Status.AddRegularPoints(1, "Им это не понравится");
                        player.Status.AddBonusPoints(1, "Им это не понравится");
                    }

                    break;

                case "Гематофагия":
                    var vampyr = player.Passives.VampyrHematophagiaList;

                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var target = vampyr.HematophagiaCurrent.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);
                        var repeat = vampyr.HematophagiaAddEndofRound.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);

                        if (target == null && repeat == null)
                        {
                            var statIndex = 0;
                            var found = false;
                            var tries = 0;
                            while (!found)
                            {
                                tries++;
                                if (tries > 20)
                                {
                                    break;
                                }

                                statIndex = _rand.Random(1, 4);

                                //поскольку мы вернули вампуру прокачку, надо добавить в условие на психику, что оно работает только когда психика <=8
                                if (player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Count < 4 && player.GameCharacter.GetPsyche() <= 8)
                                {
                                    if (!player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Any(x => x.StatIndex == 4))
                                    {
                                        statIndex = _rand.Random(4, 4);
                                    }
                                }

                                //поскольку мы вернули вампуру прокачку, надо добавить в условие на психику, что оно работает только когда психика <=8
                                if (player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Count < 5 && player.GameCharacter.GetPsyche() <= 8)
                                {
                                    if (player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Count(x => x.StatIndex == 4) < 2)
                                    {
                                        statIndex = _rand.Random(4, 4);
                                    }
                                }

                                switch (statIndex)
                                {
                                    case 1:
                                        if (player.GameCharacter.GetIntelligence() < 10) found = true;
                                        break;
                                    case 2:
                                        if (player.GameCharacter.GetStrength() < 10) found = true;

                                        break;
                                    case 3:
                                        if (player.GameCharacter.GetSpeed() < 10) found = true;

                                        break;
                                    case 4:
                                        if (player.GameCharacter.GetPsyche() < 10) found = true;

                                        break;
                                }
                            }
                            vampyr.HematophagiaAddEndofRound.Add(new Vampyr.HematophagiaSubClass(statIndex, player.Status.IsWonThisCalculation));
                        }
                    }

                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var target = vampyr.HematophagiaCurrent.Find(x => x.EnemyId == player.Status.IsLostThisCalculation);

                        if (target != null)
                        {
                            if (vampyr.HematophagiaRemoveEndofRound.All(x => x.EnemyId != player.Status.IsLostThisCalculation)) 
                                vampyr.HematophagiaRemoveEndofRound.Add(target);
                        }
                        else
                        {
                            if (vampyr.HematophagiaCurrent.Count > 0)
                            {
                                var randomIndex = _rand.Random(0, vampyr.HematophagiaCurrent.Count - 1);
                                target = vampyr.HematophagiaCurrent[randomIndex];
                                if (vampyr.HematophagiaRemoveEndofRound.All(x => x.EnemyId != player.Status.IsLostThisCalculation))
                                    vampyr.HematophagiaRemoveEndofRound.Add(target);
                            }
                        }
                    }

                    break;

                case "Огурчик Рик":
                    var pickleAfterFight = player.Passives.RickPickle;
                    if (pickleAfterFight.PickleTurnsRemaining > 0 && player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        game.Phrases.RickPickleWin.SendLog(player, false);
                    }
                    break;

                // Вороны: place crow on enemy on win (attack only)
                case "Вороны":
                    if (attack && player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var crowsAfter = player.Passives.ItachiCrows;
                        var crowTargetId = player.Status.IsWonThisCalculation;
                        if (!crowsAfter.CrowCounts.ContainsKey(crowTargetId))
                            crowsAfter.CrowCounts[crowTargetId] = 0;
                        crowsAfter.CrowCounts[crowTargetId]++;
                        game.Phrases.ItachiCrows.SendLog(player, false);
                    }
                    break;

                // Глаза Итачи: activate Tsukuyomi if charged (attack only, win or loss)
                case "Глаза Итачи":
                    if (attack && player.Passives.ItachiTsukuyomi.ChargeCounter >= 2)
                    {
                        var tsukuyomiTarget = player.Status.IsWonThisCalculation != Guid.Empty
                            ? player.Status.IsWonThisCalculation
                            : player.Status.IsLostThisCalculation;
                        if (tsukuyomiTarget != Guid.Empty)
                        {
                            player.Passives.ItachiTsukuyomi.TsukuyomiTargetThisRound = tsukuyomiTarget;
                            player.Passives.ItachiTsukuyomi.ChargeCounter = -1; // recharges over 2 rounds
                            game.Phrases.ItachiTsukuyomiActivate.SendLog(player, false);
                        }
                    }
                    break;

                // Таинственный Суппорт — "Stakes!": bonus point every 3rd round on non-marked win
                case "Stakes!":
                    if (game.RoundNo % 3 == 0 && attack &&
                        player.Status.IsWonThisCalculation != Guid.Empty &&
                        player.Status.IsWonThisCalculation != player.Passives.SupportPremade.MarkedPlayerId)
                    {
                        player.Status.AddRegularPoints(1, "Stakes!");
                        game.Phrases.SupportStakes.SendLog(player, false);
                    }
                    break;

                // Toxic Mate — "INT": +1 point on loss, first loss global log
                case "INT":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        player.Status.AddRegularPoints(1, "INT");
                        var cancerInt = player.Passives.ToxicMateCancer;
                        if (!cancerInt.FirstLossTriggered)
                        {
                            cancerInt.FirstLossTriggered = true;
                            game.AddGlobalLogs("**Ok. I'm trolling.**");
                        }
                    }
                    break;

                // Toxic Mate — "Get cancer": infect target on first win (cancer not yet active)
                case "Get cancer":
                    var cancerOwn = player.Passives.ToxicMateCancer;
                    if (attack && player.Status.IsWonThisCalculation != Guid.Empty && !cancerOwn.IsActive)
                    {
                        var cancerVictim = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        if (cancerVictim != null)
                        {
                            cancerOwn.IsActive = true;
                            cancerOwn.CurrentHolder = cancerVictim.GetPlayerId();
                            cancerOwn.TransferCount = 0;
                            cancerVictim.Passives.HasToxicMateCancer = true;
                            cancerVictim.Passives.ToxicMateCancerSourceId = player.GetPlayerId();
                            cancerVictim.GameCharacter.BlockMoralGain = true;

                            var infectMsgs = game.Phrases.ToxicMateCancerInfect.PassiveLogRus;
                            var infectMsg = infectMsgs[_rand.Random(0, infectMsgs.Count - 1)];
                            infectMsg = infectMsg.Replace("{name}", cancerVictim.DiscordUsername);
                            game.AddGlobalLogs($"Get cancer: {infectMsg}");
                        }
                    }
                    break;

                // Toxic Mate — "Aggress": +1 point if attack didn't result in a fight (target blocked/skipped)
                case "Aggress":
                    if (attack && player.Status.IsWonThisCalculation == Guid.Empty && player.Status.IsLostThisCalculation == Guid.Empty)
                    {
                        player.Status.AddRegularPoints(1, "Aggress");
                        player.GameCharacter.Justice.SetRealJusticeNow(0, "Aggress");
                        game.Phrases.ToxicMateAggressPoint.SendLog(player, false);
                    }
                    break;

                // Котики — Минька: always gain Moral (+1) and Skill (+10) from any fight
                case "Минька":
                    if (player.Status.IsWonThisCalculation != Guid.Empty || player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        // Immunity: if this is a transferred cat passive and the fight is against the cat owner, skip buff
                        var minkaFightTarget = player.Status.IsWonThisCalculation != Guid.Empty
                            ? player.Status.IsWonThisCalculation
                            : player.Status.IsLostThisCalculation;
                        var isTransferredMinkaVsOwner = player.Passives.KotikiCatOwnerId != Guid.Empty
                            && minkaFightTarget == player.Passives.KotikiCatOwnerId;

                        if (!isTransferredMinkaVsOwner)
                        {
                            player.GameCharacter.AddMoral(1, "Минька");
                            player.GameCharacter.AddExtraSkill(10, "Минька");
                            game.Phrases.KotikiMinka.SendLog(player, false);
                        }
                    }
                    break;

                // Котики — Штормяк: if taunted enemy lost to Котики → -1 Psyche, rage, give top stat
                case "Штормяк":
                    var stormAfterFight = player.Passives.KotikiStorm;
                    if (stormAfterFight.CurrentTauntTarget != Guid.Empty
                        && player.Status.IsWonThisCalculation == stormAfterFight.CurrentTauntTarget)
                    {
                        var tauntLoser = game.PlayersList.Find(x => x.GetPlayerId() == stormAfterFight.CurrentTauntTarget);
                        if (tauntLoser != null)
                        {
                            // -1 Psyche + rage log
                            tauntLoser.MinusPsycheLog(tauntLoser.GameCharacter, game, -1, "Штормяк");
                            game.Phrases.KotikiStormWin.SendLog(player, false);

                            // Give top stat: enemy's highest stat -1, Котики +1 same stat
                            var statValues = new[]
                            {
                                ("Int", tauntLoser.GameCharacter.GetIntelligence()),
                                ("Str", tauntLoser.GameCharacter.GetStrength()),
                                ("Spd", tauntLoser.GameCharacter.GetSpeed()),
                                ("Psy", tauntLoser.GameCharacter.GetPsyche())
                            };
                            var topStat = statValues.OrderByDescending(s => s.Item2).First();
                            switch (topStat.Item1)
                            {
                                case "Int":
                                    tauntLoser.GameCharacter.AddIntelligence(-1, "Штормяк");
                                    player.GameCharacter.AddIntelligence(1, "Штормяк");
                                    break;
                                case "Str":
                                    tauntLoser.GameCharacter.AddStrength(-1, "Штормяк");
                                    player.GameCharacter.AddStrength(1, "Штормяк");
                                    break;
                                case "Spd":
                                    tauntLoser.GameCharacter.AddSpeed(-1, "Штормяк");
                                    player.GameCharacter.AddSpeed(1, "Штормяк");
                                    break;
                                case "Psy":
                                    tauntLoser.MinusPsycheLog(tauntLoser.GameCharacter, game, -1, "Штормяк");
                                    player.GameCharacter.AddPsyche(1, "Штормяк");
                                    break;
                            }
                        }
                    }
                    break;

                // Котики — Кошачья засада: cat deploy/return after fight
                case "Кошачья засада":
                    var fightEnemyId = player.Status.IsWonThisCalculation != Guid.Empty
                        ? player.Status.IsWonThisCalculation
                        : player.Status.IsLostThisCalculation;

                    if (fightEnemyId != Guid.Empty)
                    {
                        var ambush = player.Passives.KotikiAmbush;
                        var fightEnemy = game.PlayersList.Find(x => x.GetPlayerId() == fightEnemyId);

                        if (fightEnemy != null)
                        {
                            // Cat return: only when we attack the enemy who has our cat
                            if (attack && fightEnemy.Passives.KotikiCatOwnerId == player.GetPlayerId())
                            {
                                var catType = fightEnemy.Passives.KotikiCatType;
                                game.Phrases.KotikiCatReturn.SendLog(player, false);
                                var isVictory = player.Status.IsWonThisCalculation == fightEnemyId;

                                if (catType == "Минька")
                                {
                                    if (isVictory)
                                    {
                                        var roundsOnEnemy = ambush.MinkaRoundsOnEnemy;
                                        player.Status.AddBonusPoints(2, "Кошачья засада");
                                        player.GameCharacter.AddExtraSkill(33 * roundsOnEnemy, "Кошачья засада (Минька)");
                                    }
                                    ambush.MinkaOnPlayer = Guid.Empty;
                                    ambush.MinkaRoundsOnEnemy = 0;
                                    ambush.MinkaCooldown = 2;
                                }
                                else if (catType == "Штормяк")
                                {
                                    if (isVictory)
                                    {
                                        var enemyScore = fightEnemy.Status.GetScore();
                                        var stolenPoints = Math.Floor(enemyScore / 2);
                                        if (stolenPoints > 0)
                                        {
                                            fightEnemy.Status.AddBonusPoints(-stolenPoints, "Кошачья засада");
                                            player.Status.AddBonusPoints(stolenPoints, "Кошачья засада (Штормяк)");
                                        }
                                        fightEnemy.MinusPsycheLog(fightEnemy.GameCharacter, game, -1, "Кошачья засада");
                                    }
                                    ambush.StormOnPlayer = Guid.Empty;
                                    ambush.StormCooldown = 2;
                                }

                                // Remove cat passive from enemy, restore to owner
                                fightEnemy.GameCharacter.Passive.RemoveAll(x => x.PassiveName == catType
                                    && fightEnemy.Passives.KotikiCatOwnerId == player.GetPlayerId());
                                fightEnemy.Passives.KotikiCatType = "";
                                fightEnemy.Passives.KotikiCatOwnerId = Guid.Empty;

                                // Restore passive to Котики
                                if (!player.GameCharacter.Passive.Any(x => x.PassiveName == catType))
                                {
                                    var desc = catType == "Минька"
                                        ? "Котики всегда получают Мораль и Скилл от боёв. Никогда не наносят вреда."
                                        : "Блок = провокация случайного врага. Враг атакует Котиков вторым действием. Если проигрывает: -1 Психика, даёт топ стат.";
                                    player.GameCharacter.Passive.Add(new Passive(catType, desc, true));
                                }
                            }
                            // Cat deploy: only on attack, 100% deploy, 50% cat choice, only 1 cat out at a time
                            else if (attack && fightEnemy.Passives.KotikiCatOwnerId == Guid.Empty)
                            {
                                // Only 1 cat deployed at a time
                                var minkaOut = ambush.MinkaOnPlayer != Guid.Empty;
                                var stormOut = ambush.StormOnPlayer != Guid.Empty;
                                if (minkaOut || stormOut) break; // a cat is already deployed

                                // Determine which cats are off cooldown
                                var minkaAvailable = ambush.MinkaCooldown <= 0;
                                var stormAvailable = ambush.StormCooldown <= 0;

                                if (minkaAvailable || stormAvailable)
                                {
                                    // 50% chance to pick which cat (if both available)
                                    string deployType;
                                    if (minkaAvailable && stormAvailable)
                                        deployType = _rand.Luck(1, 2) ? "Минька" : "Штормяк";
                                    else
                                        deployType = minkaAvailable ? "Минька" : "Штормяк";

                                    // Add passive to enemy
                                    fightEnemy.GameCharacter.Passive.Add(new Passive(
                                        deployType,
                                        $"Кот {deployType} от {player.DiscordUsername}",
                                        true
                                    ));
                                    fightEnemy.Passives.KotikiCatType = deployType;
                                    fightEnemy.Passives.KotikiCatOwnerId = player.GetPlayerId();

                                    if (deployType == "Минька")
                                        ambush.MinkaOnPlayer = fightEnemyId;
                                    else
                                        ambush.StormOnPlayer = fightEnemyId;

                                    // Remove deployed cat's passive from Котики until it returns
                                    player.GameCharacter.Passive.RemoveAll(x => x.PassiveName == deployType);

                                    game.Phrases.KotikiCatDeploy.SendLog(player, false);
                                    player.Status.AddInGamePersonalLogs(
                                        $"Кошачья засада: {deployType} остался на {fightEnemy.DiscordUsername}!\n");
                                    fightEnemy.Status.AddInGamePersonalLogs(
                                        $"Кошачья засада: Кот {deployType} сидит на вас!\n");
                                }
                            }
                        }
                    }
                    break;

                // TheBoys — Заказ Француза: check if order target was defeated
                case "Заказ Француза":
                    if (attack && player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var francieAfter = player.Passives.TheBoysFrancie;
                        if (francieAfter.OrderTarget == player.Status.IsWonThisCalculation
                            && francieAfter.OrderRoundsLeft > 0)
                        {
                            francieAfter.OrdersCompleted++;
                            francieAfter.OrderTarget = Guid.Empty;
                            francieAfter.OrderRoundsLeft = 0;
                            player.Status.AddBonusPoints(1, "Заказ Француза");
                            game.Phrases.TheBoysOrderComplete.SendLog(player, false);
                        }
                    }
                    break;

                // TheBoys — Хим.оружие: bonus points on win if fight was fair
                case "Хим.оружие":
                    if (attack && player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var chemLevel = player.Passives.TheBoysFrancie.ChemWeaponLevel;
                        if (chemLevel > 0)
                        {
                            var chemEnemy = game.PlayersList.Find(x =>
                                x.GetPlayerId() == player.Status.IsWonThisCalculation);
                            if (chemEnemy != null)
                            {
                                // Check if TheBoys was NOT tooGood/tooStronk (fair win)
                                var wasTooGood = chemEnemy.Status.FightEnemyWasTooGood;
                                var wasTooStronk = chemEnemy.Status.FightEnemyWasTooStronk;
                                if (!wasTooGood && !wasTooStronk)
                                {
                                    player.Status.AddBonusPoints(chemLevel, "Хим.оружие");
                                    game.Phrases.TheBoysChemWeapon.SendLog(player, false);
                                }
                            }
                        }
                    }
                    break;

                // TheBoys — Компромат М.М.: gather kompromat on attack target
                case "Компромат М.М.":
                    if (attack && player.Passives.TheBoysMM.NextAttackGathersKompromat)
                    {
                        var mmData = player.Passives.TheBoysMM;
                        // Fight must have happened (won or lost, but not block/skip)
                        if (player.Status.IsWonThisCalculation != Guid.Empty ||
                            player.Status.IsLostThisCalculation != Guid.Empty)
                        {
                            var fightTargetId = player.Status.IsWonThisCalculation != Guid.Empty
                                ? player.Status.IsWonThisCalculation
                                : player.Status.IsLostThisCalculation;
                            var fightTarget = game.PlayersList.Find(x => x.GetPlayerId() == fightTargetId);
                            if (fightTarget != null && !mmData.KompromatTargets.Contains(fightTargetId))
                            {
                                mmData.KompromatTargets.Add(fightTargetId);
                                var hint = GetKompromatHint(fightTarget.GameCharacter.Name);
                                mmData.KompromatHints[fightTargetId] = hint;
                                player.Status.AddInGamePersonalLogs(
                                    $"Компромат М.М.: Досье на {fightTarget.DiscordUsername}: \"{hint}\"\n");
                                game.Phrases.TheBoysKompromatGathered.SendLog(player, false);
                            }
                            mmData.NextAttackGathersKompromat = false;
                        }
                    }
                    break;
            }
    }
    //end handle during fight


    //after all fight
    public async Task HandleEndOfRound(GameClass game)
    {
        foreach (var player in game.PlayersList)
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Возвращение из мертвых":
                    //didn't fail but didn't succseed   
                    if (game.IsKratosEvent && game.RoundNo >= 16 && game.PlayersList.Count(x => !x.Passives.IsDead) < 5)
                    {
                        game.IsKratosEvent = false;
                        game.AddGlobalLogs($"У {player.GameCharacter.Name}а есть тактика и он ее придерживался...");
                        await game.Phrases.KratosEventNo.SendLogSeparateWithFile(player, false, "DataBase/art/events/kratos_death.jpg", false, 15000);
                    }

                    if (game.IsKratosEvent && player.Passives.IsDead)
                    {
                        game.IsKratosEvent = false;
                        game.AddGlobalLogs($"{player.GameCharacter.Name} решил доверится богам зная последствия...");
                        await game.Phrases.KratosEventFailed.SendLogSeparateWithFile(player, false, "DataBase/art/events/kratos_hell.png", false, 15000);
                    }
                    break;

                case "Лучше с двумя, чем с адекватными":
                    foreach (var t in game.PlayersList)
                    {
                        if (t.GameCharacter.GetIntelligence() != player.GameCharacter.GetIntelligence() && t.GameCharacter.GetPsyche() != player.GameCharacter.GetPsyche()) continue;

                        var tigr = player.Passives.TigrTwoBetterList;

                        if (!tigr.FriendList.Contains(t.GetPlayerId()))//&& tigr.FriendList.Count < 4
                        {
                            tigr.FriendList.Add(t.GetPlayerId());
                            // me.Status.AddRegularPoints();
                            player.Status.AddBonusPoints(3, "Лучше с двумя, чем с адекватными");
                            game.Phrases.TigrTwoBetter.SendLog(player, false);
                        }
                    }

                    break;

                case "Безумие":
                    var madd = player.Passives.DeepListMadnessList;

                    if (madd.RoundItTriggered == game.RoundNo)
                    {
                        var regularStats = madd.MadnessList.Find(x => x.Index == 1);
                        var madStats = madd.MadnessList.Find(x => x.Index == 2);


                        var intel = player.GameCharacter.GetIntelligence() - madStats.Intel;
                        var str = player.GameCharacter.GetStrength() - madStats.Str;
                        var speed = player.GameCharacter.GetSpeed() - madStats.Speed;
                        var psy = player.GameCharacter.GetPsyche() - madStats.Psyche;


                        player.GameCharacter.SetIntelligence(regularStats.Intel + intel, "Безумие", false);
                        player.GameCharacter.SetStrength(regularStats.Str + str, "Безумие", false);
                        player.GameCharacter.SetSpeed(regularStats.Speed + speed, "Безумие", false);
                        player.GameCharacter.SetPsyche(regularStats.Psyche + psy, "Безумие", false);
                        player.GameCharacter.SetAnySkillMultiplier();
                        player.Passives.DeepListMadnessList = new DeepList.Madness();

                        player.GameCharacter.AddPsyche(-1, "Безумие");
                    }

                    break;

                case "Претендент русского сервера":
                    var glebChall = player.Passives.GlebChallengerList;

                    if (glebChall.RoundItTriggered == game.RoundNo)
                    {
                        //x3 point:
                        player.Status.SetScoresToGiveAtEndOfRound(player.Status.GetScoresToGiveAtEndOfRound() * 3,
                            "Претендент русского сервера");

                        //end x3 point:
                        var regularStats = glebChall.MadnessList.Find(x => x.Index == 1);
                        var madStats = glebChall.MadnessList.Find(x => x.Index == 2);


                        var intel = player.GameCharacter.GetIntelligence() - madStats.Intel;
                        var str = player.GameCharacter.GetStrength() - madStats.Str;
                        var speed = player.GameCharacter.GetSpeed() - madStats.Speed;
                        var psy = player.GameCharacter.GetPsyche() - madStats.Psyche;


                        player.GameCharacter.SetIntelligence(regularStats.Intel + intel,
                            "Претендент русского сервера", false);
                        player.GameCharacter.SetStrength(regularStats.Str + str,
                            "Претендент русского сервера", false);
                        player.GameCharacter.SetSpeed(regularStats.Speed + speed,
                            "Претендент русского сервера", false);
                        player.GameCharacter.SetPsyche(regularStats.Psyche + psy,
                            "Претендент русского сервера", false);
                        player.GameCharacter.AddExtraSkill(-99, "Претендент русского сервера", false);
                        player.GameCharacter.SetAnySkillMultiplier();
                        player.Passives.GlebChallengerList = new DeepList.Madness();
                    }

                    break;

                case "Хождение боком":
                    var craboRack = player.Passives.CraboRackSidewaysBooleList;

                    if (craboRack.RoundItTriggered == game.RoundNo)
                    {
                        var regularStats = craboRack.MadnessList.Find(x => x.Index == 1);
                        var madStats = craboRack.MadnessList.Find(x => x.Index == 2);
                        var speed = player.GameCharacter.GetSpeed() - madStats.Speed;
                        player.GameCharacter.SetSpeed(regularStats.Speed + speed, "Хождение боком", false);
                        player.Passives.CraboRackSidewaysBooleList = new DeepList.Madness();
                    }

                    break;

                case "Гребанные ассассины":
                    var leCrip = player.Passives.LeCrispAssassins;

                    if (leCrip.AdditionalPsycheCurrent > 0)
                        player.GameCharacter.AddPsyche(leCrip.AdditionalPsycheCurrent * -1, "Гребанные ассассины", false);
                    if (leCrip.AdditionalPsycheForNextRound > 0)
                        player.GameCharacter.AddPsyche(leCrip.AdditionalPsycheForNextRound, "Гребанные ассассины");

                    leCrip.AdditionalPsycheCurrent = leCrip.AdditionalPsycheForNextRound;
                    leCrip.AdditionalPsycheForNextRound = 0;
                    break;

                case "Импакт":
                    var leImpact = player.Passives.LeCrispImpact;

                    if (leImpact.IsLost)
                    {
                        leImpact.ImpactTimes = 0;
                    }
                    else
                    {
                        leImpact.ImpactTimes += 1;
                        player.Status.AddBonusPoints(1, "Импакт");
                        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill();
                        game.Phrases.LeCrispImpactPhrase.SendLog(player, false, $"(x{leImpact.ImpactTimes}) ");
                    }

                    leImpact.IsLost = false;
                    break;

                case "Великий Комментатор":
                    if (game.RoundNo is >= 3 and <= 6)
                    {
                        if (_rand.Luck(1, 5))
                        {
                            var tolyaTalked = player.Passives.TolyaTalked;
                            if (tolyaTalked.PlayerHeTalkedAbout.Count < 2)
                            {
                                var randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];

                                //Most wanted — force Rick as target
                                var rickMw = game.PlayersList.Find(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Most wanted"));
                                if (rickMw != null && rickMw.GetPlayerId() != player.GetPlayerId() && !tolyaTalked.PlayerHeTalkedAbout.Contains(rickMw.GetPlayerId()))
                                    randomPlayer = rickMw;
                                //end Most wanted

                                while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.GetPlayerId()))
                                    randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];


                                if (randomPlayer.GetPlayerId() == player.GetPlayerId())
                                    do
                                    {
                                        randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];
                                    } while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.GetPlayerId()));

                                if (randomPlayer.GetPlayerId() == player.GetPlayerId())
                                    do
                                    {
                                        randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];
                                    } while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.GetPlayerId()));


                                tolyaTalked.PlayerHeTalkedAbout.Add(randomPlayer.GetPlayerId());

                                // Выдуманный персонаж: Монстра нельзя просветить
                                if (randomPlayer.GameCharacter.Passive.Any(x => x.PassiveName == "Выдуманный персонаж"))
                                {
                                    var tolyaFailSnippet = $"Толя попытался что-то разузнать про {randomPlayer.DiscordUsername}, но не удалось просветить";
                                    game.AddGlobalLogs(tolyaFailSnippet);
                                }
                                else
                                {
                                    var tolyaLogSnippet = $"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {randomPlayer.GameCharacter.Name}";
                                    game.AddGlobalLogs(tolyaLogSnippet);
                                    game.KiraHiddenLogSnippets.Add(tolyaLogSnippet);
                                }
                            }
                        }
                    }

                    break;

                case "Раммус мейн":
                    var tolya = player.Passives.TolyaRammusTimes;
                    if (tolya != null)
                    {
                        var rammusCount = 0;
                        switch (tolya.FriendList.Count)
                        {
                            case 1:
                                game.Phrases.TolyaRammusPhrase.SendLog(player, false);
                                rammusCount = 1;
                                break;
                            case 2:
                                game.Phrases.TolyaRammus2Phrase.SendLog(player, false);
                                rammusCount = 2;
                                break;
                            case 3:
                                game.Phrases.TolyaRammus3Phrase.SendLog(player, false);
                                rammusCount = 3;
                                break;
                            case 4:
                                game.Phrases.TolyaRammus4Phrase.SendLog(player, false);
                                rammusCount = 4;
                                break;
                            case 5:
                                game.Phrases.TolyaRammus5Phrase.SendLog(player, false);
                                rammusCount = 5;
                                break;
                        }

                        if (rammusCount > 0)
                        {
                            player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(1);
                            player.GameCharacter.AddMoral(rammusCount * rammusCount, "Раммус мейн");
                        }

                        tolya.FriendList.Clear();
                    }

                    break;

                case "Привет со дна":
                    var extraPoints = game.SkipPlayersThisRound + game.PlayersList.Count(p => p.Status.IsBlock);
                    if (extraPoints > 0)
                        player.Status.AddBonusPoints(extraPoints, "Привет со дна");
                    break;

                case "Обучение":
                    //There is a second part in "GetLvlUp()" !!!!!!!!!! <<<<<<<<<<
                    var siri = player.Passives.SirinoksTraining;

                    if (siri != null && siri.Training.Count >= 1)
                    {
                        var stats = siri.Training.OrderByDescending(x => x.StatNumber).ToList().First();

                        switch (stats.StatIndex)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(1, "Обучение");
                                if (player.GameCharacter.GetIntelligence() >= stats.StatNumber)
                                {
                                    player.GameCharacter.AddMoral(3, "Обучение");
                                    player.GameCharacter.AddIntelligenceQualitySkillBonus(1, "Обучение");
                                    siri.Training.Clear();
                                }

                                break;
                            case 2:
                                player.GameCharacter.AddStrength(1, "Обучение");
                                if (player.GameCharacter.GetStrength() >= stats.StatNumber)
                                {
                                    player.GameCharacter.AddMoral(3, "Обучение");
                                    player.GameCharacter.AddIntelligenceQualitySkillBonus(1, "Обучение");
                                    siri.Training.Clear();
                                }

                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(1, "Обучение");
                                if (player.GameCharacter.GetSpeed() >= stats.StatNumber)
                                {
                                    player.GameCharacter.AddMoral(3, "Обучение");
                                    player.GameCharacter.AddIntelligenceQualitySkillBonus(1, "Обучение");
                                    siri.Training.Clear();
                                }

                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(1, "Обучение");
                                if (player.GameCharacter.GetPsyche() >= stats.StatNumber)
                                {
                                    player.GameCharacter.AddMoral(3, "Обучение");
                                    player.GameCharacter.AddIntelligenceQualitySkillBonus(1, "Обучение");
                                    siri.Training.Clear();
                                }

                                break;
                        }
                    }

                    break;

                case "Одиночество":
                    var hard = player.Passives.HardKittyLoneliness;
                    if (hard != null) hard.Activated = false;
                    break;

                case "3-0 обоссан":
                    var tigrThreeZero = player.Passives.TigrThreeZeroList;
                    /*
                     if 0:0 and win-lost: lost => win
                     if 1:0 and win-lost: lost => win
                     if 2:0 and win-lost: win => lost
                    */
                    foreach (var lost in tigrThreeZero.WhoToLostThisRound.ToList())
                    {
                        var threeZero = tigrThreeZero.FriendList.Find(x => x.EnemyPlayerId == lost);
                        if (threeZero == null)
                        {
                            tigrThreeZero.WhoToLostThisRound.Remove(lost);
                            continue;
                        }

                        if (tigrThreeZero.WhoToWinThisRound.Contains(lost))
                        {
                            if (threeZero.WinsSeries >= 2) 
                                continue;

                            threeZero.WinsSeries = 0;
                            tigrThreeZero.WhoToLostThisRound.Remove(lost);
                        }
                        else
                        {
                            threeZero.WinsSeries = 0;
                            tigrThreeZero.WhoToLostThisRound.Remove(lost);
                        }
                    }

                    foreach (var win in tigrThreeZero.WhoToWinThisRound.ToList())
                    {
                        var threeZero = tigrThreeZero.FriendList.Find(x => x.EnemyPlayerId == win);
                        if (threeZero == null)
                        {
                            tigrThreeZero.FriendList.Add(new Tigr.ThreeZeroSubClass(win));
                            continue;
                        }
                        threeZero.WinsSeries++;


                        if (threeZero.WinsSeries < 3 || !threeZero.IsUnique) 
                            continue;

                        player.Status.AddRegularPoints(3, "3-0 обоссан");
                        player.GameCharacter.AddExtraSkill(30, "3-0 обоссан");
                        player.GameCharacter.AddMoral(3, "3-0 обоссан");

                        var enemyAcc = game.PlayersList.Find(x => x.GetPlayerId() == win);
                        if (enemyAcc == null) continue;

                        enemyAcc.GameCharacter.AddIntelligence(-1, "3-0 обоссан");
                        enemyAcc.MinusPsycheLog(enemyAcc.GameCharacter, game, -1, "3-0 обоссан");

                        game.Phrases.TigrThreeZero.SendLog(player, false);

                        threeZero.IsUnique = false;
                    }

                    foreach (var threeZero in tigrThreeZero.WhoToLostThisRound.ToList().Select(lost => tigrThreeZero.FriendList.Find(x => x.EnemyPlayerId == lost)))
                    {
                        if (threeZero != null)
                            threeZero.WinsSeries = 0;
                    }
                    
                    tigrThreeZero.WhoToLostThisRound.Clear();
                    tigrThreeZero.WhoToWinThisRound.Clear();
                    break;
                
                case "Доебаться":
                    var hardKitty = player.Passives.HardKittyDoebatsya;
                    for (var i = hardKitty.EnemyPlayersLostTo.Count - 1; i >= 0; i--)
                    {
                        var found = hardKitty.LostSeriesCurrent.Find(x =>
                            x.EnemyPlayerId == hardKitty.EnemyPlayersLostTo[i]);
                        if (found != null)
                        {
                            found.Series = 0;
                            game.Phrases.HardKittyDoebatsyaAnswerPhrase.SendLog(player, false);
                        }

                        hardKitty.EnemyPlayersLostTo.RemoveAt(i);
                    }

                    break;

                case "Им это не понравится":
                    if (game.RoundNo is 2 or 4 or 6 or 8)
                    {
                        var spartan = player.Passives.SpartanMark;
                        spartan.FriendList.Clear();

                        Guid enemy1;
                        Guid enemy2;

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy1 = game.PlayersList[randIndex].GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Глеб" or "mylorik" or
                                "Загадочный Спартанец в маске")
                                enemy1 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Злой Школьник" && game.RoundNo < 4)
                                enemy1 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Вампур" && game.RoundNo >= 4)
                                enemy1 = player.GetPlayerId();
                        } while (enemy1 == player.GetPlayerId());

                        // Most wanted: force Rick as enemy1
                        var rickMw2 = game.PlayersList.Find(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Most wanted"));
                        if (rickMw2 != null && rickMw2.GetPlayerId() != player.GetPlayerId())
                            enemy1 = rickMw2.GetPlayerId();

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy2 = game.PlayersList[randIndex].GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Глеб" or "mylorik" or
                                "Загадочный Спартанец в маске")
                                enemy2 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Злой Школьник" && game.RoundNo < 4)
                                enemy2 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Вампур" && game.RoundNo >= 4)
                                enemy2 = player.GetPlayerId();
                            if (enemy2 == enemy1)
                                enemy2 = player.GetPlayerId();
                        } while (enemy2 == player.GetPlayerId());


                        spartan.FriendList.Add(enemy2);
                        spartan.FriendList.Add(enemy1);
                    }

                    break;

                case "Дерзкая школота":
                    if (!player.Status.IsSkip)
                    {
                        player.GameCharacter.AddExtraSkill(-20, "Дерзкая школота");

                        var randStat1 = _rand.Random(1, 4);
                        var randStat2 = _rand.Random(1, 4);
                        switch (randStat1)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(-1, "Дерзкая школота");
                                break;
                            case 2:
                                player.GameCharacter.AddStrength(-1, "Дерзкая школота");
                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(-1, "Дерзкая школота");
                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(-1, "Дерзкая школота");
                                break;
                        }

                        switch (randStat2)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(-1, "Дерзкая школота");
                                break;
                            case 2:
                                player.GameCharacter.AddStrength(-1, "Дерзкая школота");
                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(-1, "Дерзкая школота");
                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(-1, "Дерзкая школота");
                                break;
                        }
                    }

                    break;

                case "Много выебывается":

                    var noAttack = true;

                    foreach (var target in game.PlayersList)
                    {
                        if (target.GetPlayerId() == player.GetPlayerId()) continue;
                        if (target.Status.WhoToAttackThisTurn.Contains(player.GetPlayerId()))
                            noAttack = false;
                    }

                    if (noAttack)
                    {
                        player.Status.AddRegularPoints(1, "Много выебывается");
                        game.Phrases.MitsukiTooMuchFuckingNoAttack.SendLog(player, true);
                    }

                    break;

                case "Гематофагия":
                    var vampyr = player.Passives.VampyrHematophagiaList;


                    for (var i = vampyr.HematophagiaAddEndofRound.Count - 1; i >= 0; i--)
                    {
                        var hematophagia = vampyr.HematophagiaAddEndofRound[i];
                        switch (hematophagia.StatIndex)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(2, "Гематофагия");
                                break;
                            case 2:
                                player.GameCharacter.AddStrength(2, "Гематофагия");
                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(2, "Гематофагия");
                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(2, "Гематофагия");
                                break;
                        }

                        vampyr.HematophagiaCurrent.Add(new Vampyr.HematophagiaSubClass(hematophagia.StatIndex, hematophagia.EnemyId));
                        vampyr.HematophagiaAddEndofRound.RemoveAt(i);
                    }

                    for (var i = vampyr.HematophagiaRemoveEndofRound.Count - 1; i >= 0; i--)
                    {
                        var hematophagia = vampyr.HematophagiaRemoveEndofRound[i];

                        switch (hematophagia.StatIndex)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(-2, "СОсиновый кол");
                                player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                break;
                            case 2:
                                player.GameCharacter.AddStrength(-2, "СОсиновый кол");
                                player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(-2, "СОсиновый кол");
                                player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(-2, "СОсиновый кол");
                                player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                break;
                        }

                        var enemy = vampyr.HematophagiaCurrent.Find(x => x.EnemyId == hematophagia.EnemyId);
                        vampyr.HematophagiaCurrent.Remove(enemy);
                        vampyr.HematophagiaRemoveEndofRound.RemoveAt(i);
                    }

                    break;

                case "Вампуризм":
                    vampyr = player.Passives.VampyrHematophagiaList;
                    if (vampyr.HematophagiaCurrent.Count > 0)
                        if (game.RoundNo is 2 or 4 or 6 or 8 or 10)
                            player.GameCharacter.AddMoral(vampyr.HematophagiaCurrent.Count, "Вампуризм");
                    break;

                case "Неприметность":
                    // Recalculate top 2 serious targets every round based on current combat power
                    var saitamaEndUnnoticed = player.Passives.SaitamaUnnoticed;
                    saitamaEndUnnoticed.SeriousTargets = game.PlayersList
                        .Where(x => x.GetPlayerId() != player.GetPlayerId())
                        .OrderByDescending(x => x.GameCharacter.GetSkill())
                        .Take(2)
                        .Select(x => x.GetPlayerId())
                        .ToList();
                    break;

                case "Ищет достойного противника":

                    break;

                case "Гигантские бобы":
                    // Ingredient assignment now happens in GetLvlUp (GameReactions.cs)
                    break;

                case "Портальная пушка":
                    var gunEnd = player.Passives.RickPortalGun;
                    if (!gunEnd.Invented && player.GameCharacter.GetIntelligence() >= 30)
                    {
                        gunEnd.Invented = true;
                        gunEnd.Charges++;
                        game.Phrases.RickPortalGunInvented.SendLog(player, false);
                    }
                    // Portal gun x2 multiplier: double round score when portal was fired
                    // (disabled by Подсчет and other multiplier-disabling passives)
                    if (gunEnd.FiredThisRound)
                    {
                        var isMultiplierDisabled = game.PlayersList.Any(p2 =>
                            p2.GameCharacter.Passive.Any(pas => pas.PassiveName == "Подсчет") &&
                            p2.Passives.TolyaCount.TargetList.Any(x =>
                                x.RoundNumber == game.RoundNo - 1 && x.Target == player.GetPlayerId()));
                        if (!isMultiplierDisabled)
                        {
                            var currentScore = player.Status.GetScoresToGiveAtEndOfRound();
                            player.Status.SetScoresToGiveAtEndOfRound(currentScore * 2, "Портальная пушка");
                            player.Status.AddInGamePersonalLogs("Портальная пушка: Очки из двух мульти-вселенных! x2\n");
                        }
                        gunEnd.FiredThisRound = false;
                    }
                    break;

                case "Огурчик Рик":
                    var pickleEnd = player.Passives.RickPickle;
                    if (pickleEnd.PickleTurnsRemaining > 0)
                    {
                        pickleEnd.PickleTurnsRemaining--;
                        if (pickleEnd.PickleTurnsRemaining == 0 && !pickleEnd.WasAttackedAsPickle)
                            pickleEnd.PenaltyTurnsRemaining = 1;
                    }
                    break;

                case "Тетрадь смерти":
                    var deathNote = player.Passives.KiraDeathNote;
                    if (deathNote.CurrentRoundTarget != Guid.Empty)
                    {
                        var dnTarget = game.PlayersList.Find(x => x.GetPlayerId() == deathNote.CurrentRoundTarget);
                        if (dnTarget != null)
                        {
                            // 15% chance Kira writes on glass instead of the Death Note
                            if (_rand.Luck(15))
                            {
                                player.Status.AddInGamePersonalLogs("Рюк: ЛАЙТ, ТЫ ПИШЕШЬ НА СТЕКЛЕ\n");
                                deathNote.CurrentRoundTarget = Guid.Empty;
                                deathNote.CurrentRoundName = "";
                                break;
                            }

                            var writtenName = deathNote.CurrentRoundName.Trim();
                            var actualName = dnTarget.GameCharacter.Name;
                            if (string.Equals(writtenName, actualName, StringComparison.OrdinalIgnoreCase))
                            {
                                // Goblins are immune to kill effects
                                if (dnTarget.GameCharacter.Name == "Стая Гоблинов") break;
                                // Correct — target dies
                                dnTarget.Passives.IsDead = true;
                                dnTarget.Passives.DeathSource = "Kira";
                                // Achievement tracking: Kira kill
                                player.Passives.AchievementTracker.KiraKills++;
                                dnTarget.Passives.AchievementTracker.WasKilledByKira = true;
                                if (dnTarget.GameCharacter.Name == "Кира")
                                    player.Passives.AchievementTracker.SurvivedKiraAttempt = false; // killer gets "kill_a_god" tracked at game end
                                // Монстр без имени: +1 regular point per death
                                foreach (var mp in game.PlayersList.Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Монстр")))
                                {
                                    mp.Status.AddRegularPoints(1, "Монстр");
                                    game.Phrases.MonsterDeath.SendLog(mp, false);
                                }
                                var isL = dnTarget.GetPlayerId() == player.Passives.KiraL.LPlayerId;
                                var pts = isL ? 4 : 2;
                                player.Status.AddRegularPoints(pts, "Тетрадь смерти");
                                player.GameCharacter.AddIntelligence(-1, "Гений");
                                deathNote.Entries.Add(new Characters.Kira.DeathNoteEntry
                                {
                                    TargetPlayerId = dnTarget.GetPlayerId(),
                                    WrittenName = writtenName,
                                    RoundWritten = game.RoundNo,
                                    WasCorrect = true
                                });
                                var deathLog = $"{dnTarget.DiscordUsername} умер от сердечного приступа...";
                                game.AddGlobalLogs(deathLog);
                                game.Phrases.KiraDeathNoteKill.SendLog(player, true);

                                // Kira killed L — special dialogue
                                if (isL)
                                {
                                    // Achievement: kira_kills_l
                                    player.Passives.AchievementTracker.KiraKills++; // extra count for L kill
                                    game.AddGlobalLogs(
                                        $"В связи с загадочными обстоятельствами, известный детектив по кличке **L** мертв. Его настоящее имя было {dnTarget.DiscordUsername}\n" +
                                        "**Kira:** Ну и что LLLLLLL???!?! КТО ТЕПЕРЬ... КТО ТЕПЕРЬ... эм... КТО ИЗ НАС ПОБЕДИЛ???!?! ХАХХХАХАХАХ! ГАВ ГАВ ГАВ");
                                }
                            }
                            else
                            {
                                // Wrong name — lock target in failed list
                                if (!deathNote.FailedTargets.Contains(dnTarget.GetPlayerId()))
                                    deathNote.FailedTargets.Add(dnTarget.GetPlayerId());
                                deathNote.Entries.Add(new Characters.Kira.DeathNoteEntry
                                {
                                    TargetPlayerId = dnTarget.GetPlayerId(),
                                    WrittenName = writtenName,
                                    RoundWritten = game.RoundNo,
                                    WasCorrect = false
                                });
                                game.Phrases.KiraDeathNoteFailed.SendLog(player, false);
                            }
                        }
                        deathNote.CurrentRoundTarget = Guid.Empty;
                        deathNote.CurrentRoundName = "";
                    }
                    break;

                case "L":
                    var kiraL = player.Passives.KiraL;
                    if (kiraL.LPlayerId != Guid.Empty && !kiraL.IsArrested)
                    {
                        // Check if Kira and L fought this round (either lost to the other)
                        var lPlayer = game.PlayersList.Find(x => x.GetPlayerId() == kiraL.LPlayerId);
                        if (lPlayer != null)
                        {
                            var kiraLostToL = player.Status.WhoToLostEveryRound.Any(y => y.RoundNo == game.RoundNo && y.EnemyId == kiraL.LPlayerId);
                            var lLostToKira = lPlayer.Status.WhoToLostEveryRound.Any(y => y.RoundNo == game.RoundNo && y.EnemyId == player.GetPlayerId());
                            if (!kiraLostToL && !lLostToKira)
                            {
                                player.GameCharacter.AddMoral(5, "L");
                                game.Phrases.KiraLNoFight.SendLog(player, false);
                            }
                        }
                    }
                    break;

                // Глаза Итачи: steal points from active target + charge
                case "Глаза Итачи":
                    var tsukuyomi = player.Passives.ItachiTsukuyomi;

                    // Steal points from active target
                    if (tsukuyomi.TsukuyomiActiveTarget != Guid.Empty)
                    {
                        var tsukuyomiVictim = game.PlayersList.Find(x => x.GetPlayerId() == tsukuyomi.TsukuyomiActiveTarget);
                        if (tsukuyomiVictim != null)
                        {
                            var stolenPoints = tsukuyomiVictim.Status.GetScoresToGiveAtEndOfRound();
                            if (stolenPoints > 0)
                            {
                                player.Status.AddBonusPoints(stolenPoints, "Глаза Итачи");
                                tsukuyomiVictim.Status.SetScoresToGiveAtEndOfRound(0, "Цукуеми");
                                tsukuyomi.TotalStolenPoints += stolenPoints;
                                if (!tsukuyomi.StolenFromPlayers.ContainsKey(tsukuyomi.TsukuyomiActiveTarget))
                                    tsukuyomi.StolenFromPlayers[tsukuyomi.TsukuyomiActiveTarget] = 0;
                                tsukuyomi.StolenFromPlayers[tsukuyomi.TsukuyomiActiveTarget] += stolenPoints;
                                game.Phrases.ItachiTsukuyomiSteal.SendLog(player, false);
                            }
                        }
                        tsukuyomi.TsukuyomiActiveTarget = Guid.Empty;
                        game.Phrases.ItachiTsukuyomiEnd.SendLog(player, false);
                    }

                    // Charge counter (cap at 2)
                    if (tsukuyomi.ChargeCounter < 2)
                    {
                        tsukuyomi.ChargeCounter++;
                        if (tsukuyomi.ChargeCounter >= 2)
                            game.Phrases.ItachiTsukuyomiCharge.SendLog(player, false);
                    }
                    break;

                case "Выгодная сделка":
                    var deals = player.Passives.SellerProfitableDealsThisRound;
                    if (deals > 0)
                    {
                        player.Status.AddBonusPoints(deals, "Выгодная сделка");
                        if (deals >= 3)
                            game.Phrases.SellerProfitBig.SendLog(player, false);
                        else
                            game.Phrases.SellerProfit.SendLog(player, false);
                    }
                    player.Passives.SellerProfitableDealsThisRound = 0;
                    break;

                case "Пассивный импакт":
                    if (player.Passives.DopaWonThisRound)
                    {
                        player.Status.AddBonusPoints(1, "Пассивный импакт");
                        game.Phrases.DopaImpact.SendLog(player, false);
                    }
                    player.Passives.DopaWonThisRound = false;
                    break;

                case "Взгляд в будущее":
                    if (player.Passives.DopaVision.Cooldown > 0) break;
                    // Filter out self-ID (block action) — vision requires two actual enemy targets
                    var visionTargets = player.Status.WhoToAttackThisTurn
                        .Where(t => t != player.GetPlayerId()).ToList();
                    if (visionTargets.Count < 2) break;

                    var t1Id = visionTargets[0];
                    var t2Id = visionTargets[1];
                    var t1 = game.PlayersList.Find(x => x.GetPlayerId() == t1Id);
                    var t2 = game.PlayersList.Find(x => x.GetPlayerId() == t2Id);
                    if (t1 == null || t2 == null) break;

                    bool visionProc = false;
                    if (t1.Status.WhoToAttackThisTurn.Contains(t2Id)) visionProc = true;
                    if (t2.Status.WhoToAttackThisTurn.Contains(t1Id)) visionProc = true;
                    if (t1.Status.IsBlock) visionProc = true;
                    if (t2.Status.IsBlock) visionProc = true;

                    if (visionProc)
                    {
                        int pointsAward = player.GameCharacter.Passive.Any(x => x.PassiveName == "Фарм") ? 4 : 2;
                        player.Status.AddRegularPoints(pointsAward, "Взгляд в будущее");
                        player.GameCharacter.AddExtraSkill(50, "Взгляд в будущее");
                        player.Passives.DopaVision.Cooldown = 1;
                        game.Phrases.DopaVisionProc.SendLog(player, false);
                    }
                    break;

                case "Великий летописец":
                    // 1. See others' logs
                    foreach (var other in game.PlayersList.Where(p => p.GetPlayerId() != player.GetPlayerId()))
                    {
                        var otherLogs = other.Status.GetInGamePersonalLogs();
                        if (!string.IsNullOrEmpty(otherLogs))
                            player.Status.AddInGamePersonalLogs($"[{other.DiscordUsername}]: {otherLogs}\n");
                    }

                    // 2. 20% chance to corrupt a random enemy's logs
                    if (_rand.Random(0, 99) < 20)
                    {
                        var enemies = game.PlayersList
                            .Where(p => p.GetPlayerId() != player.GetPlayerId()
                                     && !string.IsNullOrEmpty(p.Status.GetInGamePersonalLogs()))
                            .ToList();
                        if (enemies.Count > 0)
                        {
                            var victim = enemies[_rand.Random(0, enemies.Count - 1)];
                            var lines = victim.Status.GetInGamePersonalLogs().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                            if (lines.Length > 0)
                            {
                                var idx = _rand.Random(0, lines.Length - 1);
                                if (_rand.Random(0, 99) < 50)
                                    lines[idx] = "██████████████████";
                                else
                                    lines[idx] = "Салдорум был здесь...";
                                victim.Status.SetInGamePersonalLogs(string.Join('\n', lines) + '\n');
                                player.Passives.SaldorumCorruptionCount++;
                            }
                        }
                    }
                    break;

                // Napoleon — Вступить в союз: target info now shown via ⚔️ icon in leaderboard
                case "Вступить в союз":
                    break;

                // Таинственный Суппорт — "Protect": block gives +1 justice
                case "Protect":
                    if (player.Status.IsBlock)
                    {
                        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(1);
                        game.Phrases.SupportProtect.SendLog(player, false);
                    }
                    break;

                // Toxic Mate — "Tilted": +1 per enemy skip, +50 if ALL enemies blocked/skipped
                case "Tilted":
                    var tiltedEnemies = game.PlayersList.Where(x => x.GetPlayerId() != player.GetPlayerId()).ToList();
                    var skipCount = tiltedEnemies.Count(x => x.Status.IsSkip);
                    if (skipCount > 0)
                    {
                        player.Status.AddBonusPoints(skipCount, "Tilted");
                        game.Phrases.ToxicMateTiltedReact.SendLog(player, false);
                    }

                    var allPassive = tiltedEnemies.All(x => x.Status.IsBlock || x.Status.IsSkip);
                    if (allPassive)
                    {
                        player.Status.AddBonusPoints(50, "Tilted");
                        game.AddGlobalLogs("__**OPEN MID!** +20 **очков**__");
                    }
                    break;

                case "Отличный рудник":
                    // Mine income based on pre-sort position (so goblins get income even if they move away after sort)
                    // Skip round 1 — initial placement is randomized
                    var gobMinePlaceNow = player.Status.GetPlaceAtLeaderBoard();
                    if (game.RoundNo > 1 && gobMinePlaceNow is 1 or 2 or 6)
                    {
                        var gobMinePopEor = player.Passives.GoblinPopulation;
                        if (gobMinePopEor.Workers > 0)
                        {
                            player.Status.AddBonusPoints(gobMinePopEor.Workers, "Отличный рудник");
                            game.Phrases.GoblinMine.SendLog(player, false);
                            //player.Status.AddInGamePersonalLogs($"Рудник: +{gobMinePopEor.Workers} очков от трудяг!\n");
                        }
                    }
                    break;

                case "Гоблины тупые, но не идиоты":
                    // Save block intent for ziggurat build (IsBlock is still true here, gets reset before HandleNextRoundAfterSorting)
                    if (player.Status.IsBlock)
                        player.Passives.GoblinZiggurat.WantsToBuild = true;
                    break;

                // Котики — Штормяк: reset taunt target at end of round
                case "Штормяк":
                    // Only reset for Котики's own passive (not transferred Storm cat)
                    if (player.Passives.KotikiCatOwnerId == Guid.Empty)
                        player.Passives.KotikiStorm.CurrentTauntTarget = Guid.Empty;
                    break;

                // Котики — Кошачья засада: track rounds and cooldowns
                case "Кошачья засада":
                    var ambushEor = player.Passives.KotikiAmbush;
                    if (ambushEor.MinkaOnPlayer != Guid.Empty)
                        ambushEor.MinkaRoundsOnEnemy++;
                    if (ambushEor.MinkaCooldown > 0)
                        ambushEor.MinkaCooldown--;
                    if (ambushEor.StormCooldown > 0)
                        ambushEor.StormCooldown--;
                    break;

                // Монстр без имени — Пейзаж конца света: round 10 apocalypse
                case "Пейзаж конца света":
                    if (game.RoundNo == 10)
                    {
                        var deadNames = new List<string>();
                        foreach (var pawn in game.PlayersList.Where(x =>
                            x.Passives.IsJohanPawn &&
                            x.Passives.JohanPawnOwnerId == player.GetPlayerId() &&
                            !x.Passives.IsDead))
                        {
                            // Pawns who blocked or skipped survive
                            if (pawn.Status.IsBlock || pawn.Status.IsSkip) continue;
                            pawn.Passives.IsDead = true;
                            pawn.Passives.DeathSource = "Monster";
                            deadNames.Add(pawn.GameCharacter.Name);
                            player.Status.AddRegularPoints(1, "Монстр");
                        }

                        if (deadNames.Count > 0)
                        {
                            game.AddGlobalLogs($"{string.Join(", ", deadNames)} убили друг друга. Их тела были найдены в небольшом немецком городке.");
                            game.Phrases.MonsterApocalypse.SendLog(player, false);
                        }

                        // Non-pawns who fought Monster this round get a reward
                        foreach (var fighter in game.PlayersList.Where(x =>
                            !x.Passives.IsJohanPawn &&
                            x.GetPlayerId() != player.GetPlayerId() &&
                            x.Status.WhoToAttackThisTurn.Contains(player.GetPlayerId())))
                        {
                            fighter.Status.AddRegularPoints(7, "Пейзаж конца света");
                            fighter.Status.AddBonusPoints(10, "Пейзаж конца света");
                            game.AddGlobalLogs("Я увидел... Зверя... с семью головами и десятью рогами! Я выстрелил!");
                        }
                    }
                    break;

                // Геральт — Медитация: if meditated and NOT attacked → activate ALL oils
                case "Медитация":
                    if (player.GameCharacter.Name == "Геральт")
                    {
                        var geraltEorContracts = player.Passives.GeraltContracts;
                        if (geraltEorContracts.IsMeditating && !geraltEorContracts.WasAttackedDuringMeditation)
                        {
                            if (geraltEorContracts.OilInventory.Count > 0)
                            {
                                geraltEorContracts.OilsActivated = true;
                                game.Phrases.GeraltOilActivate.SendLog(player, false);
                            }
                            else
                                game.Phrases.GeraltMeditation.SendLog(player, false);
                        }
                    }
                    break;
            }

        // Геральт — Ведьмачий Заказ: check dead players with contracts → stolen bounty
        foreach (var geraltPlayer in game.PlayersList.Where(x => x.GameCharacter.Name == "Геральт"))
        {
            var geraltStolenContracts = geraltPlayer.Passives.GeraltContracts;
            var deadWithContracts = game.PlayersList.Where(x =>
                x.Passives.IsDead &&
                geraltStolenContracts.ContractMap.ContainsKey(x.GetPlayerId()) &&
                geraltStolenContracts.ContractMap[x.GetPlayerId()].Count > 0).ToList();

            foreach (var deadPlayer in deadWithContracts)
            {
                geraltStolenContracts.ContractMap.Remove(deadPlayer.GetPlayerId());
                deadPlayer.Passives.GeraltContractsOnMe.Clear();
                deadPlayer.Passives.GeraltContractOwnerId = Guid.Empty;
                geraltPlayer.MinusPsycheLog(geraltPlayer.GameCharacter, game, -1, "Ведьмачий Заказ");
                game.Phrases.GeraltBountyStolen.SendLog(geraltPlayer, false);
            }
        }

        // High Elo repeated loss — any player losing to a high-elo character for 2nd+ consecutive time
        var highEloNames = new HashSet<string> { "DeepList", "mylorik", "Глеб", "Dopa", "Загадочный Спартанец в маске" };
        foreach (var player in game.PlayersList)
        {
            if (player.Status.IsLostThisCalculation == Guid.Empty) continue;
            var enemy = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsLostThisCalculation);
            if (enemy == null || !highEloNames.Contains(enemy.GameCharacter.Name)) continue;

            // Check if also lost to this same enemy last round
            if (player.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == enemy.GetPlayerId()))
            {
                game.Phrases.HighEloLoss.SendLog(player, false);
            }
        }

        // LeCrisp Stonks — earned more than 10 regular points this round
        foreach (var player in game.PlayersList)
        {
            if (player.GameCharacter.Name != "LeCrisp") continue;
            if (player.Status.GetScoresToGiveAtEndOfRound() > 10)
            {
                game.Phrases.LeCrispStonks.SendLog(player, false);
            }
        }

        // Salldorum — end of round: reset Shen, record position history
        foreach (var player in game.PlayersList)
        {
            if (player.GameCharacter.Name != "Salldorum") continue;
            var shen = player.Passives.SalldorumShen;
            shen.ActiveThisTurn = false;
            shen.TargetPosition = -1;

            // Record position in history
            var posHistory = player.Passives.SalldorumChronicler.PositionHistory;
            var currentPos = player.Status.GetPlaceAtLeaderBoard();
            while (posHistory.Count < game.RoundNo)
                posHistory.Add(0);
            posHistory[game.RoundNo - 1] = currentPos;
        }
    }

    public async Task HandleNextRound(GameClass game)
    {
        foreach (var player in game.PlayersList)
        {
            foreach (var passive in player.GameCharacter.Passive.ToList())
                switch (passive.PassiveName)
                {
                    case "Коммуникация":
                        if (game.RoundNo == 6)
                        {
                            game.Phrases.YongGlebCommunicationReady.SendLog(player, false);
                        }
                        break;

                    case "Следит за игрой":
                        // Compute default bot attack preferences for Gleb
                        var metaTargets = game.PlayersList
                            .Where(x => x.GetPlayerId() != player.GetPlayerId())
                            .Select(t =>
                            {
                                decimal pref = 10;
                                var botJustice = player.GameCharacter.Justice.GetRealJusticeNow();
                                var targetJustice = t.GameCharacter.Justice.GetSeenJusticeNow();

                                if (botJustice == targetJustice) pref -= 5;
                                else if (botJustice < targetJustice) pref -= 7;

                                if (t.Status.GetPlaceAtLeaderBoard() == 1) pref -= 1;
                                if (player.Status.GetPlaceAtLeaderBoard() == 1 && t.Status.GetPlaceAtLeaderBoard() == 2) pref -= 1;

                                if (player.Status.WhoToLostEveryRound.Any(x =>
                                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == t.GetPlayerId() && x.IsTooGoodEnemy))
                                    pref -= 7;
                                else if (t.Status.WhoToLostEveryRound.Any(x =>
                                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == player.GetPlayerId() && x.IsTooGoodMe))
                                    pref -= 7;
                                else if (player.Status.WhoToLostEveryRound.Any(x =>
                                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == t.GetPlayerId() && x.IsStatsBetterEnemy))
                                    pref -= 5;

                                if (t.Status.WhoToLostEveryRound.Any(x =>
                                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == player.GetPlayerId() && x.IsTooGoodEnemy))
                                    pref += 4;

                                if (pref >= 5 && player.GameCharacter.HasSkillTargetOn(t.GameCharacter)) pref += 1;
                                if (pref >= 5 && player.GameCharacter.HasNemesisOver(t.GameCharacter)) pref += 3;

                                return new { Player = t, Pref = pref };
                            })
                            .OrderByDescending(x => x.Pref)
                            .Take(3)
                            .Select(x => x.Player.GetPlayerId())
                            .ToList();

                        player.Passives.YongGlebMetaClass = metaTargets;
                        break;

                    case "L":
                        if (game.RoundNo == 11)
                        {
                            var kiraLNext = player.Passives.KiraL;
                            if (kiraLNext.LPlayerId != Guid.Empty && !kiraLNext.IsArrested)
                            {
                                var lPlayerNext = game.PlayersList.Find(x => x.GetPlayerId() == kiraLNext.LPlayerId);
                                if (lPlayerNext != null)
                                {
                                    // Check if L correctly predicted Kira
                                    var lPredictedKira = lPlayerNext.Predict.Any(p =>
                                        p.PlayerId == player.GetPlayerId() &&
                                        string.Equals(p.CharacterName, "Кира", StringComparison.OrdinalIgnoreCase));
                                    if (lPredictedKira)
                                    {
                                        // Goblins are immune to kill effects
                                        if (player.GameCharacter.Name == "Стая Гоблинов") break;
                                        kiraLNext.IsArrested = true;
                                        player.Passives.IsDead = true;
                                        player.Passives.DeathSource = "Kira";
                                        // Монстр без имени: +1 regular point per death
                                        foreach (var mp in game.PlayersList.Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Монстр")))
                                        {
                                            mp.Status.AddRegularPoints(1, "Монстр");
                                            game.Phrases.MonsterDeath.SendLog(mp, false);
                                        }
                                        player.Status.AddBonusPoints(-500, "Арест Киры");

                                        game.AddGlobalLogs(
                                            "**L:** Эй, Кира.\n" +
                                            "**Kira:** Да?\n" +
                                            "**L:** Ты арестован. Ты Кира. Я думал так начать ко всем обращаться, но им оказался ты.\n\n" +
                                            $"**L:** Я поймал Киру... Причем совершенно случайно. Оказалось что он играл со мной в одну текстовую онлайн игру под ником {player.DiscordUsername} и пытался убить своих оппонентов с помощью какой-то тетрадки. Кто бы мог подумать. \n" +
                                            "А еще я уронил мороженное на тетрадь, она заляпалась и испортилась, теперь никто больше не будет умирать. Но не волнуйтесь, мороженное я слизал. \n" +
                                            "**Рюк:** Лайт, ну ты чего, совсем дурачок что ли? Зачем ты вообще играл в этот мусор? Нафига ты мне такой нужен. Запишу тебя в __свою__ тетрадь. \n" +
                                            "Kira -500 **очков**\n" +
                                            "Рюк +500 **очков**");
                                    }
                                }
                            }
                        }
                        break;

                    case "Ищет достойного противника":
                        if (game.RoundNo == 11)
                        {
                            // Round 10 just finished; RoundNo is already 11.
                            var saitamaWorthy = player.Passives.SaitamaUnnoticed;

                            var saitamaBeatTop1All = game.PlayersList.FindAll(x => x.Status.WhoToLostEveryRound.Any(y => y.RoundNo == 10 && y.EnemyId == player.GetPlayerId()));
                            var saitamaBeatTop1 = saitamaBeatTop1All.FindAll(x => x.Status.WhoToLostEveryRound.Any(y => y.RoundNo == 10 && y.PlaceAtLeaderBoardMe == 1 && y.WhoAttacked == player.GetPlayerId()));

                            if (saitamaBeatTop1.Count > 0)
                            {
                                // ONE PUUUUUUNCH! Restore all deferred points and moral!
                                var deferred = saitamaWorthy.DeferredPoints;
                                if (deferred > 0)
                                {
                                    player.Status.AddBonusPoints(4*deferred, "🐙🐙🐙Ищет достойного противника🐙🐙🐙");
                                    saitamaWorthy.DeferredPoints = 0;
                                }

                                var deferredMoral = saitamaWorthy.DeferredMoral;
                                if (deferredMoral > 0)
                                {
                                    player.GameCharacter.AddMoral(deferredMoral, "Ищет достойного противника");
                                    saitamaWorthy.DeferredMoral = 0;
                                }

                                game.AddGlobalLogs($"{player.DiscordUsername} наконец показал свою ИСТИННУЮ СИЛУ! ONE PUUUUUUNCH!!!");
                            }
                        }
                        break;

                    case "Чернильная завеса":
                        if (game.RoundNo == 11)
                        {
                            var octopusInk = player.Passives.OctopusInkList;
                            var octopusInv = player.Passives.OctopusInvulnerabilityList;

                            foreach (var t in octopusInk.RealScoreList)
                            {
                                var pl = game.PlayersList.Find(x => x.GetPlayerId() == t.PlayerId);
                                pl?.Status.AddBonusPoints(t.RealScore, "🐙");
                            }

                            player.Status.AddBonusPoints(octopusInv.Count, "🐙");

                            //sort
                            //     game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
                            //    for (var i = 0; i < game.PlayersList.Count; i++) game.PlayersList[i].Status.GetPlaceAtLeaderBoard() = i + 1;
                            //end sorting
                        }

                        break;

                    case "Они позорят военное искусство":
                        if (game.RoundNo == 10)
                            player.GameCharacter.SetStrength(0, "Они позорят военное искусство");
                        break;

                    case "Буль":
                        if (player.GameCharacter.GetPsyche() < 7)
                        {

                            if (_rand.Luck(1, 10 + player.GameCharacter.GetPsyche() * 5))
                            {
                                player.Status.IsSkip = true;
                                player.Status.ConfirmedSkip = false;
                                player.Status.IsBlock = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = new List<Guid>();

                                game.Phrases.MylorikBoolePhrase.SendLog(player, false);
                            }
                        }

                        var boole = player.Passives.MylorikBoole;
                        if (boole.IsBoole && player.GameCharacter.GetPsyche() > 0)
                        {
                            player.GameCharacter.AddStrength(-2, "Буль", false);
                            player.GameCharacter.AddExtraSkill(-22, "Буль", false);
                            boole.IsBoole = !boole.IsBoole;
                        }

                        if (!boole.IsBoole && player.GameCharacter.GetPsyche() <= 0)
                        {
                            player.GameCharacter.AddStrength(2, "Буль");
                            player.GameCharacter.AddExtraSkill(22, "Буль");
                            boole.IsBoole = !boole.IsBoole;
                        }

                        break;

                    case "Повторяет за myloran":
                        if (game.RoundNo == 5)
                        {
                            player.Status.AddInGamePersonalLogs(
                                "ZaRDaK: Ты никогда не возьмешь даймонд, Лорик. Удачи в промо.\nmylorik: ММММММММММ!!!!!  +4 Интеллекта.\n");
                            player.GameCharacter.AddIntelligence(4, "Повторяет за myloran", false);
                        }

                        if (game.RoundNo == 10)
                        {
                            player.Status.AddInGamePersonalLogs(
                                "ZaRDaK: Ты так и не апнул чалланджер? Хах, неудивительно.\nmylorik закупился у продавца сомнительных тактик: +228 *Скилла*!\n");
                            player.GameCharacter.AddExtraSkill(228, "Повторяет за myloran", false);
                        }

                        break;

                    case "Стримснайпят и банят и банят и банят":
                        if (game.RoundNo == 10)
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();
                            player.GameCharacter.SetPsyche(0, "Стримснайпят и банят и банят и банят");
                            player.GameCharacter.SetIntelligence(0,
                                "Стримснайпят и банят и банят и банят");
                            player.GameCharacter.SetStrength(10, "Стримснайпят и банят и банят и банят");
                            game.AddGlobalLogs(
                                $"{player.DiscordUsername}: ЕБАННЫЕ БАНЫ НА 10 ЛЕТ");
                        }

                        break;

                    case "Тигр топ, а ты холоп":
                        var tigr = player.Passives.TigrTopWhen;
                        if (tigr.WhenToTrigger.Contains(game.RoundNo))
                            player.Passives.TigrTop = new Tigr.TigrTopClass();
                        break;

                    case "Дерзкая школота":
                        if (game.RoundNo == 1)
                        {
                            game.Phrases.MitsukiCheekyBriki.SendLog(player, true);
                            player.Status.AddRegularPoints(1, "Много выебывается");
                            game.Phrases.MitsukiTooMuchFucking.SendLog(player, false);
                        }

                        break;

                    case "Школьник":
                        var acc = player.Passives.MitsukiNoPcTriggeredWhen;


                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();

                            game.Phrases.MitsukiSchoolboy.SendLog(player, true);
                            player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(5);
                        }

                        break;

                    case "АФКА":
                        var afkaChance = 32 - (game.RoundNo - player.GameCharacter.GetLastMoralRound()) * 4;
                        if (afkaChance <= 0)
                            afkaChance = 1;
                        if (_rand.Luck(1, afkaChance))
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();

                            game.Phrases.AwdkaAfk.SendLog(player, true);
                        }

                        break;

                    case "Я пытаюсь!":
                        var awdkaa = player.Passives.AwdkaTryingList;

                        foreach (var enemy in awdkaa.TryingList)
                            if (enemy != null)
                                if (enemy.Times >= 2 && enemy.IsUnique == false)
                                {
                                    player.Status.LvlUpPoints += 2;
                                    player.GameCharacter.AddExtraSkill(20, "Я пытаюсь!");
                                    await _gameUpdateMess.UpdateMessage(player);
                                    enemy.IsUnique = true;
                                    game.Phrases.AwdkaTrying.SendLog(player, true);
                                }

                        break;

                    case "Научите играть":
                        var awdkaTempStats = player.Passives.AwdkaTeachToPlayTempStats;

                        var awdka = player.Passives.AwdkaTeachToPlay;

                        //remove stats from previos time
                        if (awdkaTempStats.MadnessList.Count >= 2)
                        {
                            var regularStats = awdkaTempStats.MadnessList.Find(x => x.Index == 1);
                            var madStats = awdkaTempStats.MadnessList.Find(x => x.Index == 2);

                            var intel = player.GameCharacter.GetIntelligence() - madStats.Intel;
                            var str = player.GameCharacter.GetStrength() - madStats.Str;
                            var speed = player.GameCharacter.GetSpeed() - madStats.Speed;
                            var psy = player.GameCharacter.GetPsyche() - madStats.Psyche;

                            var intelToGive = regularStats.Intel + intel;
                            if (intelToGive > 10)
                                intelToGive = 10;
                            player.GameCharacter.SetIntelligence(intelToGive, "Научите играть", false);
                            player.GameCharacter.SetStrength(regularStats.Str + str, "Научите играть", false);
                            player.GameCharacter.SetSpeed(regularStats.Speed + speed, "Научите играть", false);
                            player.GameCharacter.SetPsyche(regularStats.Psyche + psy, "Научите играть", false);
                            player.GameCharacter.SetIntelligenceExtraText("");
                            player.GameCharacter.SetStrengthExtraText("");
                            player.GameCharacter.SetSpeedExtraText("");
                            player.GameCharacter.SetPsycheExtraText("");
                            player.Passives.AwdkaTeachToPlayTempStats.MadnessList.Clear();
                        }
                        //end remove stats


                        //crazy shit
                        player.Passives.AwdkaTeachToPlayTempStats = new DeepList.Madness
                        {
                            RoundItTriggered = game.RoundNo
                        };

                        awdkaTempStats = player.Passives.AwdkaTeachToPlayTempStats;

                        awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(1,
                            player.GameCharacter.GetIntelligence(),
                            player.GameCharacter.GetStrength(), player.GameCharacter.GetSpeed(),
                            player.GameCharacter.GetPsyche()));
                        //end crazy shit

                        if (awdka.Training.Count == 0) break;
                        //find out  the biggest stat
                        var bestSkill = awdka.Training.OrderByDescending(x => x.StatNumber).ToList().First();

                        var intel1 = player.GameCharacter.GetIntelligence();
                        var str1 = player.GameCharacter.GetStrength();
                        var speed1 = player.GameCharacter.GetSpeed();
                        var pshy1 = player.GameCharacter.GetPsyche();

                        switch (bestSkill.StatIndex)
                        {
                            case 1:
                                intel1 = bestSkill.StatNumber;
                                player.GameCharacter.SetIntelligenceExtraText(
                                    $" (<:volibir:894286361895522434> Интеллект {intel1})");
                                break;
                            case 2:
                                str1 = bestSkill.StatNumber;
                                player.GameCharacter.SetStrengthExtraText(
                                    $" (<:volibir:894286361895522434> Сила {str1})");
                                break;
                            case 3:
                                speed1 = bestSkill.StatNumber;
                                player.GameCharacter.SetSpeedExtraText(
                                    $" (<:volibir:894286361895522434> Скорость {speed1})");
                                break;
                            case 4:
                                pshy1 = bestSkill.StatNumber;
                                player.GameCharacter.SetPsycheExtraText(
                                    $" (<:volibir:894286361895522434> Психика {pshy1})");
                                break;
                        }

                        if (intel1 >= player.GameCharacter.GetIntelligence())
                            player.GameCharacter.SetIntelligence(intel1, "Научите играть");

                        if (str1 >= player.GameCharacter.GetStrength())
                            player.GameCharacter.SetStrength(str1, "Научите играть");

                        if (speed1 >= player.GameCharacter.GetSpeed())
                            player.GameCharacter.SetSpeed(speed1, "Научите играть");

                        if (pshy1 >= player.GameCharacter.GetPsyche())
                            player.GameCharacter.SetPsyche(pshy1, "Научите играть");
                        //end find out  the biggest stat

                        //crazy shit 2
                        awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(2, intel1, str1, speed1, pshy1));
                        player.Passives.AwdkaTeachToPlay = new Sirinoks.TrainingClass();
                        //end crazy shit 2

                        game.Phrases.AwdkaTeachToPlay.SendLog(player, true);
                        break;

                    case "Я за чаем":
                        var luck = _rand.Luck(1, 8);

                        var glebChalleger = player.Passives.GlebChallengerTriggeredWhen;


                        if (glebChalleger.WhenToTrigger.Contains(game.RoundNo))
                            luck = _rand.Luck(1, 7);

                        // Most wanted: bigger chance for tea when Rick is in the game
                        if (!luck && game.PlayersList.Any(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Most wanted") && x.GetPlayerId() != player.GetPlayerId()))
                            luck = _rand.Luck(1, 4);


                        var glebTea = player.Passives.GlebTea;

                        if (luck)
                        {
                            glebTea.Ready = true;
                            glebTea.TimesRolled++;
                        }

                        if (game.RoundNo == 9 && glebTea.TimesRolled == 0) glebTea.Ready = true;

                        if (glebTea.Ready)
                            game.Phrases.GlebTeaReadyPhrase.SendLog(player, true);
                        break;

                    case "Спящее хуйло":
                        acc = player.Passives.GlebSleepingTriggeredWhen;


                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();

                            player.GameCharacter.AddExtraSkill(-30, "Спящее хуйло");

                            player.GameCharacter.AvatarCurrent = player.GameCharacter.GetEventAvatar("Спящее хуйло");
                            game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                        }
                        else
                        {
                            player.GameCharacter.AvatarCurrent = player.GameCharacter.Avatar;
                        }

                        if (game.RoundNo == 11)
                        {
                            player.GameCharacter.AvatarCurrent = player.GameCharacter.GetEventAvatar("Спящее хуйло");
                            game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                        }

                        // Gleb sees Sirinoks become dragon on round 10
                        if (game.RoundNo == 10 && game.PlayersList.Any(x => x.GameCharacter.Name == "Sirinoks"))
                        {
                            game.AddGlobalLogs($"\n{player.DiscordUsername}: Ogo, drakon, nihuya sebe");
                        }

                        break;

                    case "Претендент русского сервера":
                        acc = player.Passives.GlebChallengerTriggeredWhen;

                        if (game.RoundNo == 10 && !acc.WhenToTrigger.Contains(game.RoundNo) &&
                            player.Status.GetPlaceAtLeaderBoard() > 2)
                        {
                            // шанс = 1 / (40 - место глеба в таблице * 4)
                            if (_rand.Luck(1, 40 - player.Status.GetPlaceAtLeaderBoard() * 4)) acc.WhenToTrigger.Add(game.RoundNo);
                        }

                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            //just check
                            player.Passives.GlebChallengerList = new DeepList.Madness
                            {
                                MadnessList = new List<DeepList.MadnessSub>(),
                                RoundItTriggered = game.RoundNo
                            };

                            var gleb = player.Passives.GlebChallengerList;
                            gleb.MadnessList.Add(new DeepList.MadnessSub(1, player.GameCharacter.GetIntelligence(),
                                player.GameCharacter.GetStrength(), player.GameCharacter.GetSpeed(),
                                player.GameCharacter.GetPsyche()));

                            //  var randomNumber =  _rand.Random(1, 100);

                            var intel = player.GameCharacter.GetIntelligence() >= 10 ? 10 : 9;
                            var str = player.GameCharacter.GetStrength() >= 10 ? 10 : 9;
                            var speed = player.GameCharacter.GetSpeed() >= 10 ? 10 : 9;
                            var pshy = player.GameCharacter.GetPsyche() >= 10 ? 10 : 9;


                            player.GameCharacter.SetIntelligence(intel, "Претендент русского сервера");
                            player.GameCharacter.SetStrength(str, "Претендент русского сервера");
                            player.GameCharacter.SetSpeed(speed, "Претендент русского сервера");
                            player.GameCharacter.SetPsyche(pshy, "Претендент русского сервера");
                            player.GameCharacter.AddExtraSkill(99, "Претендент русского сервера");
                            player.GameCharacter.SetTargetSkillMultiplier(2);


                            gleb.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));

                            //game.Phrases.GlebChallengerPhrase.SendLog(player, true);
                            await game.Phrases.GlebChallengerSeparatePhrase.SendLogSeparateWithFile(player, true, "DataBase/sound/Irelia.mp3", true, 0, roundsToPlay: 1);
                        }

                        break;

                    case "Хождение боком":
                        acc = player.Passives.CraboRackSidewaysBooleTriggeredWhen;

                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            //just check
                            player.Passives.CraboRackSidewaysBooleList = new DeepList.Madness
                            {
                                MadnessList = new List<DeepList.MadnessSub>(),
                                RoundItTriggered = game.RoundNo
                            };

                            var craboRack = player.Passives.CraboRackSidewaysBooleList;
                            craboRack.MadnessList.Add(new DeepList.MadnessSub(1, player.GameCharacter.GetIntelligence(),
                                player.GameCharacter.GetStrength(), player.GameCharacter.GetSpeed(),
                                player.GameCharacter.GetPsyche()));


                            var speed = 10;

                            player.GameCharacter.SetSpeed(speed, "Хождение боком");
                            craboRack.MadnessList.Add(new DeepList.MadnessSub(2, player.GameCharacter.GetIntelligence(),
                                player.GameCharacter.GetStrength(), speed, player.GameCharacter.GetPsyche()));
                            game.Phrases.CraboRackSidewaysBoolePhrase.SendLog(player, true);
                        }

                        break;

                    case "Сверхразум":
                        var currentDeepList = player.Passives.DeepListSupermindTriggeredWhen;

                        if (currentDeepList != null)
                            if (currentDeepList.WhenToTrigger.Any(x => x == game.RoundNo))
                            {
                                GamePlayerBridgeClass randPlayer;

                                // Most wanted: force discover Rick first
                                var rickMwSm = game.PlayersList.Find(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Most wanted"));
                                if (rickMwSm != null && rickMwSm.GetPlayerId() != player.GetPlayerId()
                                    && !player.Passives.DeepListSupermindKnown.KnownPlayers.Contains(rickMwSm.GetPlayerId()))
                                {
                                    randPlayer = rickMwSm;
                                }
                                else
                                {
                                    // Guard: skip if all other players are already known
                                    var knownCheck = player.Passives.DeepListSupermindKnown;
                                    if (knownCheck != null && knownCheck.KnownPlayers.Count >= game.PlayersList.Count - 1)
                                        break;

                                    do
                                    {
                                        randPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];

                                        var check1 = player.Passives.DeepListSupermindKnown;

                                        if (check1 != null)
                                            if (check1.KnownPlayers.Contains(randPlayer.GetPlayerId()))
                                                randPlayer = player;
                                    } while (randPlayer.GetPlayerId() == player.GetPlayerId());
                                }

                                var check = player.Passives.DeepListSupermindKnown;

                                check.KnownPlayers.Add(randPlayer.GetPlayerId());

                                game.Phrases.DeepListSuperMindPhrase.SendLog(player, randPlayer, true);
                            }

                        break;

                    case "Безумие":
                        var madd = player.Passives.DeepListMadnessTriggeredWhen;

                        if (madd != null)
                            if (madd.WhenToTrigger.Contains(game.RoundNo))
                            {
                                
                                //trigger maddness
                                //me.Status.AddBonusPoints(-3, "Безумие");
                                //just check
                                player.Passives.DeepListMadnessList = new DeepList.Madness
                                {
                                    MadnessList = new List<DeepList.MadnessSub>(),
                                    RoundItTriggered = game.RoundNo
                                };

                                var curr = player.Passives.DeepListMadnessList;
                                curr.MadnessList.Add(new DeepList.MadnessSub(1, player.GameCharacter.GetIntelligence(),
                                    player.GameCharacter.GetStrength(), player.GameCharacter.GetSpeed(),
                                    player.GameCharacter.GetPsyche()));


                                var intel = 0;
                                var str = 0;
                                var speed = 0;
                                var pshy = 0;

                                for (var i = 0; i < 4; i++)
                                {
                                    var statNumber = _rand.Random(0, 10);

                                    switch (i)
                                    {
                                        case 0:
                                            intel = statNumber;
                                            break;
                                        case 1:
                                            str = statNumber;
                                            break;
                                        case 2:
                                            speed = statNumber;
                                            break;
                                        case 3:
                                            pshy = statNumber;
                                            break;
                                    }
                                }

                                player.GameCharacter.SetIntelligence(intel, "Безумие");
                                player.GameCharacter.SetStrength(str, "Безумие");
                                player.GameCharacter.SetSpeed(speed, "Безумие");
                                player.GameCharacter.SetPsyche(pshy, "Безумие");
                                //2 это х3
                                player.GameCharacter.SetAnySkillMultiplier(3);
                                //me.Status.AddBonusPoints(-3, "Безумие");

                                game.Phrases.DeepListMadnessPhrase.SendLog(player, true);

                                // БОЛЬШЕ МОЛОКА ДЛЯ ХАРДКИТТИ!
                                if (game.PlayersList.Any(x => x.GameCharacter.Name == "HardKitty"))
                                    game.Phrases.DeepListMadnessHardKittyMilk.SendLog(player, false);

                                curr.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));
                            }

                        break;

                    case "Дракон":
                        if (game.RoundNo == 9)
                            player.Status.AddInGamePersonalLogs(
                                "Дракон: __**Бля, чо за хуйня со мной происходит!?**__\n");

                        if (game.RoundNo == 10)
                        {
                            player.GameCharacter.SetIntelligence(10, "Дракон");
                            player.GameCharacter.SetStrength(10, "Дракон");
                            player.GameCharacter.SetSpeed(10, "Дракон");
                            player.GameCharacter.SetPsyche(10, "Дракон");

                            //me.GameCharacter.AddExtraSkill((int)me.GameCharacter.GetSkill(), "Дракон");

                            var pointsToGive = player.GameCharacter.GetSkill() / 10;


                            var siri = player.Passives.SirinoksFriendsList;

                            if (siri != null)
                                for (var i = player.Status.GetPlaceAtLeaderBoard() + 1;
                                     i < game.PlayersList.Count + 1;
                                     i++)
                                {
                                    var player2 = game.PlayersList[i - 1];
                                    if (siri.FriendList.Contains(player2.GetPlayerId()))
                                        pointsToGive -= 1;
                                }

                            player.Status.AddBonusPoints(pointsToGive, "Дракон");
                            game.Phrases.SirinoksDragonPhrase.SendLog(player, true);
                        }

                        break;

                    case "Vampyr":
                        if (game.RoundNo == 1)
                        {
                            game.Phrases.VampyrVampyr.SendLog(player, true);
                            if (game.PlayersList.Any(x => x.GameCharacter.Name == "mylorik"))
                                game.AddGlobalLogs(
                                    " \n<:Y_:562885385395634196> *mylorik: Гребанный Вампур!* <:Y_:562885385395634196>",
                                    "\n\n");
                        }

                        break;

                    case "Огурчик Рик":
                        var pickleNext = player.Passives.RickPickle;
                        if (pickleNext.PickleTurnsRemaining > 0)
                        {
                            player.Status.WhoToAttackThisTurn = new List<Guid>();
                            player.Status.IsReady = true;
                        }
                        if (pickleNext.PenaltyTurnsRemaining > 0)
                        {
                            pickleNext.PenaltyTurnsRemaining--;
                            player.Status.IsSkip = true;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();
                            game.Phrases.RickPicklePenalty.SendLog(player, true);
                        }
                        break;

                    // Глаза Итачи: move this-round target to active target for next round
                    case "Глаза Итачи":
                        var tsukuyomiNext = player.Passives.ItachiTsukuyomi;
                        if (tsukuyomiNext.TsukuyomiTargetThisRound != Guid.Empty)
                        {
                            tsukuyomiNext.TsukuyomiActiveTarget = tsukuyomiNext.TsukuyomiTargetThisRound;
                            tsukuyomiNext.TsukuyomiTargetThisRound = Guid.Empty;
                        }
                        break;

                    // Глаз Шусуи: resurrect once if killed
                    case "Глаз Шусуи":
                        if (!player.Passives.ItachiShisuiUsed && player.Passives.IsDead)
                        {
                            player.Passives.IsDead = false;
                            player.Passives.DeathSource = "";
                            player.Passives.ItachiShisuiUsed = true;
                            player.Passives.AchievementTracker.WasRevived = true;
                            game.AddGlobalLogs($"**Изанаги!**\n**{player.GameCharacter.Name}** вернулся к жизни\n\"Я планировал приберечь глаз Шисуи для кое-чего другого... но ладно.\"");
                        }
                        break;

                    // Боги мне не указ: resurrect once if killed by a God (Kira)
                    case "Боги мне не указ":
                        if (!player.Passives.KratosGodSlayerUsed && player.Passives.IsDead && player.Passives.DeathSource == "Kira")
                        {
                            player.Passives.IsDead = false;
                            player.Passives.DeathSource = "";
                            player.Passives.KratosGodSlayerUsed = true;
                            player.Passives.AchievementTracker.WasRevived = true;
                            player.Passives.AchievementTracker.SurvivedKiraAttempt = true;
                            player.GameCharacter.AddExtraSkill(228, "Боги мне не указ");
                            game.AddGlobalLogs($"**{player.GameCharacter.Name}:** Боги мне не указ!");
                        }
                        break;

                    case "Впарить говна":
                        // Decrement cooldown
                        var sellerVNext = player.Passives.SellerVparitGovna;
                        sellerVNext.Cooldown = Math.Max(0, sellerVNext.Cooldown - 1);

                        // Decrement mark timers on all players
                        foreach (var marked in game.PlayersList)
                        {
                            if (marked.Passives.SellerVparitGovnaRoundsLeft > 0)
                            {
                                marked.Passives.SellerVparitGovnaRoundsLeft--;
                                if (marked.Passives.SellerVparitGovnaRoundsLeft <= 0)
                                {
                                    // Mark expired — remove temporary skill
                                    marked.GameCharacter.AddExtraSkill(
                                        -marked.Passives.SellerVparitGovnaTotalSkill, "Впарить говна", false);
                                    marked.Passives.SellerVparitGovnaTotalSkill = 0;

                                    // Collect siphoned skill into seller's box and stop siphoning
                                    var siphoned = marked.GameCharacter.SkillSiphonBox ?? 0;
                                    player.Passives.SellerSecretBuild.AccumulatedSkill += siphoned;
                                    marked.GameCharacter.SkillSiphonBox = null;

                                    // Clear outplay marks and forced loss flag
                                    marked.Passives.SellerOutplayTargets.Clear();
                                    marked.Passives.SellerForcedLossNextAttack = false;
                                }
                            }
                        }
                        break;

                    case "Секретный билд":
                        if (game.RoundNo == 10)
                        {
                            // Collect remaining active siphons from still-marked players
                            decimal totalSiphoned = player.Passives.SellerSecretBuild.AccumulatedSkill;
                            foreach (var marked in game.PlayersList)
                            {
                                if (marked.GameCharacter.SkillSiphonBox.HasValue)
                                {
                                    totalSiphoned += marked.GameCharacter.SkillSiphonBox.Value;
                                    marked.GameCharacter.SkillSiphonBox = null;
                                }
                            }

                            if (totalSiphoned > 0)
                            {
                                player.GameCharacter.AddExtraSkill(totalSiphoned, "Секретный билд", isLog:false);
                                player.Status.AddInGamePersonalLogs($"Пришло время играть по-настоящему. Мой секретный билд: +{totalSiphoned} Скилла\n");
                                game.Phrases.SellerSecretBuild.SendLog(player, false);
                            }
                        }
                        break;

                    case "Макро":
                        player.Passives.DopaMacro.FightsProcessed = 0;
                        break;

                    case "Get cancer":
                        player.Passives.ToxicMateCancer.TransferredThisRound = false;
                        break;

                    case "Взгляд в будущее":
                        if (player.Passives.DopaVision.Cooldown > 0)
                        {
                            player.Passives.DopaVision.Cooldown--;
                            if (player.Passives.DopaVision.Cooldown == 0)
                                game.Phrases.DopaVisionReady.SendLog(player, false);
                        }
                        break;

                    case "Законодатель меты":
                        if (game.RoundNo == 1 && player.IsBot() && !player.Passives.DopaMetaChoice.Triggered)
                        {
                            var tactics = new[] { "Стомп", "Фарм", "Доминация", "Роум" };
                            var chosen = tactics[_rand.Random(0, 3)];
                            ApplyDopaChoice(player, game, chosen);
                        }
                        break;

                    // Монстр без имени — Выдуманный персонаж: round 9 → mark pawns + bonus
                    case "Выдуманный персонаж":
                        if (game.RoundNo == 9)
                        {
                            // Monster's correct predictions → those players become Johan's pawns
                            foreach (var prediction in player.Predict)
                            {
                                var predTarget = game.PlayersList.Find(x => x.GetPlayerId() == prediction.PlayerId);
                                if (predTarget != null &&
                                    string.Equals(predTarget.GameCharacter.Name, prediction.CharacterName, StringComparison.OrdinalIgnoreCase))
                                {
                                    predTarget.Passives.IsJohanPawn = true;
                                    predTarget.Passives.JohanPawnOwnerId = player.GetPlayerId();
                                    predTarget.Status.AddInGamePersonalLogs("Ты стал пешкой Йохана...\n");
                                }
                            }

                            // If anyone tried to guess Monster's identity → +3 bonus
                            var anyTriedToGuessMonster = game.PlayersList.Any(x =>
                                x.GetPlayerId() != player.GetPlayerId() &&
                                x.Predict.Any(p => p.PlayerId == player.GetPlayerId()));

                            if (anyTriedToGuessMonster)
                            {
                                player.Status.AddBonusPoints(3, "Выдуманный персонаж");
                                game.AddGlobalLogs("Я бы хотел найти того, кто во всём виноват... Но у Монстра нет имени.");
                            }
                        }
                        break;

                    // Монстр без имени — Пейзаж конца света: round 10 warning
                    case "Пейзаж конца света":
                        if (game.RoundNo == 10)
                        {
                            game.AddGlobalLogs("Йохан: Я позволю вам узреть \"Пейзаж конца света\", доктор Тэнма.");
                            game.AddGlobalLogs("**Если вы нападете на Йохана, получите победу.**");
                            var tenmaMsg = "Тэнма: ~~Нет! Глупцы! Если он знает вашу личность... Не делайте этого! Пропустите ход!~~";
                            game.AddGlobalLogs(tenmaMsg);
                            // Тэнма's message disappears after ~10 seconds
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(10_000);
                                game.HiddenGlobalLogSnippets.Add(tenmaMsg);
                            });
                        }
                        break;

                    case "Монстр":
                        foreach (var p in game.PlayersList)
                            p.Passives.MonsterNoEscape = false;
                        break;

                    // TheBoys — Заказ Француза: manage orders on odd rounds (3, 5, 7, 9)
                    case "Заказ Француза":
                        var francieNR = player.Passives.TheBoysFrancie;
                        if (game.RoundNo is 3 or 5 or 7 or 9)
                        {
                            // Fail current order if still active
                            if (francieNR.OrderTarget != Guid.Empty && francieNR.OrderRoundsLeft > 0)
                            {
                                francieNR.OrdersFailed++;
                                player.Status.AddBonusPoints(-1, "Заказ Француза");
                                game.Phrases.TheBoysOrderFailed.SendLog(player, false);
                                francieNR.OrderTarget = Guid.Empty;
                                francieNR.OrderRoundsLeft = 0;
                            }
                            // Assign new order
                            if (francieNR.RemainingTargets.Count > 0)
                            {
                                francieNR.OrderTarget = francieNR.RemainingTargets[0];
                                francieNR.RemainingTargets.RemoveAt(0);
                                francieNR.OrderHistory.Add(francieNR.OrderTarget);
                                francieNR.OrderRoundsLeft = 2;
                                var orderTargetName = game.PlayersList.Find(x => x.GetPlayerId() == francieNR.OrderTarget)?.DiscordUsername ?? "???";
                                player.Status.AddInGamePersonalLogs($"Заказ Француза: Новая цель — {orderTargetName}. 2 хода.\n");
                                game.Phrases.TheBoysOrderNew.SendLog(player, false);
                            }
                        }
                        else if (francieNR.OrderRoundsLeft > 0)
                        {
                            francieNR.OrderRoundsLeft--;
                        }
                        break;

                    // TheBoys — Регенерация Кимико: recovery/disable state management
                    case "Регенерация Кимико":
                        var kimikoNR = player.Passives.TheBoysKimiko;
                        if (kimikoNR.DisabledNextRound)
                        {
                            kimikoNR.IsDisabled = true;
                            kimikoNR.DisabledNextRound = false;
                        }
                        else if (kimikoNR.IsDisabled)
                        {
                            kimikoNR.IsDisabled = false;
                            game.Phrases.TheBoysKimikoRecovered.SendLog(player, false);
                        }
                        break;

                    // Геральт — Ведьмачий Заказ: spawn contracts each round
                    case "Ведьмачий Заказ":
                        if (player.GameCharacter.Name == "Геральт" && game.RoundNo > 1)
                        {
                            var geraltNrContracts = player.Passives.GeraltContracts;
                            var monsterTypes = new[] { "Утопец", "Гуль", "Грифон", "Виверна", "Кикимора", "Лютоволк", "Вампур", "Дракон", "Бес", "Леший" };
                            var enemies = game.PlayersList.Where(x =>
                                x.GetPlayerId() != player.GetPlayerId() && !x.Passives.IsDead).ToList();
                            if (enemies.Count > 0)
                            {
                                var randomEnemy = enemies[_rand.Random(0, enemies.Count - 1)];
                                var randomMonster = monsterTypes[_rand.Random(0, monsterTypes.Length - 1)];
                                var enemyId = randomEnemy.GetPlayerId();
                                if (!geraltNrContracts.ContractMap.ContainsKey(enemyId))
                                    geraltNrContracts.ContractMap[enemyId] = new List<string>();
                                geraltNrContracts.ContractMap[enemyId].Add(randomMonster);
                                randomEnemy.Passives.GeraltContractsOnMe.Add(randomMonster);
                                randomEnemy.Passives.GeraltContractOwnerId = player.GetPlayerId();
                                game.Phrases.GeraltContractSpawn.SendLog(player, false);

                                // Bonus chance for extra contract on same enemy: 5%/10%/15% based on existing count
                                var existingCount = geraltNrContracts.ContractMap[enemyId].Count;
                                var bonusChance = Math.Min(existingCount * 5, 15);
                                if (_rand.Luck(bonusChance))
                                {
                                    var bonusMonster = monsterTypes[_rand.Random(0, monsterTypes.Length - 1)];
                                    geraltNrContracts.ContractMap[enemyId].Add(bonusMonster);
                                    randomEnemy.Passives.GeraltContractsOnMe.Add(bonusMonster);
                                    game.Phrases.GeraltMultiContract.SendLog(player, false);
                                }
                            }
                        }
                        break;

                    // Геральт — Медитация: reset per-round state, roll Lambert mixup
                    case "Медитация":
                        if (player.GameCharacter.Name == "Геральт")
                        {
                            var geraltNrMed = player.Passives.GeraltContracts;
                            geraltNrMed.IsMeditating = false;
                            geraltNrMed.WasAttackedDuringMeditation = false;
                            geraltNrMed.OilsActivated = false;
                            geraltNrMed.CurrentRoundFightQueue.Clear();
                            geraltNrMed.LastOilBonusCount = 0;
                            geraltNrMed.LambertMixup = _rand.Luck(10);
                        }
                        break;
                }

            //Я за чаем
            var isSkip = player.Passives.GlebTeaTriggeredWhen;

            var hasPortalGun = player.GameCharacter.Passive.Any(x => x.PassiveName == "Портальная пушка") &&
                player.Passives.RickPortalGun.Invented && player.Passives.RickPortalGun.Charges > 0;

            if (isSkip.WhenToTrigger.Contains(game.RoundNo) && !hasPortalGun)
            {
                player.Status.IsSkip = true;
                player.Status.ConfirmedSkip = false;
                player.Status.IsBlock = false;
                player.Status.IsReady = true;
                player.Status.WhoToAttackThisTurn = new List<Guid>();
                player.Status.AddInGamePersonalLogs("Тебя усыпили...\n");
            }
            //end Я за чаем
        }

        // Таинственный Суппорт — "Premade": prevent marked player from skipping
        foreach (var supporter in game.PlayersList)
        {
            if (!supporter.GameCharacter.Passive.Any(x => x.PassiveName == "Premade")) continue;
            var markedId = supporter.Passives.SupportPremade.MarkedPlayerId;
            if (markedId == Guid.Empty) continue;
            var marked = game.PlayersList.Find(x => x.GetPlayerId() == markedId);
            if (marked != null && marked.Status.IsSkip && !marked.Status.ConfirmedSkip)
            {
                marked.Status.IsSkip = false;
                marked.Status.IsReady = false;
                game.Phrases.SupportPremadeAntiSkip.SendLog(supporter, false);
            }
        }

        // Salldorum — Временная капсула: auto-pickup check
        foreach (var player in game.PlayersList)
        {
            if (player.GameCharacter.Name != "Salldorum") continue;
            var capsule = player.Passives.SalldorumTimeCapsule;
            if (capsule.Buried && !capsule.PickedUpThisTurn)
            {
                var currentPos = player.Status.GetPlaceAtLeaderBoard();
                if (currentPos == capsule.BuriedAtPosition && (game.RoundNo - capsule.BuriedOnRound) >= 3)
                {
                    player.FightCharacter.AddSpeedForOneFight(5);
                    player.Status.AddBonusPoints(2, "Временная капсула");
                    capsule.PickedUpThisTurn = true;
                    game.Phrases.SalldorumTimeCapsulePickup.SendLog(player, false);
                }
            }
            // Reset pickup flag from previous round
            if (capsule.PickedUpThisTurn && game.RoundNo > capsule.BuriedOnRound + 3)
                capsule.PickedUpThisTurn = false;
        }
    }






    public void ApplyDopaChoice(GamePlayerBridgeClass player, GameClass game, string tactic)
    {
        player.Passives.DopaMetaChoice.Triggered = true;
        player.Passives.DopaMetaChoice.ChosenTactic = tactic;

        var allTactics = new[] { "Стомп", "Фарм", "Доминация", "Роум" };
        foreach (var t in allTactics.Where(t => t != tactic))
            player.GameCharacter.Passive.RemoveAll(x => x.PassiveName == t);
        player.GameCharacter.Passive.RemoveAll(x => x.PassiveName == "Законодатель меты");

        if (tactic == "Стомп")
        {
            player.GameCharacter.AddStrength(9, "Стомп");
            player.GameCharacter.AddExtraSkill(99, "Стомп");
        }

        game.Phrases.DopaMetaChosen.SendLog(player, false);
        player.Status.AddInGamePersonalLogs($"Тактика выбрана: {tactic}\n");
    }

    public void HandleNextRoundAfterSorting(GameClass game)
    {
        foreach (var player in game.PlayersList)
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Weed":
                    var diff = game.RoundNo - player.Passives.WeedwickLastRoundWeed;
                    if (diff >= 2)
                    {
                        game.Phrases.WeedwickWeedNo.SendLog(player, false);
                        player.MinusPsycheLog(player.GameCharacter, game, -1, "Weed");
                    }

                    break;

                case "Булькает":
                    if (player.Status.GetPlaceAtLeaderBoard() != 1)
                        player.GameCharacter.Justice.AddRealJusticeNow();
                    break;

                case "Челюсти":
                    if (game.RoundNo > 1)
                    {
                        var shark = player.Passives.SharkJawsLeader;


                        if (!shark.FriendList.Contains(player.Status.GetPlaceAtLeaderBoard()))
                        {
                            shark.FriendList.Add(player.Status.GetPlaceAtLeaderBoard());
                            player.GameCharacter.AddSpeed(1, "Челюсти");
                        }
                    }

                    break;

                case "Тигр топ, а ты холоп":
                    if (player.Status.GetPlaceAtLeaderBoard() == 1 && game.RoundNo is > 1 and < 10)
                    {
                        player.GameCharacter.AddPsyche(1, "Тигр топ, а ты холоп");
                        player.GameCharacter.AddMoral(3, "Тигр топ, а ты холоп");
                        game.Phrases.TigrTop.SendLog(player, false);
                    }

                    break;

                case "Много выебывается":
                    if (player.Status.GetPlaceAtLeaderBoard() == 1)
                    {
                        player.Status.AddRegularPoints(1, "Много выебывается");
                        game.Phrases.MitsukiTooMuchFucking.SendLog(player, false);
                    }

                    break;

                case "Запах мусора":
                    if (game.RoundNo == 11)
                    {
                        var mitsuki = player.Passives.MitsukiGarbageList;
                        if (mitsuki != null)
                        {
                            var count = 0;
                            foreach (var t in mitsuki.Training.Where(x => x.Times >= 2))
                            {
                                var player2 = game.PlayersList.Find(x => x.GetPlayerId() == t.EnemyId);
                                if (player2 != null)
                                {
                                    player2.Status.AddBonusPoints(-5, "Запах мусора");

                                    game.Phrases.MitsukiGarbageSmell.SendLog(player2, true);
                                    count++;
                                }
                            }

                            game.AddGlobalLogs($"Mitsuki отнял в общей сумме {count * 5} очков.");
                        }
                    }

                    break;

                case "Раскинуть щупальца":
                    if (game.RoundNo > 1)
                    {
                        var octo = player.Passives.OctopusTentaclesList;
                        if (!octo.LeaderboardPlace.Contains(player.Status.GetPlaceAtLeaderBoard()))
                        {
                            octo.LeaderboardPlace.Add(player.Status.GetPlaceAtLeaderBoard());
                            player.Status.AddRegularPoints(1, "Раскинуть щупальца");
                        }
                    }

                    break;

                case "Никому не нужен":
                    if (game.RoundNo is 9 or 7 or 5 or 3)
                    {
                        var hardKitty = player.Passives.HardKittyDoebatsya;
                        foreach (var target in game.PlayersList)
                        {
                            if (player.GetPlayerId() == target.GetPlayerId()) continue;
                            var found = hardKitty.LostSeriesCurrent.Find(x => x.EnemyPlayerId == target.GetPlayerId());

                            if (found != null)
                                found.Series++;
                            else
                                hardKitty.LostSeriesCurrent.Add(new HardKitty.DoebatsyaSubClass(target.GetPlayerId()));
                        }
                    }

                    break;

                case "Не повезло":
                    var darksciType = player.Passives.DarksciTypeList;

                    if (darksciType.IsStableType)
                    {
                        player.GameCharacter.AddExtraSkill(20, "Не повезло");
                        player.GameCharacter.AddMoral(2, "Не повезло");
                    }

                    break;

                case "Дизмораль":

                    if (game.RoundNo == 9)
                    {
                        //Дизмораль Part #1
                        game.Phrases.DarksciDysmoral.SendLog(player, true);
                        game.AddGlobalLogs($"{player.DiscordUsername}: Всё, у меня горит!");
                        //end Дизмораль Part #2
                    }


                    /*
                       _        _
                      ( `-.__.-' )
                       `-.    .-'
                          \  /
                           ||
                           ||
                          //\\
                         //  \\
                        ||    ||
                        ||____||
                        ||====||
                         \\  //
                          \\//
                           ||
                           ||
                           ||
                           ||
                           ||
                           ||
                           ||
                           ||
                           []
                    */
                    //Да всё нахуй эту игру (3, 6 and 9 are in LVL up): Part #3
                    if (game.RoundNo == 9 && player.GameCharacter.GetPsyche() < 4)
                        if (!game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                            game.AddGlobalLogs($"{player.DiscordUsername}: Нахуй эту игру..");


                    //end Да всё нахуй эту игру: Part #3
                    //Да всё нахуй эту игру (3, 6 and 9 are in LVL up): Part #1
                    if (game.RoundNo != 9 && game.RoundNo != 7 && game.RoundNo != 5 && game.RoundNo != 3)
                        if (player.GameCharacter.GetPsyche() <= 0)
                        {
                            player.Status.IsSkip = true;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();
                            game.Phrases.DarksciFuckThisGame.SendLog(player, true);

                            if (game.RoundNo == 10 && !game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                                game.AddGlobalLogs(
                                    $"{player.DiscordUsername}: Нахуй эту игру..");
                        }

                    //end Да всё нахуй эту игру: Part #1
                    break;


                case "Подсчет":
                    var tolya = player.Passives.TolyaCount;

                    tolya.Cooldown--;

                    if (tolya.Cooldown <= 0)
                    {
                        tolya.IsReadyToUse = true;
                        game.Phrases.TolyaCountReadyPhrase.SendLog(player, false);
                    }

                    if (tolya.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1))
                    {
                        var targetTolya = game.PlayersList.Find(x =>
                                x.GetPlayerId() == tolya.TargetList.Find(y => y.RoundNumber == game.RoundNo - 1)!
                                    .Target)
                            .DiscordUsername;
                        player.Status.AddInGamePersonalLogs(
                            $"Подсчет: __Ставлю на то, что {targetTolya} получит пизды!__\n");
                    }
                    break;

                case "Спокойствие":
                    var yongGleb = player.Passives.YongGlebTea;
                    yongGleb.Cooldown--;

                    if (yongGleb.Cooldown <= 0)
                    {
                        yongGleb.IsReadyToUse = true;
                        game.Phrases.YongGlebTeaReady.SendLog(player, true);
                    }
                    break;

                case "Тупорылая Акула":
                    if (player.GameCharacter.GetPsyche() == 10 && !player.IsBot())
                    {
                        player.GameCharacter.Name = "Братишка";
                        player.GameCharacter.Passive = new List<Passive>();
                        player.GameCharacter.Passive = _charactersPull.GetRollableCharacters().Find(x => x.Name == "Братишка").Passive;
                        player.Status.AddInGamePersonalLogs("Братишка: **Буууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууль**\n");
                    }

                    break;

                case "Огурчик Рик":
                    if (player.Passives.RickPickle.PickleTurnsRemaining > 0)
                    {
                        player.Status.MoveListPage = 1;
                        player.Status.IsReady = true;
                    }
                    break;

                case "Гоблины":
                    // Auto-grow goblins each round
                    var gobEndPop = player.Passives.GoblinPopulation;
                    var autoGrowth = gobEndPop.GrowthThisRound;
                    gobEndPop.TotalGoblins += autoGrowth;
                    // Update persistent stat bonuses based on new population
                    player.GameCharacter.SetStrength(gobEndPop.Warriors, "Гоблины");
                    player.GameCharacter.SetIntelligence(gobEndPop.Hobs, "Гоблины");
                    player.GameCharacter.SetPsyche(5 + gobEndPop.Hobs, "Гоблины");
                    player.Status.AddInGamePersonalLogs($"Гоблины: +{autoGrowth} прирост. Всего: {gobEndPop.TotalGoblins} (⚔️{gobEndPop.Warriors} 🧙{gobEndPop.Hobs} ⛏️{gobEndPop.Workers})\n");
                    break;

                case "Отличный рудник":
                    // Mine income moved to HandleEndOfRound (uses pre-sort position)
                    break;

                case "Гоблины тупые, но не идиоты":
                    var gobZigEnd = player.Passives.GoblinZiggurat;
                    var placeEnd = player.Status.GetPlaceAtLeaderBoard();

                    // Build ziggurat on block (WantsToBuild is set in HandleEndOfRound where IsBlock is still true)
                    if (gobZigEnd.WantsToBuild)
                    {
                        var zigPop = player.Passives.GoblinPopulation;

                        if (zigPop.Warriors < 1 || zigPop.Hobs < 1 || zigPop.Workers < 1)
                        {
                            game.Phrases.GoblinZigguratNoMoney.SendLog(player, false);
                        }
                        else if (player.Status.GetScore() < 3)
                        {
                            game.Phrases.GoblinZigguratNoMoney.SendLog(player, false);
                        }
                        else if (gobZigEnd.BuiltPositions.Contains(placeEnd))
                        {
                            player.Status.AddInGamePersonalLogs("Зиккурат уже построен на этом месте!\n");
                        }
                        else
                        {
                            player.Status.AddBonusPoints(-3, "Гоблины тупые, но не идиоты");
                            zigPop.ZigguratWorkerDeductions++;
                            game.Phrases.GoblinZigguratWorkerDeath.SendLog(player, false);
                            player.Status.AddInGamePersonalLogs($"Зиккурат: -1 трудяга. Трудяг осталось: {zigPop.Workers}\n");

                            gobZigEnd.BuiltPositions.Add(placeEnd);
                            gobZigEnd.IsInZiggurat = true;
                            gobZigEnd.ZigguratStayRoundsLeft = 1;

                            var lastAttacked = game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.GoblinLastAttackedPlayer);
                            var enemyPassives = lastAttacked?.GameCharacter.Passive ?? new List<Passive>();
                            var standalonePassives = enemyPassives
                                .Where(p => p.Standalone && !gobZigEnd.LearnedPassives.Contains(p.PassiveName)
                                    && player.GameCharacter.Passive.All(x => x.PassiveName != p.PassiveName))
                                .ToList();

                            if (standalonePassives.Count > 0)
                            {
                                var learnedPassive = standalonePassives[_rand.Random(0, standalonePassives.Count - 1)];
                                gobZigEnd.LearnedPassives.Add(learnedPassive.PassiveName);
                                player.GameCharacter.Passive.Add(learnedPassive.DeepCopy());
                                player.Status.AddInGamePersonalLogs($"Отлично! Гоблины постарались как следует и научились производить: {learnedPassive.PassiveName}\n");
                            }

                            game.Phrases.GoblinZigguratBuild.SendLog(player, false);
                            player.Status.AddInGamePersonalLogs($"Зиккурат построен на месте {placeEnd}! Позиция защищена.\n");
                        }
                    }
                    gobZigEnd.WantsToBuild = false;

                    // Check if current position has a built ziggurat
                    if (gobZigEnd.BuiltPositions.Contains(placeEnd))
                    {
                        gobZigEnd.IsInZiggurat = true;
                        gobZigEnd.ZigguratStayRoundsLeft = 1;
                    }
                    else
                    {
                        gobZigEnd.IsInZiggurat = false;
                        gobZigEnd.ZigguratStayRoundsLeft = 0;
                    }

                    // Ziggurat grants Justice and Moral each round
                    if (gobZigEnd.IsInZiggurat)
                    {
                        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(1);
                        player.GameCharacter.AddMoral(5, "Зиккурат");
                    }
                    break;

                // Salldorum — Шэн: position swap after sorting
                case "Шэн":
                    if (player.GameCharacter.Name == "Salldorum")
                    {
                        var shenState = player.Passives.SalldorumShen;
                        if (shenState.ActiveThisTurn && shenState.TargetPosition >= 1 && shenState.TargetPosition <= game.PlayersList.Count)
                        {
                            var targetIdx = shenState.TargetPosition - 1;
                            var salloIdx = game.PlayersList.IndexOf(player);
                            if (salloIdx != targetIdx)
                            {
                                // Check Ziggurat protection
                                if (game.PlayersList[targetIdx].Passives.GoblinZiggurat.IsInZiggurat)
                                {
                                    player.Status.AddInGamePersonalLogs("Шэн: Зиккурат защищает эту позицию!\n");
                                }
                                else
                                {
                                    var displaced = game.PlayersList[targetIdx];
                                    game.PlayersList[targetIdx] = player;
                                    game.PlayersList[salloIdx] = displaced;
                                    game.Phrases.SalldorumShen.SendLog(player, false);
                                }
                            }
                        }
                    }
                    break;

                // Геральт — Детектив: ~33% chance per round, give witcher hint about unhinted enemy
                case "Детектив":
                    if (player.GameCharacter.Name == "Геральт" && _rand.Luck(33))
                    {
                        var geraltDetective = player.Passives.GeraltDetective;
                        var unhinted = game.PlayersList.Where(x =>
                            x.GetPlayerId() != player.GetPlayerId() &&
                            !x.Passives.IsDead &&
                            !geraltDetective.HintedPlayers.Contains(x.GetPlayerId())).ToList();

                        if (unhinted.Count > 0)
                        {
                            var hintTarget = unhinted[_rand.Random(0, unhinted.Count - 1)];
                            geraltDetective.HintedPlayers.Add(hintTarget.GetPlayerId());

                            var hint = hintTarget.GameCharacter.Name switch
                            {
                                "Weedwick" => "Волчьи следы... Ведут в поле конопли...",
                                "Вампур" => "Следы клыков на шее. Высший вампир?",
                                "Sirinoks" => "Чешуя дракона. Огромные крылья.",
                                "Стая Гоблинов" => "Маленькие следы. Много. Очень много.",
                                "DeepList" => "Этот... слишком умный. Опасно.",
                                "mylorik" => "Буйный воин. Жаждет мести.",
                                "Глеб" => "Спит? Или притворяется?",
                                "Тигр" => "Зверь на вершине. Территориальный.",
                                "Толя" => "Бронированная тварь. Сам не нападёт.",
                                "Осьминожка" => "Щупальца повсюду. Неуязвим.",
                                "HardKitty" => "Одиночка. Не трогай — не тронет.",
                                "LeCrisp" => "Ассасин в тенях. Быстрый.",
                                "Кратос" => "Бог войны. Убивает богов.",
                                "Кира" => "Тетрадь... Пишет имена. Опасно.",
                                "Итачи" => "Шаринган. Не смотри в глаза.",
                                "Котики" => "Коты... повсюду коты.",
                                "Dopa" => "Анализирует. Адаптируется. Побеждает.",
                                "Наполеон" => "Стратег. Строит альянсы.",
                                _ => $"Что-то странное. Неизвестный зверь."
                            };
                            game.Phrases.GeraltDetective.SendLog(player, false);
                            player.Status.AddInGamePersonalLogs($"Детектив: {hint} ({hintTarget.DiscordUsername})\n");
                        }
                    }
                    break;
            }
    }
    //end after all fight


    //predict bot
    public List<string> GetCharactersBasedOnClassAndRound(string characterClass, int round)
    {
        //Умный => Сильный => Быстрый
        var characters = new List<string>();
        switch (characterClass)
        {
            case "(**Умный** ?) ":
                characters = new List<string> { "DeepList", "Глеб", "LeCrisp", "Толя" };
                switch (round)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                        characters = new List<string> { "DeepList", "Глеб", "Толя" };
                        break;
                    case 6:
                        characters = new List<string> { "DeepList", "Глеб", "Толя" };
                        break;
                    case 7:
                        characters = new List<string> { "DeepList", "Толя" };
                        break;
                    case 8:
                        characters = new List<string> { "DeepList", "Толя" };
                        break;
                }

                break;
            case "(**Сильный** ?) ":
                characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске" };
                switch (round)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                        characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске", "LeCrisp" };
                        break;
                    case 6:
                        characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске", "LeCrisp" };
                        break;
                    case 7:
                        characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске", "LeCrisp",  "Глеб" };
                        break;
                    case 8:
                        characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске", "LeCrisp", "Глеб" };
                        break;
                }

                break;
            case "(**Быстрый** ?) ":
                characters = new List<string> { "mylorik", "Осьминожка", "Darksci", "Братишка", "Краборак" };
                switch (round)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                        break;
                }

                break;
        }

        characters.Add("Sirinoks");
        characters.Add("Злой Школьник");
        characters.Add("AWDKA");
        characters.Add("Вампур");
        characters.Add("Итачи");


        return characters;
    }

    [SuppressMessage("ReSharper", "UnusedVariable")]
    public void HandleBotPredict(GameClass game)
    {
        //
        foreach (var player in game.PlayersList)
            try
            {
                if (!player.IsBot()) continue;
                if (game.RoundNo >= 9) continue;
                // Kira uses Death Note, not predictions
                if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Тетрадь смерти")) continue;

                var splitLogs = player.Status.InGamePersonalLogsAll.Split("|||");

                var lastRoundEvents = "";
                if (splitLogs.Length > 1 && splitLogs[^2].Length > 3 && game.RoundNo > 1)
                    lastRoundEvents = splitLogs[^2];
                var personalLogs = player.Status.GetInGamePersonalLogs();
                var globalLogs = game.GetGlobalLogs();
                var leaderboard = _gameUpdateMess.LeaderBoard(player);
                var knownCLass = player.Status.KnownPlayerClass;


                switch (player.GameCharacter.Name)
                {
                    case "AWDKA":
                        try
                        {
                            if (lastRoundEvents.Contains("напал на игрока"))
                            {
                                var playerName = lastRoundEvents.Split("напал на игрока")[1].Split("\n")[0].TrimStart();
                                var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);

                                if (player.GameCharacter.GetIntelligenceString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.GameCharacter.GetIntelligenceString()
                                        .Replace("Интеллект ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("DeepList",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Злой Школьник",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Толя", playerClass.GetPlayerId()));
                                            break;
                                        case 7:
                                            break;
                                        case 6:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(
                                                    new PredictClass("Вампур", playerClass.GetPlayerId()));
                                            break;
                                        case 5:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Sirinoks", playerClass.GetPlayerId()));
                                            break;
                                    }
                                }

                                if (player.GameCharacter.GetStrengthString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.GameCharacter.GetStrengthString()
                                        .Replace("Сила ", "")
                                        .Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Загадочный Спартанец в маске",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Тигр", playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            break;
                                        case 7:
                                            break;
                                        case 6:
                                            break;
                                        case 5:
                                            break;
                                    }
                                }

                                if (player.GameCharacter.GetSpeedString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.GameCharacter.GetSpeedString()
                                        .Replace("Скорость ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Краборак",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("mylorik",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Darksci",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 7:
                                            break;
                                        case 6:
                                            break;
                                        case 5:
                                            break;
                                    }
                                }

                                if (player.GameCharacter.GetPsycheString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.GameCharacter.GetPsycheString()
                                        .Replace("Психика ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:

                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Осьминожка",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Краборак",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()) &&
                                                playerClass.Status.GetPlaceAtLeaderBoard() == 6)
                                                player.Predict.Add(new PredictClass("HardKitty",
                                                    playerClass.GetPlayerId()));
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Глеб", playerClass.GetPlayerId()));
                                            break;
                                        case 7:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("LeCrisp",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 6:

                                            break;
                                        case 5:
                                            break;
                                    }
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            _log.Critical(exception.Message);
                            _log.Critical(exception.StackTrace);
                        }

                        break;
                    case "DeepList":
                        var deepList = player.Passives.DeepListSupermindKnown;

                        if (deepList != null)
                            foreach (var knownPlayer in deepList.KnownPlayers)
                            {
                                var playerClass = game.PlayersList.Find(x => x.GetPlayerId() == knownPlayer);

                                if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()) &&
                                    playerClass.GetPlayerId() != player.GetPlayerId())
                                    player.Predict.Add(new PredictClass(playerClass.GameCharacter.Name,
                                        playerClass.GetPlayerId()));
                            }

                        break;
                }

                //game.AddGlobalLogs($"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {randomPlayer.GameCharacter.Name}");
                //100%
                try
                {
                    if (globalLogs.Contains("Толя запизделся"))
                    {
                        var playerName =
                            globalLogs.Split("запизделся и спалил")[1].Replace(", что ", "").Split(" - ")[^2];
                        var playerCharacter =
                            globalLogs.Split("запизделся и спалил")[1].Replace(", что ", "").Split(" - ")[^1]
                                .Replace("\n", "");
                        var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);
                        if (playerClass.GetPlayerId() != player.GetPlayerId())
                        {
                            if (player.Predict.Any(x => x.PlayerId == playerClass.GetPlayerId()))
                                player.Predict.Remove(player.Predict.Find(x =>
                                    x.PlayerId == playerClass.GetPlayerId()));
                            player.Predict.Add(new PredictClass(playerCharacter, playerClass.GetPlayerId()));
                        }
                    }
                }
                catch
                {
                    //ignored
                }

                //100%
                try
                {
                    if (lastRoundEvents.Contains("Ничего не понимает"))
                    {
                        var playerName = lastRoundEvents.Split(" напал на игрока ")[1].Split("\n")[0];
                        var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);

                        if (player.Predict.Any(x => x.PlayerId == playerClass!.GetPlayerId()))
                            player.Predict.Remove(player.Predict.Find(x => x.PlayerId == playerClass!.GetPlayerId()));
                        player.Predict.Add(new PredictClass("Братишка", playerClass.GetPlayerId()));
                    }
                }
                catch
                {
                    //ignored
                }

                //not 100%
                try
                {
                    if (lastRoundEvents.Contains("Они позорят военное искусство"))
                    {
                        var removedTimes = 0;
                        foreach (var line in globalLogs.Split("\n"))
                        {
                            if (!line.Contains("⟶")) continue;
                            if (!line.Contains(player.DiscordUsername)) continue;
                            string playerName;
                            if (lastRoundEvents.Contains(" напал на игрока "))
                            {
                                playerName = lastRoundEvents.Split(" напал на игрока ")[1].Split("\n")[0];
                                if (line.Contains(playerName) && removedTimes == 0)
                                {
                                    removedTimes++;
                                    continue;
                                }
                            }

                            playerName = line.Split("  ⟶")[0].Replace($"{player.DiscordUsername}  ", "")
                                .Replace($" {player.DiscordUsername}", "").Replace("<:war:561287719838547981>", "")
                                .Trim();
                            var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);
                            if (playerClass != null)
                                if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()) &&
                                    playerClass.GetPlayerId() != player.GetPlayerId())
                                    player.Predict.Add(new PredictClass("Загадочный Спартанец в маске", playerClass.GetPlayerId()));
                        }
                    }
                }
                catch
                {
                    //ignored
                }


                //not 100%
                try
                {
                    if (lastRoundEvents.Contains("Стёб"))
                    {
                        var removedTimes = 0;
                        foreach (var line in globalLogs.Split("\n"))
                        {
                            if (!line.Contains("⟶")) continue;
                            if (!line.Contains(player.DiscordUsername)) continue;
                            string playerName;
                            if (lastRoundEvents.Contains(" напал на игрока "))
                            {
                                playerName = lastRoundEvents.Split(" напал на игрока ")[1].Split("\n")[0];
                                if (line.Contains(playerName) && removedTimes == 0)
                                {
                                    removedTimes++;
                                    continue;
                                }
                            }

                            playerName = line.Split("  ⟶")[0].Replace($"{player.DiscordUsername}  ", "")
                                .Replace($" {player.DiscordUsername}", "").Replace("<:war:561287719838547981>", "")
                                .Trim();
                            var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);

                            if(playerClass != null)
                                if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()) && playerClass.GetPlayerId() != player.GetPlayerId())
                                    player.Predict.Add(new PredictClass("DeepList", playerClass.GetPlayerId()));
                        }
                    }
                }
                catch
                {
                    //ignored
                }
            }
            catch (Exception exception)
            {
                _log.Critical(exception.Message);
                _log.Critical(exception.StackTrace);
            }
    }
    //end predict bot


    //unique
    public void HandleShark(GameClass game)
    {
        foreach (var player in game.PlayersList)
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Лежит на дне":
                    var enemyTop = game.PlayersList.Find(x =>
                        x.Status.GetPlaceAtLeaderBoard() - 1 == player.Status.GetPlaceAtLeaderBoard());
                    var enemyBottom = game.PlayersList.Find(x =>
                        x.Status.GetPlaceAtLeaderBoard() + 1 == player.Status.GetPlaceAtLeaderBoard());
                    if (enemyTop != null && enemyTop.Status.IsLostThisCalculation != Guid.Empty)
                        player.Status.AddRegularPoints(1, "Лежит на дне");

                    if (enemyBottom != null && enemyBottom.Status.IsLostThisCalculation != Guid.Empty)
                        player.Status.AddRegularPoints(1, "Лежит на дне");
                    break;
            }
    }

    public async Task<int> HandleJews(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
    {
        var jews = new List<GamePlayerBridgeClass>();
        var toReturn = 1;

        if (me.GameCharacter.Passive.Any(x => x.PassiveName == "Еврей")) return toReturn;
        if (me.GameCharacter.Passive.Any(x => x.PassiveName == "Вступить в союз")) return toReturn;

        foreach (var player in game.PlayersList)
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Еврей":
                    if (player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                        jews.Add(player);
                    break;
            }

        switch (jews.Count)
        {
            case 0:
                return toReturn;
            default:
                //1 jews or more!
                foreach (var jew in jews)
                {
                    if (me.GameCharacter.Name == "DeepList" && jew.GameCharacter.Name == "LeCrisp")
                    {
                        game.Phrases.LeCrispBoolingPhrase.SendLog(jew, false);
                        continue;
                    }

                    jew.Status.AddRegularPoints(1, "Еврей");
                    switch (jew.GameCharacter.Name)
                    {
                        case "Толя":
                            game.Phrases.TolyaJewPhrase.SendLog(jew, true);
                            break;

                        case "LeCrisp":
                            game.Phrases.LeCrispJewPhrase.SendLog(jew, true);
                            break;

                        default:
                            foreach (var player in game.PlayersList)
                                switch (player.GameCharacter.Name)
                                {
                                    case "Толя":
                                        game.Phrases.TolyaJewPhrase.SendLog(jew, true);
                                        break;

                                    case "LeCrisp":
                                        game.Phrases.LeCrispJewPhrase.SendLog(jew, true);
                                        break;
                                }

                            break;
                    }

                    if (jews.Count > 1 && !jew.IsBot())
                        try
                        {
                            await _help.SendMsgAndDeleteItAfterRound(jew, "__**МЫ**__ жрём деньги!", 10000);
                        }
                        catch (Exception exception)
                        {
                            _log.Critical(exception.Message);
                            _log.Critical(exception.StackTrace);
                        }

                    toReturn = 0;
                }

                break;
        }

        return toReturn;
    }

    public async Task<int> HandleOctopus(GamePlayerBridgeClass octopus, GamePlayerBridgeClass attacker, GameClass game)
    {
        if (octopus.GameCharacter.Passive.All(x => x.PassiveName != "Чернильная завеса")) return 0;

        //Сомнительная тактика
        if (attacker.GameCharacter.Passive.Any(x => x.PassiveName == "Сомнительная тактика"))
        {
            var deepListDoubtfulTactic = attacker.Passives.DeepListDoubtfulTactic;

            if (deepListDoubtfulTactic != null)
                if (!deepListDoubtfulTactic.FriendList.Contains(octopus.GetPlayerId()))
                    return 0;
        }
        //end Сомнительная тактика


        var enemyIds = new List<Guid> { attacker.GetPlayerId() };

        //jew
        var point = await HandleJews(attacker, octopus, game);

        if (point == 0)
        {
            var jews = game.PlayersList.FindAll(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Еврей"));

            switch (jews.Count)
            {
                case 1:
                    enemyIds = new List<Guid> { jews.FirstOrDefault()!.Status.PlayerId };
                    break;
                case 2:
                    enemyIds.Clear();
                    enemyIds.AddRange(jews.Where(x => x.Status.ScoreSource.Contains("Еврей")).Select(j => j.Status.PlayerId));
                    break;
            }
        }
        //end jew

        foreach (var enemyId in enemyIds)
        {
            var octopusInkList = octopus.Passives.OctopusInkList;


            var enemyRealScore = octopusInkList.RealScoreList.Find(x => x.PlayerId == enemyId);
            var octopusRealScore = octopusInkList.RealScoreList.Find(x => x.PlayerId == octopus.GetPlayerId());

            if (octopusRealScore == null)
            {
                octopus.Passives.OctopusInkList.RealScoreList.Add(new Octopus.InkSubClass(enemyId, game.RoundNo, -1));
                octopus.Passives.OctopusInkList.RealScoreList.Add(new Octopus.InkSubClass(octopus.GetPlayerId(),
                    game.RoundNo, 1));
            }
            else
            {
                if (enemyRealScore == null)
                {
                    octopusInkList.RealScoreList.Add(new Octopus.InkSubClass(enemyId, game.RoundNo, -1));
                    octopusRealScore.AddRealScore(game.RoundNo);
                }
                else
                {
                    enemyRealScore.AddRealScore(game.RoundNo, -1);
                    octopusRealScore.AddRealScore(game.RoundNo);
                }
            }
        }

        return 1;
    }

    // TheBoys — M.M. kompromat hints per character
    private string GetKompromatHint(string characterName)
    {
        var hints = new Dictionary<string, List<string>>
        {
            ["Кратос"] = new() { "Этот парень явно любит кого-то душить. Вместе с его проблемами.", "Бородатый мужик с цепями. Очень злой." },
            ["Глеб"] = new() { "Подозреваемый, кажется, постоянно спит... или делает вид.", "Цель пьёт чай в подозрительных количествах." },
            ["DeepList"] = new() { "Мой источник утверждает, что цель ведёт сомнительную тактику.", "Этот тип постоянно что-то считает." },
            ["Стая Гоблинов"] = new() { "Судя по запаху, целей как минимум двадцать.", "Они строят что-то подозрительное." },
            ["Котики"] = new() { "Повсюду шерсть... и подозрительное мурчание.", "Один из них точно невиновен. Другой — нет." },
            ["Vampyr"] = new() { "Следы укусов на шее. Чесноком не пахнет.", "Подозреваемый избегает солнечного света." },
            ["Загадочный Спартанец в маске"] = new() { "У цели обнаружен комплекс бога. И копьё.", "THIS. IS... подозрительно." },
            ["Weedwick"] = new() { "Цель пахнет... травами. Лечебными, конечно.", "Подозреваемый подозрительно расслаблен." },
            ["Saitama"] = new() { "Лысый. Один удар. Больше данных нет.", "Цель скучает. Это опасно." },
            ["HardKitty"] = new() { "Подозреваемый мьютит всех вокруг.", "Не могу найти информацию — цель заблокировала доступ." },
            ["Осьминожка"] = new() { "Щупальца повсюду. Буквально.", "Цель прячется за чернильной завесой." },
            ["Dopa"] = new() { "Ранг: Претендент. Подозрительно высокий.", "Цель выбирает мету. Всегда." },
            ["LeCrisp"] = new() { "Цель делает вид, что она кто-то другой.", "Подозреваемый хрустит. Подозрительно." },
            ["mylorik"] = new() { "Админ. Все данные засекречены.", "Подозреваемый знает слишком много." },
            ["Kira"] = new() { "У цели обнаружена подозрительная тетрадь.", "Подозреваемый пишет имена. Много имён." },
            ["Монстр без имени"] = new() { "Нет данных. Нет имени. Нет лица.", "Этого человека не существует в базах данных." },
            ["Итачи"] = new() { "Глаза... красные глаза. Не смотри в них.", "Подозреваемый — мастер иллюзий." },
        };

        if (hints.TryGetValue(characterName, out var list))
            return list[_rand.Random(0, list.Count - 1)];

        // Generic hint for characters not in the dictionary
        var generic = new List<string>
        {
            "Досье собрано, но данные зашифрованы.",
            "М.М.: Что-то тут нечисто... но я пока не уверен.",
            "Компромат есть, но нужно больше данных.",
            "Подозреваемый ведёт себя подозрительно. Как и все остальные.",
        };
        return generic[_rand.Random(0, generic.Count - 1)];
    }
    //end unique
}