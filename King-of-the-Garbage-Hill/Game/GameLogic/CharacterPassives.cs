﻿using System;
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
                        if (playersList[randIndex].GameCharacter.Name is "Mit*suki*" or "Глеб" or "mylorik"
                            or "Загадочный Спартанец в маске")
                            enemy1 = player.GetPlayerId();
                    } while (enemy1 == player.GetPlayerId());

                    do
                    {
                        var randIndex = _rand.Random(0, playersList.Count - 1);
                        enemy2 = playersList[randIndex].GetPlayerId();
                        if (playersList[randIndex].GameCharacter.Name is "Mit*suki*" or "Глеб" or "mylorik"
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
                    me.GameCharacter.SetStrengthForOneFight(0, "Неуязвимость");
                    break;

                case "Панцирь":
                    var сraboRackShell = target.Passives.CraboRackShell;
                    if (сraboRackShell != null)
                        if (!сraboRackShell.FriendList.Contains(me.GetPlayerId()))
                        {
                            сraboRackShell.FriendList.Add(me.GetPlayerId());
                            сraboRackShell.CurrentAttacker = me.GetPlayerId();
                            target.FightCharacter.AddMoral(3, "Панцирь");
                            target.FightCharacter.AddExtraSkill(33, "Панцирь");
                            target.Status.IsBlock = true;
                        }

                    break;

                case "Хождение боком":
                    me.GameCharacter.SetSpeedForOneFight(0, "Хождение боком");
                    break;

                case "Ничего не понимает":

                    var shark = target.Passives.SharkBoole;

                    if (!shark.FriendList.Contains(me.GetPlayerId()))
                    {
                        shark.FriendList.Add(me.GetPlayerId());
                        me.FightCharacter.AddIntelligence(-1, "Ничего не понимает");
                    }

                    me.GameCharacter.SetIntelligenceForOneFight(0, "Ничего не понимает");
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
                    if (me.GameCharacter.GetStrength() - target.GameCharacter.GetStrength() >= 3 && !target.Status.IsBlock && !target.Status.IsSkip && ok)
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
            }
    }

    public void HandleDefenseAfterBlockOrFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {

            }
    }


    public void HandleDefenseAfterBlockOrFightOrSkip(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Гребанные ассассины":
                    //5-2 = 3
                    if (me.GameCharacter.GetStrength() - target.GameCharacter.GetStrength() < 3)
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
                        me.FightCharacter.AddMoral(9, "Я щас приду", false);
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
            }
    }

    public void HandleAttackBeforeFight(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
    {
        foreach (var passive in me.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "AutoWin":
                    target.Status.IsAbleToWin = false;
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
                        game.AddGlobalLogs($"Пиквард просветил {target.GameCharacter.Name}");
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
                    if (me.GameCharacter.GetCurrentSkillClassTarget() == target.GameCharacter.GetSkillClass())
                    {
                        game.Phrases.KratosTarget.SendLog(me, false);
                        me.FightCharacter.SetSkillFightMultiplier(2);
                        if (game.IsKratosEvent)
                            me.FightCharacter.SetSkillFightMultiplier(4);
                    }

                    break;

                case "Подсчет":
                    var tolya = me.Passives.TolyaCount;

                    if (tolya.IsReadyToUse && me.Status.WhoToAttackThisTurn.Count != 0)
                    {
                        tolya.TargetList.Add(new Tolya.TolyaCountSubClass(target.GetPlayerId(), game.RoundNo));
                        tolya.IsReadyToUse = false;
                        tolya.Cooldown = _rand.Random(4, 5);
                    }

                    break;

                case "Оборотень":
                    var myTempStrength = me.GameCharacter.GetStrength();
                    var targetTempStrength = target.GameCharacter.GetStrength();
                    me.GameCharacter.SetStrengthForOneFight(targetTempStrength, "Оборотень");
                    target.GameCharacter.SetStrengthForOneFight(myTempStrength, "Оборотень");

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


                    if (target.GameCharacter.Justice.GetRealJusticeNow() == 0)
                    {
                        var tempSpeed = me.GameCharacter.GetSpeed() * 2;
                        me.GameCharacter.SetSpeedForOneFight(tempSpeed, "Безжалостный охотник");
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
                        me.FightCharacter.AddPsyche(1, "ОН уважает военное искусство!");
                        target.FightCharacter.AddPsyche(1, "ОН уважает военное искусство!");
                        game.Phrases.SpartanShameMylorik.SendLog(me, false);
                    }

                    if (target.GameCharacter.Name == "Кратос" && !spartan.FriendList.Contains(target.GetPlayerId()))
                    {
                        spartan.FriendList.Add(target.GetPlayerId());
                        me.FightCharacter.AddPsyche(1, "Отец?");
                        target.FightCharacter.AddPsyche(1, "Boi?");
                        game.Phrases.SpartanShameMylorik.SendLog(me, false);
                    }

                    if (!spartan.FriendList.Contains(target.GetPlayerId()))
                    {
                        spartan.FriendList.Add(target.GetPlayerId());
                        target.FightCharacter.AddStrength(-1, "Они позорят военное искусство");
                        target.FightCharacter.AddSpeed(-1, "Они позорят военное искусство");
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
                        new(1, target.GameCharacter.GetIntelligence()),
                        new(2, target.GameCharacter.GetStrength()),
                        new(3, target.GameCharacter.GetSpeed()),
                        new(4, target.GameCharacter.GetPsyche())
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

                        if (me.GameCharacter.GetSkillFightMultiplier() > 1)
                            me.Status.AddInGamePersonalLogs(
                                $"Спарта: {(int)(me.GameCharacter.GetSkill())} *Скилла* против {target.DiscordUsername}\n");
                    }

                    break;

                case "Питается водорослями":
                    if (target.Status.GetPlaceAtLeaderBoard() >= 4) me.Status.AddBonusPoints(1, "Питается водорослями");
                    break;
            }
    }

    public void HandleAttackAfterFight(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
    {
        foreach (var passive in me.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Exploit":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        if (target.Passives.IsExploitable)
                        {
                            game.TotalExploit++;
                            target.Passives.IsExploitable = false;
                            target.Passives.IsExploitFixed = true;
                            if (game.TotalExploit > 0)
                            {
                                me.Status.AddRegularPoints(game.TotalExploit, "Exploit", true);
                            }
                            game.TotalExploit = 0;
                        }
                    }
                    break;

                case "Много выебывается":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        if (me.GameCharacter.GetCurrentSkillClassTarget() == target.GameCharacter.GetSkillClass())
                        {
                            me.FightCharacter.AddExtraSkill(40, "Много выебывается");
                        }
                    }
                    break;

                case "Возвращение из мертвых":
                    if (game.IsKratosEvent && game.RoundNo > 10)
                        if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        {
                            game.AddGlobalLogs($"{me.GameCharacter.Name} **УБИЛ** {target.GameCharacter.Name}!");
                            game.AddGlobalLogs($"Они скинули **{target.DiscordUsername}**! Сволочи!");
                            game.Phrases.KratosEventKill.SendLog(me, true, isRandomOrder:false);
                            target.Passives.KratosIsDead = true;
                        }
                    break;

                case "Weed":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        if (target.Passives.WeedwickWeed > 0)
                        {
                            me.FightCharacter.AddMoral(target.Passives.WeedwickWeed, "Weed");

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
                        if (target.GameCharacter.GetWinStreak() > 1)
                        {
                            if (me.Status.GetPlaceAtLeaderBoard() > target.Status.GetPlaceAtLeaderBoard())
                            {
                                me.Status.AddRegularPoints(target.GameCharacter.GetWinStreak(), "Ценная добыча");
                            }
                            else
                            {
                                me.Status.AddBonusPoints(target.GameCharacter.GetWinStreak(), "Ценная добыча");
                            }
                        }

                        switch (target.GameCharacter.GetWinStreak())
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
                        var range = me.GameCharacter.GetSpeedQualityResistInt();
                        // ReSharper disable once RedundantAssignment
                        range -= target.GameCharacter.GetSpeedQualityKiteBonus();

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
                                target.FightCharacter.LowerQualityResist(target, game, me);
                                game.Phrases.WeedwickValuablePreyDrop.SendLog(me, false);
                            }

                            // 1/5
                            if (_rand.Luck(1, 5))
                            {
                                harm++;
                                target.FightCharacter.LowerQualityResist(target, game, me);
                                game.Phrases.WeedwickValuablePreyDrop.SendLog(me, false);
                            }

                            // 1/3 если враг топ1
                            if (_rand.Luck(1, 3) && target.Status.GetPlaceAtLeaderBoard() == 1)
                            {
                                harm++;
                                target.FightCharacter.LowerQualityResist(target, game, me);
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
                            me.FightCharacter.AddMoral(3, "Падальщик");
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
                            me.Status.AddBonusPoints(me.Status.GetScore() * 3, "Повезло");
                        }

                        me.FightCharacter.AddPsyche(2 * darksciUnstableMultiplier, "Повезло");
                        me.FightCharacter.AddMoral(2 * darksciUnstableMultiplier, "Повезло");
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
            }

        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Возвращение из мертвых":
                    //failed
                    if (game.RoundNo > 10 && game.IsKratosEvent && player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        player.Passives.KratosIsDead = true;
                    }

                    //start
                    else if (!game.IsKratosEvent && game.RoundNo == 10 && player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        game.IsKratosEvent = true;
                        game.AddGlobalLogs("Бегите! На Гору Мусорной Горы идёт Кратос и НИЧТО его не остановит!");
                        foreach (var p in game.PlayersList.Where(x => !x.IsBot()))
                            await game.Phrases.KratosEventYes.SendLogSeparateWithFile(p, false, "DataBase/sound/Kratos_PLAY_ME.mp3", false, 15000);

                        player.FightCharacter.SetSkillFightMultiplier(4);
                        player.FightCharacter.SetClassSkillMultiplier(4);
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

                                if (target!.GameCharacter.Name == "Mit*suki*")
                                {
                                    howMuchToAdd = -2;
                                    target.Status.AddInGamePersonalLogs(
                                        "MitSUKI: __Да сука, я щас ливну, заебали токсики!__\nDeepList: *хохочет*\n");
                                }

                                if (target.GameCharacter.Name != "LeCrisp")
                                {
                                    target.MinusPsycheLog(target.FightCharacter, game, howMuchToAdd, "Стёб");
                                }


                                player.Status.AddRegularPoints(1, "Стёб");
                                game.Phrases.DeepListPokePhrase.SendLog(player, true);
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
                            player.FightCharacter.AddMoral(3, "Месть");
                            player.FightCharacter.AddPsyche(1, "Месть");
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
                            player.FightCharacter.AddExtraSkill(10, "Испанец");
                            player.MinusPsycheLog(player.FightCharacter, game, -1, "Испанец");
                            game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                        }
                        else
                        {
                            boole.Times++;

                            if (boole.Times == 2)
                            {
                                boole.Times = 0;
                                player.FightCharacter.AddExtraSkill(10, "Испанец");
                                player.MinusPsycheLog(player.FightCharacter, game, -1, "Испанец");
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
                    }

                    break;

                case "Импакт":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var lePuska = player.Passives.LeCrispImpact;


                        player.FightCharacter.AddMoral(lePuska.ImpactTimes + 1, "Импакт");
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
                        player.MinusPsycheLog(player.FightCharacter, game, -1, "Не повезло");
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
                            player.FightCharacter.AddSpeed(1, "Челюсти");
                        }
                    }

                    break;

                case "Первая кровь":
                    var spartan = player.Passives.SpartanFirstBlood;

                    if (spartan.FriendList.Count == 1)
                    {
                        if (spartan.FriendList.Contains(player.Status.IsWonThisCalculation))
                        {
                            player.FightCharacter.AddSpeed(1, "Первая кровь");
                            game.Phrases.SpartanFirstBlood.SendLog(player, false);
                            game.AddGlobalLogs("Они познают войну!");
                        }
                        else if (spartan.FriendList.Contains(player.Status.IsLostThisCalculation))
                        {
                            var ene = game.PlayersList.Find(x =>
                                x.GetPlayerId() == player.Status.IsLostThisCalculation);
                            ene!.FightCharacter.AddSpeed(1, "Первая кровь");
                        }

                        spartan.FriendList.Add(Guid.Empty);
                    }

                    break;

                case "Это привилегия - умереть от моей руки":
                    if (player.Status.IsWonThisCalculation != Guid.Empty && game.RoundNo > 4)
                    {
                        game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)!.GameCharacter
                            .Justice.AddJusticeForNextRoundFromSkill();
                        player.FightCharacter.AddIntelligence(-1, "Это привилегия");
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
                                    if (player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Where(x => x.StatIndex == 4) == null)
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
                    if (game.IsKratosEvent && game.RoundNo >= 16 && game.PlayersList.Count(x => !x.Passives.KratosIsDead) < 5)
                    {
                        game.IsKratosEvent = false;
                        game.AddGlobalLogs($"У {player.GameCharacter.Name}а есть тактика и он ее придерживался...");
                        await game.Phrases.KratosEventNo.SendLogSeparateWithFile(player, false, "DataBase/art/events/kratos_death.jpg", false, 15000);
                    }

                    if (game.IsKratosEvent && player.Passives.KratosIsDead)
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
                                game.AddGlobalLogs(
                                    $"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {randomPlayer.GameCharacter.Name}");
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
                            player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(rammusCount);
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
                        player.FightCharacter.AddExtraSkill(30, "3-0 обоссан");
                        player.FightCharacter.AddMoral(3, "3-0 обоссан");

                        var enemyAcc = game.PlayersList.Find(x => x.GetPlayerId() == win);

                        enemyAcc.FightCharacter.AddIntelligence(-1, "3-0 обоссан");
                        enemyAcc.MinusPsycheLog(enemyAcc.FightCharacter, game, -1, "3-0 обоссан");

                        game.Phrases.TigrThreeZero.SendLog(player, false);

                        threeZero.IsUnique = false;
                    }

                    foreach (var threeZero in tigrThreeZero.WhoToLostThisRound.ToList().Select(lost => tigrThreeZero.FriendList.Find(x => x.EnemyPlayerId == lost)))
                    {
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
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Mit*suki*" && game.RoundNo < 4)
                                enemy1 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Вампур" && game.RoundNo >= 4)
                                enemy1 = player.GetPlayerId();
                        } while (enemy1 == player.GetPlayerId());

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy2 = game.PlayersList[randIndex].GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Глеб" or "mylorik" or
                                "Загадочный Спартанец в маске")
                                enemy2 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Mit*suki*" && game.RoundNo < 4)
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
                        player.Passives.YongGlebMetaClass = new List<Guid>();
                        var indexes = new List<int>();
                        while (indexes.Count < 3)
                        {
                            var randomIndex = _rand.Random(0, 5);
                            if(indexes.Contains(randomIndex))
                                continue;
                            indexes.Add(randomIndex);
                        }

                        foreach (var index in indexes)
                        {
                            player.Passives.YongGlebMetaClass.Add(game.PlayersList[index].GetPlayerId());
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

                            game.Phrases.GlebChallengerPhrase.SendLog(player, true);
                            await game.Phrases.GlebChallengerSeparatePhrase.SendLogSeparate(player, true, 0);
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

                                do
                                {
                                    randPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];

                                    var check1 = player.Passives.DeepListSupermindKnown;

                                    if (check1 != null)
                                        if (check1.KnownPlayers.Contains(randPlayer.GetPlayerId()))
                                            randPlayer = player;
                                } while (randPlayer.GetPlayerId() == player.GetPlayerId());

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
                }

            //Я за чаем
            var isSkip = player.Passives.GlebTeaTriggeredWhen;

            if (isSkip.WhenToTrigger.Contains(game.RoundNo))
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
    }






    public void HandleNextRoundAfterSorting(GameClass game)
    {
        foreach (var player in game.PlayersList)
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Weed":
                    var diff = game.RoundNo - player.Passives.WeedwickLastRoundWeed;
                    if (diff >= 3)
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
                        if (game.RoundNo == 9 ||
                            (game.RoundNo == 10 && !game.GetAllGlobalLogs().Contains("Нахуй эту игру")))
                            game.AddGlobalLogs(
                                $"{player.DiscordUsername}: Нахуй эту игру..");


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

                            if (game.RoundNo == 9 ||
                                (game.RoundNo == 10 && !game.GetAllGlobalLogs().Contains("Нахуй эту игру")))
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
        characters.Add("Mit*suki*");
        characters.Add("AWDKA");
        characters.Add("Вампур");


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
                                                player.Predict.Add(new PredictClass("Mit*suki*",
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
    //end unique
}