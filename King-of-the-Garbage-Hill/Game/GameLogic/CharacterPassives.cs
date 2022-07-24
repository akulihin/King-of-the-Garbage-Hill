using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class CharacterPassives : IServiceSingleton
{
    private readonly InGameGlobal _gameGlobal;
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly HelperFunctions _help;
    private readonly Logs _log;
    private readonly SecureRandom _rand;


    public CharacterPassives(SecureRandom rand, HelperFunctions help,
        InGameGlobal gameGlobal, Logs log, GameUpdateMess gameUpdateMess)
    {
        _rand = rand;
        _help = help;
        _gameGlobal = gameGlobal;
        _log = log;
        _gameUpdateMess = gameUpdateMess;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    //handle during fight
    public void HandleDefenseBeforeFight(GamePlayerBridgeClass target,
        GamePlayerBridgeClass me,
        GameClass game)
    {
        var characterName = target.Character.Name;

        switch (characterName)
        {
            case "Осьминожка":
                // Неуязвимость
                var octopusInvulnerability = _gameGlobal.OctopusInvulnerability.Find(x =>
                    x.PlayerId == target.GetPlayerId() && target.GameId == x.GameId);
                if (octopusInvulnerability != null)
                {
                    octopusInvulnerability.CurrentAttacker = me.GetPlayerId();
                    me.Character.ExtraWeight = me.Character.GetStrength() * -1;
                }
                //end Неуязвимость
                break;
            case "Краборак":
                //Панцирь
                var сraboRackShell = _gameGlobal.CraboRackShell.Find(x =>
                    x.PlayerId == target.GetPlayerId() && target.GameId == x.GameId);
                if (сraboRackShell != null)
                    if (!сraboRackShell.FriendList.Contains(me.GetPlayerId()))
                    {
                        сraboRackShell.FriendList.Add(me.GetPlayerId());
                        сraboRackShell.CurrentAttacker = me.GetPlayerId();
                        target.Character.AddMoral(target.Status, 6, "Панцирь");
                        target.Character.AddExtraSkill(target.Status, 30, "Панцирь");
                        target.Status.IsBlock = true;
                    }
                //end Панцирь

                // Хождение боком
                var сraboBakoBoole = _gameGlobal.CraboRackBakoBoole.Find(x =>
                    x.PlayerId == target.GetPlayerId() && target.GameId == x.GameId);
                if (сraboBakoBoole != null)
                {
                    сraboBakoBoole.CurrentAttacker = me.GetPlayerId();
                    me.Character.ExtraWeight = me.Character.GetSpeed() * -1;
                }

                //end Хождение боком
                break;

            case "Братишка":
                //Ничего не понимает: 
                var shark = _gameGlobal.SharkBoole.Find(x =>
                    x.PlayerId == target.GetPlayerId() &&
                    game.GameId == x.GameId);

                if (!shark.FriendList.Contains(me.GetPlayerId()))
                {
                    shark.FriendList.Add(me.GetPlayerId());
                    me.Character.AddIntelligence(me.Status, -1, "Ничего не понимает");
                }
                //end Ничего не понимает: 


                // Ничего не понимает
                var bratishkaDontUnderstand = _gameGlobal.SharkDontUnderstand.Find(x =>
                    x.PlayerId == target.GetPlayerId() && target.GameId == x.GameId);
                if (bratishkaDontUnderstand != null)
                {
                    bratishkaDontUnderstand.CurrentAttacker = me.GetPlayerId();
                    me.Character.ExtraWeight = me.Character.GetIntelligence() * -1;
                }
                //end  Ничего не понимает

                break;

            case "Глеб":
                //Я щас приду:
                var rand = _rand.Random(1, 9);
                if (rand == 1)
                {
                    var acc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                        x.PlayerId == target.GetPlayerId() && target.GameId == x.GameId);


                    if (acc.WhenToTrigger.Contains(game.RoundNo))
                        return;


                    if (!target.Status.IsSkip)
                    {
                        target.Status.IsSkip = true;
                        _gameGlobal.GlebSkipList.Add(new Gleb.GlebSkipClass(target.GetPlayerId(), game.GameId));
                        game.Phrases.GlebComeBackPhrase.SendLog(target, true);

                        var glebSkipFriendList = _gameGlobal.GlebSkipFriendList.Find(x =>
                            x.PlayerId == target.GetPlayerId() && game.GameId == x.GameId);
                        if (!glebSkipFriendList.FriendList.Contains(me.GetPlayerId()))
                            glebSkipFriendList.FriendList.Add(me.GetPlayerId());
                    }
                }

                //end Я щас приду:
                break;
            case "LeCrisp":
                //Гребанные ассассин

                //Гребанные ассассин + Сомнительная тактика
                var ok = true;
                var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                    x.PlayerId == target.GetPlayerId() && game.GameId == x.GameId);

                if (deep != null)
                    if (!deep.FriendList.Contains(me.GetPlayerId()))
                        ok = false;


                if (me.Character.GetStrength() - target.Character.GetStrength() >= 2
                    && !target.Status.IsBlock
                    && !target.Status.IsSkip
                    && ok)
                {
                    target.Status.IsAbleToWin = false;
                    game.Phrases.LeCrispAssassinsPhrase.SendLog(target, false);
                }
                //end Гребанные ассассин

                break;

            case "Толя":

                //Раммус мейн
                if (target.Status.IsBlock)
                {
                    // target.Status.IsBlock = false;
                    me.Status.IsAbleToWin = false;
                    var tolya = _gameGlobal.TolyaRammusTimes.Find(x =>
                        x.GameId == target.GameId &&
                        x.PlayerId == target.GetPlayerId());
                    tolya.FriendList.Add(me.GetPlayerId());
                }
                //end Раммус мейн

                break;

            case "HardKitty":
                //Одиночество
                var hard = _gameGlobal.HardKittyLoneliness.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == target.GetPlayerId());
                if (hard != null)
                    if (!hard.Activated)
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
                                hardEnemy.Times += 1;
                                break;
                            case 5:
                            case 6:
                            case 7:
                            case 8:
                            case 9:
                                hardEnemy.Times += 2;
                                break;
                            case 10:
                                hardEnemy.Times += 4;
                                break;
                        }
                    }

                //Одиночество
                break;

            case "Mit*suki*":
                //Запах мусора
                var mitsuki = _gameGlobal.MitsukiGarbageList.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == target.GetPlayerId());

                if (mitsuki == null)
                {
                    _gameGlobal.MitsukiGarbageList.Add(new Mitsuki.GarbageClass(target.GetPlayerId(), game.GameId,
                        me.GetPlayerId()));
                }
                else
                {
                    var found = mitsuki.Training.Find(x => x.EnemyId == me.GetPlayerId());
                    if (found != null)
                        found.Times++;
                    else
                        mitsuki.Training.Add(new Mitsuki.GarbageSubClass(me.GetPlayerId()));
                }

                //end Запах мусора
                break;
        }
    }

    public void HandleDefenseAfterBlockOrFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        var characterName = target.Character.Name;

        switch (characterName)
        {
            case "LeCrisp":
                //Гребанные ассассин
                if (me.Character.GetStrength() - target.Character.GetStrength() >= 3 && !target.Status.IsBlock &&
                    !target.Status.IsSkip)
                {
                    target.Status.IsAbleToWin = true;
                }
                else
                {
                    var leCrip = _gameGlobal.LeCrispAssassins.Find(x =>
                        x.PlayerId == target.GetPlayerId() && game.GameId == x.GameId);
                    leCrip.AdditionalPsycheForNextRound += 1;
                }

                //end Гребанные ассассин
                break;
        }
    }

    public void HandleDefenseAfterFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        var characterName = target.Character.Name;


        switch (characterName)
        {
            case "Глеб":
                //Я щас приду:
                var glebSkipFriendList = _gameGlobal.GlebSkipFriendList.Find(x =>
                    x.PlayerId == target.GetPlayerId() && game.GameId == x.GameId);
                if (glebSkipFriendList.FriendList.Contains(me.GetPlayerId()))
                {
                    glebSkipFriendList.FriendList.Remove(me.GetPlayerId());
                    me.Character.AddMoral(me.Status, 9, "Я щас приду", false);
                    me.Status.AddInGamePersonalLogs("Я щас приду: +9 *Морали*. Вы дождались Глеба!!! Празднуем!");
                }

                //end Я щас приду:
                break;
            case "LeCrisp":
                //Импакт:
                if (target.Status.IsLostThisCalculation != Guid.Empty)
                {
                    var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                        x.PlayerId == target.GetPlayerId() && x.GameId == game.GameId);

                    lePuska.IsLost = true;
                }
                //end Импакт

                break;
            case "HardKitty":
                //Mute passive
                if (target.Status.IsLostThisCalculation != Guid.Empty)
                {
                    var hardKittyMute = _gameGlobal.HardKittyMute.Find(x =>
                        x.PlayerId == target.GetPlayerId() &&
                        x.GameId == game.GameId);


                    if (!hardKittyMute.UniquePlayers.Contains(me.GetPlayerId()))
                    {
                        hardKittyMute.UniquePlayers.Add(me.GetPlayerId());
                        me.Status.AddRegularPoints(1, "Mute");
                        game.Phrases.HardKittyMutedPhrase.SendLog(target, false);
                    }
                }
                //Mute passive end

                //Доебаться
                var hardKittyDoebatsya = _gameGlobal.HardKittyDoebatsya.Find(x =>
                    x.PlayerId == target.GetPlayerId() && x.GameId == game.GameId);

                var found = hardKittyDoebatsya.LostSeries.Find(x => x.EnemyPlayerId == me.GetPlayerId());
                if (found != null)
                {
                    found.Series = 0;
                    game.Phrases.HardKittyDoebatsyaAnswerPhrase.SendLog(target, false);
                }
                //end Доебаться

                break;
        }
    }


    public void HandleAttackBeforeFight(GamePlayerBridgeClass me,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        var characterName = me.Character.Name;

        switch (characterName)
        {
            case "Загадочный Спартанец в маске":

                //Им это не понравится:
                var spartanMark =
                    _gameGlobal.SpartanMark.Find(x => x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());
                if (spartanMark != null)
                    if (target.Status.IsBlock && spartanMark.FriendList.Contains(target.GetPlayerId()))
                    {
                        spartanMark.BlockedPlayer = target.GetPlayerId();
                        target.Status.IsAbleToWin = false;
                        target.Status.IsBlock = false;
                        game.Phrases.SpartanTheyWontLikeIt.SendLog(me, false);
                    }
                //end Им это не понравится:


                //DragonSlayer:
                if (game.RoundNo == 10)
                    if (target.Character.Name == "Sirinoks")
                    {
                        target.Status.IsAbleToWin = false;
                        game.AddGlobalLogs("**Я DRAGONSLAYER!**\n" +
                                           $"{me.DiscordUsername} побеждает дракона и забирает **1000 голды**!");
                        foreach (var p in game.PlayersList) game.Phrases.SpartanDragonSlayer.SendLog(p, false);
                    }
                //end DragonSlayer


                //Первая кровь: 
                var pant = _gameGlobal.SpartanFirstBlood.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());
                if (pant.FriendList.Count == 0) pant.FriendList.Add(target.GetPlayerId());


                //end Первая кровь: 

                //Они позорят военное искусство:
                var Spartan = _gameGlobal.SpartanShame.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());


                if (target.Character.Name == "mylorik" && !Spartan.FriendList.Contains(target.GetPlayerId()))
                {
                    Spartan.FriendList.Add(target.GetPlayerId());
                    me.Character.AddPsyche(me.Status, 1, "ОН уважает военное искусство!");
                    target.Character.AddPsyche(target.Status, 1, "ОН уважает военное искусство!");
                    game.Phrases.SpartanShameMylorik.SendLog(me, false);
                }

                if (!Spartan.FriendList.Contains(target.GetPlayerId()))
                {
                    Spartan.FriendList.Add(target.GetPlayerId());
                    target.Character.AddStrength(target.Status, -1, "Они позорят военное искусство");
                    target.Character.AddSpeed(target.Status, -1, "Они позорят военное искусство");
                }


                //end Они позорят военное искусство:
                break;


            case "Глеб":
                // Я за чаем:
                var geblTea =
                    _gameGlobal.GlebTea.Find(x => x.PlayerId == me.GetPlayerId() && game.GameId == x.GameId);

                if (geblTea.Ready && me.Status.WhoToAttackThisTurn != Guid.Empty)
                {
                    geblTea.Ready = false;
                    _gameGlobal.GlebTeaTriggeredWhen.Add(new WhenToTriggerClass(me.Status.WhoToAttackThisTurn,
                        game.GameId, game.RoundNo + 1));
                    me.Status.AddRegularPoints(1, "Я за чаем");
                    game.Phrases.GlebTeaPhrase.SendLog(me, true);
                }

                //end  Я за чаем:
                break;

            case "Sirinoks":

                //Заводить друзей
                var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());
                var siriAttack = _gameGlobal.SirinoksFriendsAttack.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());

                if (siri != null && siriAttack != null)
                    if (siri.FriendList.Contains(target.GetPlayerId()) && target.Status.IsBlock)
                    {
                        target.Status.IsBlock = false;
                        siriAttack.EnemyId = target.GetPlayerId();
                        siriAttack.IsBlock = true;
                    }

                if (siri != null && siriAttack != null)
                    if (siri.FriendList.Contains(target.GetPlayerId()) && target.Status.IsSkip)
                    {
                        target.Status.IsSkip = false;
                        siriAttack.EnemyId = target.GetPlayerId();
                        siriAttack.IsSkip = true;
                    }

                if (!siri.FriendList.Contains(target.GetPlayerId()))
                {
                    siri.FriendList.Add(target.GetPlayerId());
                    me.Status.AddRegularPoints(1, "Заводить друзей");
                    game.Phrases.SirinoksFriendsPhrase.SendLog(me, true);
                }


                //Заводить друзей end
                break;

            case "AWDKA":

                //Научите играть
                var awdka = _gameGlobal.AwdkaTeachToPlay.Find(x => x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());
                var awdkaHistory = _gameGlobal.AwdkaTeachToPlayHistory.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());

                var player2Stats = new List<Sirinoks.TrainingSubClass>
                {
                    new(1, target.Character.GetIntelligence()),
                    new(2, target.Character.GetStrength()),
                    new(3, target.Character.GetSpeed()),
                    new(4, target.Character.GetPsyche())
                };
                var sup = player2Stats.OrderByDescending(x => x.StatNumber).ToList().First();

                if (awdka == null)
                    _gameGlobal.AwdkaTeachToPlay.Add(new Sirinoks.TrainingClass(me.GetPlayerId(), game.GameId,
                        sup.StatIndex, sup.StatNumber, Guid.Empty));
                else
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
                //end Научите играть

                //Я пытаюсь
                var awdkaTrying = _gameGlobal.AwdkaTryingList.Find(x => x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());
                var awdkaTryingTarget = awdkaTrying?.TryingList.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                if (awdkaTryingTarget is { IsUnique: true })
                {
                    me.Character.SetSkillFightMultiplier(2);
                }
                //end Я пытаюсь

                break;
            case "Вампур":

                //Падальщик
                if (target.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                {
                    var scavenger = _gameGlobal.VampyrScavengerList.Find(x =>
                        x.PlayerId == me.GetPlayerId() && x.GameId == game.GameId);
                    scavenger.EnemyId = target.GetPlayerId();
                    scavenger.EnemyJustice = target.Character.Justice.GetFullJusticeNow();
                    target.Character.Justice.SetFullJusticeNow(target.Status, scavenger.EnemyJustice - 1, "Падальщик",
                        false);
                }

                //end Падальщик
                break;
            case "mylorik":
                // Спарта
                var mylorikSpartan =
                    _gameGlobal.MylorikSpartan.Find(x => x.GameId == me.GameId && x.PlayerId == me.GetPlayerId());
                var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                if (mylorikEnemy == null)
                {
                    mylorikSpartan.Enemies.Add(new Mylorik.MylorikSpartanSubClass(target.GetPlayerId()));
                    mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                }

                if (me.Status.WhoToAttackThisTurn == target.GetPlayerId())
                    //set FightMultiplier
                {
                    switch (mylorikEnemy.LostTimes)
                    {
                        case 1:
                            me.Character.SetSkillFightMultiplier(2);
                            break;
                        case 2:
                            me.Character.SetSkillFightMultiplier(4);
                            break;
                        case 3:
                            me.Character.SetSkillFightMultiplier(8);
                            break;
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                        case 8:
                        case 9:
                        case 10:
                            me.Character.SetSkillFightMultiplier(16);
                            game.AddGlobalLogs(
                                $"mylorik: Айсик, можно тортик? У меня {me.Character.GetSkill()} *Скилла*!");
                            break;
                        default:
                            me.Character.SetSkillFightMultiplier();
                            break;
                    }

                    if (me.Character.GetSkillFightMultiplier() > 1)
                        me.Status.AddInGamePersonalLogs(
                            $"Спарта: {me.Character.GetSkill()} *Скилла* против {target.DiscordUsername}\n");
                }

                //end Cпарта
                break;
            case "Краборак":
                //Питается водорослями
                if (target.Status.PlaceAtLeaderBoard >= 4) me.Status.AddBonusPoints(1, "Питается водорослями");
                //end Питается водорослями
                break;
        }
    }

    public void HandleAttackAfterFight(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
    {
        var characterName = me.Character.Name;


        switch (characterName)
        {
            case "Глеб":
                //Я щас приду:
                var glebSkipFriendList =
                    _gameGlobal.GlebSkipFriendList.Find(x => x.PlayerId == me.GetPlayerId() && game.GameId == x.GameId);
                if (glebSkipFriendList.FriendList.Contains(target.GetPlayerId()))
                {
                    glebSkipFriendList.FriendList.Remove(target.GetPlayerId());
                    target.Character.AddMoral(target.Status, 9, "Я щас приду", false);
                    target.Status.AddInGamePersonalLogs("Я щас приду: +9 *Морали*. Вы дождались Глеба!!! Празднуем!");
                }

                //end Я щас приду:
                break;
            case "Загадочный Спартанец в маске":

                //Им это не понравится:
                var spartanMark =
                    _gameGlobal.SpartanMark.Find(x => x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());
                if (spartanMark != null)
                    if (spartanMark.BlockedPlayer == target.GetPlayerId())
                    {
                        target.Status.IsAbleToWin = true;
                        target.Status.IsBlock = true;
                        spartanMark.BlockedPlayer = Guid.Empty;
                    }
                //end Им это не понравится:

                //DragonSlayer:
                if (game.RoundNo == 10)
                    if (target.Character.Name == "Sirinoks")
                        target.Status.IsAbleToWin = true;
                //end DragonSlayer

                break;
            case "Бог ЛоЛа":
                _gameGlobal.LolGodUdyrList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.GetPlayerId())
                    .EnemyPlayerId = target.GetPlayerId();
                game.Phrases.SecondСommandmentBan.SendLog(me, false);
                break;
            case "Вампур":
                //Падальщик
                if (target.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                {
                    var scavenger = _gameGlobal.VampyrScavengerList.Find(x =>
                        x.PlayerId == me.GetPlayerId() && x.GameId == game.GameId);

                    if (scavenger.EnemyId == target.GetPlayerId())
                    {
                        target.Character.Justice.SetFullJusticeNow(target.Status, scavenger.EnemyJustice, "Падальщик",
                            false);
                        scavenger.EnemyId = Guid.Empty;
                        scavenger.EnemyJustice = 0;
                        if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                            me.Character.AddMoral(me.Status, 3, "Падальщик");
                    }
                }
                //end Падальщик

                //Вампуризм
                if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    me.Character.Justice.AddJusticeForNextRoundFromSkill(target.Character.Justice.GetFullJusticeNow());
                //Вампуризм

                break;


            case "Осьминожка":
                //Неуязвимость
                if (me.Status.IsLostThisCalculation != Guid.Empty)
                {
                    var octo = _gameGlobal.OctopusInvulnerabilityList.Find(x =>
                        x.GameId == me.GameId &&
                        x.PlayerId == me.GetPlayerId());

                    if (octo == null)
                        _gameGlobal.OctopusInvulnerabilityList.Add(
                            new Octopus.InvulnerabilityClass(me.GetPlayerId(), game.GameId));
                    else
                        octo.Count++;
                }
                //end Неуязвимость

                break;

            case "Sirinoks":

                //Заводить друзей
                var siriAttack = _gameGlobal.SirinoksFriendsAttack.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == me.GetPlayerId());

                if (siriAttack != null)
                    if (siriAttack.EnemyId == target.GetPlayerId())
                    {
                        if (siriAttack.IsSkip)
                            target.Status.IsSkip = true;

                        if (siriAttack.IsBlock)
                            target.Status.IsBlock = true;

                        siriAttack.EnemyId = Guid.Empty;
                        siriAttack.IsBlock = false;
                        siriAttack.IsSkip = false;
                    }

                //end Заводить друзей
                break;

            case "Darksci":
                //Повезло
                var darscsi = _gameGlobal.DarksciLuckyList.Find(x =>
                    x.GameId == me.GameId &&
                    x.PlayerId == me.GetPlayerId());

                if (!darscsi.TouchedPlayers.Contains(target.GetPlayerId()))
                    darscsi.TouchedPlayers.Add(target.GetPlayerId());

                if (darscsi.TouchedPlayers.Count == game.PlayersList.Count - 1 && darscsi.Triggered == false)
                {
                    var darksciType =
                        _gameGlobal.DarksciTypeList.Find(x =>
                            x.PlayerId == me.GetPlayerId() && game.GameId == x.GameId);
                    if (darksciType.IsStableType)
                        me.Status.AddBonusPoints(me.Status.GetScore(), "Повезло");
                    else
                        me.Status.AddBonusPoints(me.Status.GetScore() * 3, "Повезло");

                    me.Character.AddPsyche(me.Status, 3, "Повезло");
                    darscsi.Triggered = true;
                    game.Phrases.DarksciLucky.SendLog(me, true);
                }
                //end Повезло

                break;
            case "mylorik":
                // Cпарта
                if (me.Status.WhoToAttackThisTurn == target.GetPlayerId())
                {
                    var mylorikSpartan =
                        _gameGlobal.MylorikSpartan.Find(x => x.GameId == me.GameId && x.PlayerId == me.GetPlayerId());
                    var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                    if (mylorikEnemy == null)
                    {
                        mylorikSpartan.Enemies.Add(new Mylorik.MylorikSpartanSubClass(target.GetPlayerId()));
                        mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                    }

                    if (me.Status.IsLostThisCalculation == me.Status.WhoToAttackThisTurn) mylorikEnemy.LostTimes++;

                    if (me.Status.IsWonThisCalculation == me.Status.WhoToAttackThisTurn) mylorikEnemy.LostTimes = 0;
                }

                //Cпарта reset FightMultiplier
                me.Character.SetSkillFightMultiplier();
                //end Cпарта
                break;
            case "AWDKA":
                //Я пытаюсь reset FightMultiplier
                me.Character.SetSkillFightMultiplier();
                //end Я пытаюсь
                break;
        }
    }


    public void HandleCharacterAfterFight(GamePlayerBridgeClass player, GameClass game)
    {
        //Подсчет
        if (player.Status.IsLostThisCalculation != Guid.Empty && player.Character.Name != "Толя" &&
            game.PlayersList.Any(x => x.Character.Name == "Толя"))
        {
            var tolyaAcc = game.PlayersList.Find(x => x.Character.Name == "Толя");

            var tolyaCount = _gameGlobal.TolyaCount.Find(x =>
                x.PlayerId == tolyaAcc.GetPlayerId() && x.GameId == game.GameId);


            if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1 && x.Target == player.GetPlayerId()))
            {
                tolyaAcc.Status.AddRegularPoints(2, "Подсчет");
                tolyaAcc.Character.Justice.AddJusticeForNextRoundFromSkill(2);
                game.Phrases.TolyaCountPhrase.SendLog(tolyaAcc, false);
            }
        }
        //Подсчет end


        var characterName = player.Character.Name;
        switch (characterName)
        {
            case "Краборак":
                //Панцирь
                var сraboRackShell = _gameGlobal.CraboRackShell.Find(x =>
                    x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);
                if (сraboRackShell != null)
                    if (сraboRackShell.CurrentAttacker != Guid.Empty)
                    {
                        сraboRackShell.CurrentAttacker = Guid.Empty;
                        player.Status.IsBlock = false;
                    }
                //end Панцирь

                //Хождение боком
                var сraboBakoBoole = _gameGlobal.CraboRackBakoBoole.Find(x =>
                    x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);
                if (сraboBakoBoole != null)
                    if (сraboBakoBoole.CurrentAttacker != Guid.Empty)
                    {
                        game.PlayersList.Find(x => x.GetPlayerId() == сraboBakoBoole.CurrentAttacker).Character.ExtraWeight = 0;
                        сraboBakoBoole.CurrentAttacker = Guid.Empty;
                    }

                //end Хождение боком
                break;

            case "DeepList":
                //Сомнительная тактика
                var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                    x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);

                if (deep != null)
                    if (!deep.FriendList.Contains(player.Status.IsFighting) &&
                        player.Status.IsLostThisCalculation == player.Status.IsFighting)
                    {
                        player.Status.IsAbleToWin = true;
                        deep.FriendList.Add(player.Status.IsFighting);
                        game.Phrases.DeepListDoubtfulTacticFirstLostPhrase.SendLog(player, false);
                    }

                if (deep != null)
                    if (deep.FriendList.Contains(player.Status.IsFighting))
                        if (player.Status.IsWonThisCalculation != Guid.Empty)
                        {
                            player.Status.AddRegularPoints(1, "Сомнительная тактика");
                            //player.Status.AddBonusPoints(1, "Сомнительная тактика");
                            game.Phrases.DeepListDoubtfulTacticPhrase.SendLog(player, false);
                        }
                //end Сомнительная тактика

                // Стёб
                if (player.Status.IsWonThisCalculation != Guid.Empty)
                {
                    var target =
                        game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                    //Стёб
                    var currentDeepList = _gameGlobal.DeepListMockeryList.Find(x =>
                        x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);

                    if (currentDeepList != null)
                    {
                        var currentDeepList2 =
                            currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == target.GetPlayerId());

                        if (currentDeepList2 != null)
                        {
                            currentDeepList2.Times++;

                            if (currentDeepList2.Times == 2 && !currentDeepList2.Triggered)
                            {
                                currentDeepList2.Triggered = true;

                                if (target.Character.Name != "LeCrisp")
                                {
                                    target.Character.AddPsyche(target.Status, -1, "Стёб");
                                    target.MinusPsycheLog(game);
                                }


                                player.Status.AddRegularPoints(1, "Стёб");
                                game.Phrases.DeepListPokePhrase.SendLog(player, true);
                                if (target.Character.GetPsyche() < 4)
                                    if (target.Character.Justice.GetFullJusticeNow() > 0)
                                        if (target.Character.Name != "LeCrisp")
                                            target.Character.Justice.AddJusticeForNextRoundFromSkill(-1);
                            }
                        }
                        else
                        {
                            currentDeepList.WhoWonTimes.Add(new DeepList.MockerySub(target.GetPlayerId(), 1));
                        }
                    }
                    else
                    {
                        var toAdd = new DeepList.Mockery(
                            new List<DeepList.MockerySub> { new(target.GetPlayerId(), 1) }, game.GameId,
                            player.GetPlayerId());
                        _gameGlobal.DeepListMockeryList.Add(toAdd);
                    }

                    //end Стёб
                }

                //end Стёб
                break;

            case "mylorik":
                //Месть
                //enemyIdLostTo may be 0
                var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                    x.GameId == player.GameId && x.PlayerId == player.GetPlayerId());

                if (player.Status.IsLostThisCalculation != Guid.Empty)
                {
                    //check if very first lost
                    if (mylorik == null)
                    {
                        _gameGlobal.MylorikRevenge.Add(new Mylorik.MylorikRevengeClass(player.GetPlayerId(),
                            player.GameId, player.Status.IsLostThisCalculation, game.RoundNo));
                        game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                    }
                    else
                    {
                        if (mylorik.EnemyListPlayerIds.All(x =>
                                x.EnemyPlayerId != player.Status.IsLostThisCalculation))
                        {
                            mylorik.EnemyListPlayerIds.Add(
                                new Mylorik.MylorikRevengeClassSub(player.Status.IsLostThisCalculation, game.RoundNo));
                            game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                        }
                    }
                }
                else
                {
                    var find = mylorik?.EnemyListPlayerIds.Find(x =>
                        x.EnemyPlayerId == player.Status.IsWonThisCalculation && x.IsUnique);

                    if (find != null && find.RoundNumber != game.RoundNo)
                    {
                        player.Status.AddRegularPoints(2, "Месть");
                        player.Character.AddMoral(player.Status, 3, "Месть");
                        player.Character.AddPsyche(player.Status, 1, "Месть");
                        find.IsUnique = false;
                        game.Phrases.MylorikRevengeVictoryPhrase.SendLog(player, true);
                    }
                }
                //end //Месть

                //Испанец
                if (player.Status.IsLostThisCalculation != Guid.Empty)
                {
                    var rand = _rand.Random(1, 2);
                    var boole = _gameGlobal.MylorikSpanish.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                    if (rand == 1)
                    {
                        boole.Times = 0;
                        player.Character.AddPsyche(player.Status, -1, "Испанец");
                        player.Character.AddExtraSkill(player.Status, 10, "Испанец");
                        player.MinusPsycheLog(game);
                        game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                    }
                    else
                    {
                        boole.Times++;

                        if (boole.Times == 2)
                        {
                            boole.Times = 0;
                            player.Character.AddPsyche(player.Status, -1, "Испанец");
                            player.Character.AddExtraSkill(player.Status, 10, "Испанец");
                            player.MinusPsycheLog(game);
                            game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                        }
                    }
                }

                //end Испанец
                break;
            case "Глеб":
                //Спящее хуйло
                var skip = _gameGlobal.GlebSkipList.Find(x =>
                    x.PlayerId == player.GetPlayerId() && x.GameId == player.GameId);
                if (skip != null && player.Status.WhoToAttackThisTurn != Guid.Empty)
                {
                    player.Status.IsSkip = false;
                    _gameGlobal.GlebSkipList.Remove(skip);
                }
                //end Спящее хуйло

                break;
            case "LeCrisp":

                //Импакт
                if (player.Status.IsWonThisCalculation != Guid.Empty)
                {
                    var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);


                    player.Character.AddMoral(player.Status, lePuska.ImpactTimes + 1, "Импакт");
                }

                //Импакт
                break;
            case "Толя":
                //Раммус мейн
                if (player.Status.IsBlock && player.Status.IsWonThisCalculation != Guid.Empty)
                    game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation).Status
                        .IsAbleToWin = true;
                //end Раммус мейн
                break;
            case "HardKitty":
                //Доебаться
                var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                    x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);

                if (player.Status.WhoToAttackThisTurn != Guid.Empty)
                    if (player.Status.IsLostThisCalculation == player.Status.WhoToAttackThisTurn ||
                        player.Status.IsTargetBlocked == player.Status.WhoToAttackThisTurn ||
                        player.Status.IsTargetSkipped == player.Status.WhoToAttackThisTurn)
                    {
                        var found = hardKitty.LostSeries.Find(x =>
                            x.EnemyPlayerId == player.Status.WhoToAttackThisTurn);

                        if (found != null)
                            found.Series++;
                        else
                            hardKitty.LostSeries.Add(
                                new HardKitty.DoebatsyaSubClass(player.Status.WhoToAttackThisTurn));
                    }

                if (player.Status.IsWonThisCalculation != Guid.Empty &&
                    player.Status.IsWonThisCalculation == player.Status.WhoToAttackThisTurn)
                {
                    var found = hardKitty.LostSeries.Find(x =>
                        x.EnemyPlayerId == player.Status.WhoToAttackThisTurn);
                    if (found != null)
                        if (found.Series > 0)
                        {
                            if (found.Series >= 10) found.Series += 10;
                            player.Status.AddRegularPoints(found.Series * 2, "Доебаться");

                            if (found.Series >= 10)
                                game.Phrases.HardKittyDoebatsyaLovePhrase.SendLog(player, false);
                            else
                                game.Phrases.HardKittyDoebatsyaPhrase.SendLog(player, false);
                            found.Series = 0;
                        }
                }

                //end Доебаться
                break;
            case "Sirinoks":
                //Обучение
                var siri = _gameGlobal.SirinoksTraining.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());


                if (player.Status.IsLostThisCalculation != Guid.Empty)
                {
                    var playerSheLostLastTime =
                        game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsLostThisCalculation);
                    var intel = new List<Sirinoks.StatsClass>
                    {
                        new(1, playerSheLostLastTime.Character.GetIntelligence()),
                        new(2, playerSheLostLastTime.Character.GetStrength()),
                        new(3, playerSheLostLastTime.Character.GetSpeed()),
                        new(4, playerSheLostLastTime.Character.GetPsyche())
                    };
                    var best = intel.OrderByDescending(x => x.Number).ToList().First();


                    if (siri == null)
                    {
                        _gameGlobal.SirinoksTraining.Add(new Sirinoks.TrainingClass(player.GetPlayerId(),
                            game.GameId, best.Index, best.Number, playerSheLostLastTime.GetPlayerId()));
                    }
                    else
                    {
                        siri.Training.Clear();
                        siri.Training.Add(new Sirinoks.TrainingSubClass(best.Index, best.Number));
                    }
                }

                //Обучение end
                break;
            case "Mit*suki*":
                //Много выебывается
                if (player.Status.WhoToAttackThisTurn != Guid.Empty &&
                    player.Status.WhoToAttackThisTurn == player.Status.IsWonThisCalculation)
                {
                    var playerIamAttacking =
                        game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);

                    var howMuchToAdd = player.Character.GetSkillMainOnly() switch
                    {
                        10 => 10,
                        19 => 9,
                        27 => 8,
                        34 => 7,
                        40 => 6,
                        45 => 5,
                        49 => 4,
                        52 => 3,
                        54 => 2,
                        _ => 0
                    };

                    if (player.Character.GetCurrentSkillClassTarget() == playerIamAttacking.Character.GetSkillClass())
                        player.Character.AddExtraSkill(player.Status, howMuchToAdd * 2, "Много выебывается");
                }

                //end Много выебывается
                break;
            case "AWDKA":
                //Произошел троллинг:
                if (player.Status.IsWonThisCalculation != Guid.Empty &&
                    player.Status.WhoToAttackThisTurn == player.Status.IsWonThisCalculation)
                {
                    var awdka = _gameGlobal.AwdkaTrollingList.Find(x =>
                        x.GameId == player.GameId &&
                        x.PlayerId == player.GetPlayerId());

                    var enemy = awdka.EnemyList.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);

                    if (enemy == null)
                        awdka.EnemyList.Add(new Awdka.TrollingSubClass(player.Status.IsWonThisCalculation,
                            game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)
                                .Status
                                .GetScore()));
                    else
                        enemy.Score = game.PlayersList
                            .Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)
                            .Status.GetScore();
                }
                //end Произошел троллинг:

                //Я пытаюсь!
                if (player.Status.IsLostThisCalculation != Guid.Empty)
                {
                    var awdka = _gameGlobal.AwdkaTryingList.Find(x =>
                        x.GameId == player.GameId && x.PlayerId == player.GetPlayerId());


                    var enemy = awdka.TryingList.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);
                    if (enemy == null)
                        awdka.TryingList.Add(new Awdka.TryingSubClass(player.Status.IsLostThisCalculation));
                    else
                        enemy.Times++;
                }

                //Я пытаюсь!
                break;
            case "Осьминожка":

                /*//привет со дна
                if (player.Status.IsWonThisCalculation != Guid.Empty)
                {
                    var moral = player.Status.PlaceAtLeaderBoard - game.PlayersList
                        .Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation).Status.PlaceAtLeaderBoard;
                    if (moral > 0)
                        player.Character.AddMoral(player.Status, moral, "Привет со дна");
                }
                //end привет со дна*/

                //Неуязвимость
                var octopusInvulnerability = _gameGlobal.OctopusInvulnerability.Find(x =>
                    x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);
                if (octopusInvulnerability != null)
                    if (octopusInvulnerability.CurrentAttacker != Guid.Empty)
                    {
                        game.PlayersList.Find(x => x.GetPlayerId() == octopusInvulnerability.CurrentAttacker).Character.ExtraWeight = 0;
                        octopusInvulnerability.CurrentAttacker = Guid.Empty;
                    }
                //end Неуязвимость

                break;
            case "Darksci":
                //Не повезло
                if (player.Status.IsLostThisCalculation != Guid.Empty)
                {
                    //LOL GOD, EXAMPLE:
                    /*
                    if (game.PlayersList.All(x => x.Character.Name != "Бог ЛоЛа") || _gameGlobal.LolGodUdyrList.Any(
                            x =>
                                x.GameId == game.GameId && x.EnemyDiscordId == player.GetPlayerId()))
                    {
                        player.Character.AddPsyche(player.Status, -1);
                        player.MinusPsycheLog(game);
                        game.Phrases.DarksciNotLucky.SendLog(player);
                    }
                    else
                        game.Phrases.ThirdСommandment.SendLog(player);*/
                    player.Character.AddPsyche(player.Status, -1, "Не повезло");
                    player.MinusPsycheLog(game);
                    game.Phrases.DarksciNotLucky.SendLog(player, false);
                }

                //end Не повезло
                break;
            case "Тигр":
                //3-0 обоссан: 
                if (player.Status.IsWonThisCalculation != Guid.Empty)
                {
                    var tigr = _gameGlobal.TigrThreeZeroList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());


                    if (tigr == null)
                    {
                        _gameGlobal.TigrThreeZeroList.Add(new Tigr.ThreeZeroClass(player.GetPlayerId(),
                            game.GameId,
                            player.Status.IsWonThisCalculation));
                    }
                    else
                    {
                        var enemy = tigr.FriendList.Find(x =>
                            x.EnemyPlayerId == player.Status.IsWonThisCalculation);
                        if (enemy != null)
                        {
                            enemy.WinsSeries++;

                            if (enemy.WinsSeries >= 3 && enemy.IsUnique)
                            {
                                player.Status.AddRegularPoints(3, "3-0 обоссан");
                                player.Character.AddExtraSkill(player.Status, 30, "3-0 обоссан");


                                var enemyAcc = game.PlayersList.Find(x =>
                                    x.GetPlayerId() == player.Status.IsWonThisCalculation);

                                if (enemyAcc != null)
                                {
                                    enemyAcc.Character.AddIntelligence(enemyAcc.Status, -1, "3-0 обоссан");

                                    enemyAcc.Character.AddPsyche(enemyAcc.Status, -1, "3-0 обоссан");
                                    enemyAcc.MinusPsycheLog(game);
                                    game.Phrases.TigrThreeZero.SendLog(player, false);


                                    enemy.IsUnique = false;
                                }
                            }
                        }
                        else
                        {
                            tigr.FriendList.Add(new Tigr.ThreeZeroSubClass(player.Status.IsWonThisCalculation));
                        }
                    }
                }
                else
                {
                    var tigr = _gameGlobal.TigrThreeZeroList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());

                    var enemy = tigr?.FriendList.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);

                    if (enemy != null && enemy.IsUnique) enemy.WinsSeries = 0;
                }

                //end 3-0 обоссан: 

                /*//Тигр топ, а ты холоп: 
                if (player.Status.IsLostThisCalculation != Guid.Empty && player.Status.PlaceAtLeaderBoard == 1)
                {
                    player.Character.Justice.AddJusticeForNextRoundFromSkill(-1);
                }
                //end //Тигр топ, а ты холоп*/
                break;
            case "Братишка":
                //Челюсти: 
                if (player.Status.IsWonThisCalculation != Guid.Empty)
                {
                    var shark = _gameGlobal.SharkJawsWin.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());


                    if (!shark.FriendList.Contains(player.Status.IsWonThisCalculation))
                    {
                        shark.FriendList.Add(player.Status.IsWonThisCalculation);
                        player.Character.AddSpeed(player.Status, 1, "Челюсти");
                    }
                }
                //end Челюсти: 

                //ничего не понимает
                var btratishkaDontUnderstand = _gameGlobal.SharkDontUnderstand.Find(x =>
                    x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);
                if (btratishkaDontUnderstand != null)
                    if (btratishkaDontUnderstand.CurrentAttacker != Guid.Empty)
                    {
                        game.PlayersList.Find(x => x.GetPlayerId() == btratishkaDontUnderstand.CurrentAttacker).Character.ExtraWeight = 0;
                        btratishkaDontUnderstand.CurrentAttacker = Guid.Empty;
                    }

                //end ничего не понимает

                break;
            case "Загадочный Спартанец в маске":
                //Первая кровь: 
                var Spartan = _gameGlobal.SpartanFirstBlood.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());

                if (Spartan.FriendList.Count == 1)
                {
                    if (Spartan.FriendList.Contains(player.Status.IsWonThisCalculation))
                    {
                        player.Character.AddSpeed(player.Status, 1, "Первая кровь");
                        game.AddGlobalLogs("Они познают войну!\n");
                    }
                    else if (Spartan.FriendList.Contains(player.Status.IsLostThisCalculation))
                    {
                        var ene = game.PlayersList.Find(x =>
                            x.GetPlayerId() == player.Status.IsLostThisCalculation);
                        ene.Character.AddSpeed(ene.Status, 1, "Первая кровь");
                    }

                    Spartan.FriendList.Add(Guid.Empty);
                }
                //end Первая кровь: 

                //Это привилегия - умереть от моей руки
                if (player.Status.IsWonThisCalculation != Guid.Empty && game.RoundNo > 4)
                {
                    game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation).Character.Justice
                        .AddJusticeForNextRoundFromSkill();
                    player.Character.AddIntelligence(player.Status, -1, "Это привилегия");
                }
                //end Это привилегия - умереть от моей руки

                //Им это не понравится: 
                var SpartanTheyWontLikeIt = _gameGlobal.SpartanMark.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());

                if (SpartanTheyWontLikeIt.FriendList.Contains(player.Status.IsWonThisCalculation))
                {
                    player.Status.AddRegularPoints(1, "Им это не понравится");
                    player.Status.AddBonusPoints(1, "Им это не понравится");
                }

                //end Им это не понравится: 
                break;
            case "Вампур":
                //Гематофагия

                var vampyr = _gameGlobal.VampyrHematophagiaList.Find(x =>
                    x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                if (player.Status.IsWonThisCalculation != Guid.Empty)
                {
                    var target = vampyr.Hematophagia.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);
                    if (target == null)
                    {
                        var statIndex = 0;

                        var found = false;
                        while (!found)
                        {
                            statIndex = _rand.Random(1, 4);
                            switch (statIndex)
                            {
                                case 1:
                                    if (player.Character.GetIntelligence() < 10)
                                    {
                                        player.Character.AddIntelligence(player.Status, 2, "Гематофагия");
                                        found = true;
                                    }

                                    break;
                                case 2:
                                    if (player.Character.GetStrength() < 10)
                                    {
                                        player.Character.AddStrength(player.Status, 2, "Гематофагия");
                                        found = true;
                                    }

                                    break;
                                case 3:
                                    if (player.Character.GetSpeed() < 10)
                                    {
                                        player.Character.AddSpeed(player.Status, 2, "Гематофагия");
                                        found = true;
                                    }

                                    break;
                                case 4:
                                    if (player.Character.GetPsyche() < 10)
                                    {
                                        player.Character.AddPsyche(player.Status, 2, "Гематофагия");
                                        found = true;
                                    }

                                    break;
                            }
                        }

                        vampyr.Hematophagia.Add(new Vampyr.HematophagiaSubClass(statIndex,
                            player.Status.IsWonThisCalculation));
                    }
                }

                if (player.Status.IsLostThisCalculation != Guid.Empty)
                {
                    var target = vampyr.Hematophagia.Find(x => x.EnemyId == player.Status.IsLostThisCalculation);

                    if (target != null)
                    {
                        vampyr.Hematophagia.Remove(target);
                        switch (target.StatIndex)
                        {
                            case 1:
                                player.Character.AddIntelligence(player.Status, -2, "СОсиновый кол");
                                player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                break;
                            case 2:
                                player.Character.AddStrength(player.Status, -2, "СОсиновый кол");
                                player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                break;
                            case 3:
                                player.Character.AddSpeed(player.Status, -2, "СОсиновый кол");
                                player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                break;
                            case 4:
                                player.Character.AddPsyche(player.Status, -2, "СОсиновый кол");
                                player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                break;
                        }
                    }
                    else
                    {
                        if (vampyr.Hematophagia.Count > 0)
                        {
                            var randomIndex = _rand.Random(0, vampyr.Hematophagia.Count - 1);
                            target = vampyr.Hematophagia[randomIndex];
                            vampyr.Hematophagia.Remove(target);
                            switch (target.StatIndex)
                            {
                                case 1:
                                    player.Character.AddIntelligence(player.Status, -2, "СОсиновый кол");
                                    player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                    break;
                                case 2:
                                    player.Character.AddStrength(player.Status, -2, "СОсиновый кол");
                                    player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                    break;
                                case 3:
                                    player.Character.AddSpeed(player.Status, -2, "СОсиновый кол");
                                    player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                    break;
                                case 4:
                                    player.Character.AddPsyche(player.Status, -2, "СОсиновый кол");
                                    player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                    break;
                            }
                        }
                    }
                }

                //end Гематофагия
                break;
        }
    }
    //end handle during fight

    //this should not exist, but it works, so don't touch
    public void HandleCharacterWithKnownEnemyBeforeFight(GamePlayerBridgeClass player, GameClass game)
    {
        var characterName = player.Character.Name;
        switch (characterName)
        {
            case "Толя":

                //Подсчет
                var tolya = _gameGlobal.TolyaCount.Find(x =>
                    x.GameId == player.GameId &&
                    x.PlayerId == player.GetPlayerId());

                if (tolya.IsReadyToUse && player.Status.WhoToAttackThisTurn != Guid.Empty)
                {
                    tolya.TargetList.Add(new Tolya.TolyaCountSubClass(player.Status.WhoToAttackThisTurn,
                        game.RoundNo));
                    tolya.IsReadyToUse = false;
                    tolya.Cooldown = _rand.Random(4, 5);
                }

                //Подсчет end
                break;

            case "DeepList":
                //Сомнительная тактика
                var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                    x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);


                if (deep != null)
                    if (player.Status.IsFighting != Guid.Empty)
                    {
                        var target = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsFighting);
                        if (!deep.FriendList.Contains(player.Status.IsFighting) && !target.Status.IsSkip &&
                            !target.Status.IsBlock) player.Status.IsAbleToWin = false;
                    }


                //end Сомнительная тактика
                break;
        }
    }
    //

    //after all fight

    public void HandleEndOfRound(GameClass game)
    {
        foreach (var player in game.PlayersList)
        {
            if (player == null) _log.Critical("HandleEndOfRound - octopus == null");

            var characterName = player?.Character.Name;
            if (characterName == null) return;

            switch (characterName)
            {
                case "Тигр":
                    //Лучше с двумя, чем с адекватными:
                    for (var i = 0; i < game.PlayersList.Count; i++)
                    {
                        var t = game.PlayersList[i];
                        if (t.Character.GetIntelligence() == player.Character.GetIntelligence() ||
                            t.Character.GetPsyche() == player.Character.GetPsyche())
                        {
                            var tigr = _gameGlobal.TigrTwoBetterList.Find(x =>
                                x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                            if (!tigr.FriendList.Contains(t.GetPlayerId()))
                            {
                                tigr.FriendList.Add(t.GetPlayerId());
                                // me.Status.AddRegularPoints();
                                player.Status.AddBonusPoints(3, "Лучше с двумя, чем с адекватными");
                                game.Phrases.TigrTwoBetter.SendLog(player, false);
                            }
                        }
                    }

                    //end Лучше с двумя, чем с адекватными:
                    break;

                case "DeepList":

                    //Безумие
                    var madd = _gameGlobal.DeepListMadnessList.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId &&
                        x.RoundItTriggered == game.RoundNo);

                    if (madd != null)
                    {
                        var regularStats = madd.MadnessList.Find(x => x.Index == 1);
                        var madStats = madd.MadnessList.Find(x => x.Index == 2);


                        var intel = player.Character.GetIntelligence() - madStats.Intel;
                        var str = player.Character.GetStrength() - madStats.Str;
                        var speed = player.Character.GetSpeed() - madStats.Speed;
                        var psy = player.Character.GetPsyche() - madStats.Psyche;


                        player.Character.SetIntelligence(player.Status, regularStats.Intel + intel, "Безумие",
                            false);
                        player.Character.SetStrength(player.Status, regularStats.Str + str, "Безумие", false);
                        player.Character.SetSpeed(player.Status, regularStats.Speed + speed, "Безумие", false);
                        player.Character.SetPsyche(player.Status, regularStats.Psyche + psy, "Безумие", false);
                        player.Character.SetSkillMultiplier();
                        _gameGlobal.DeepListMadnessList.Remove(madd);
                    }

                    // end Безумие 
                    break;
                case "Глеб":
                    //Претендент русского сервера
                    var glebChall = _gameGlobal.GlebChallengerList.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId &&
                        x.RoundItTriggered == game.RoundNo);


                    if (glebChall != null)
                    {
                        //x3 point:
                        player.Status.SetScoresToGiveAtEndOfRound((int)player.Status.GetScoresToGiveAtEndOfRound() * 3,
                            "Претендент русского сервера");
                        //end x3 point:

                        var regularStats = glebChall.MadnessList.Find(x => x.Index == 1);
                        var madStats = glebChall.MadnessList.Find(x => x.Index == 2);


                        var intel = player.Character.GetIntelligence() - madStats.Intel;
                        var str = player.Character.GetStrength() - madStats.Str;
                        var speed = player.Character.GetSpeed() - madStats.Speed;
                        var psy = player.Character.GetPsyche() - madStats.Psyche;


                        player.Character.SetIntelligence(player.Status, regularStats.Intel + intel,
                            "Претендент русского сервера", false);
                        player.Character.SetStrength(player.Status, regularStats.Str + str,
                            "Претендент русского сервера", false);
                        player.Character.SetSpeed(player.Status, regularStats.Speed + speed,
                            "Претендент русского сервера", false);
                        player.Character.SetPsyche(player.Status, regularStats.Psyche + psy,
                            "Претендент русского сервера", false);
                        player.Character.AddExtraSkill(player.Status, -99, "Претендент русского сервера", false);
                        _gameGlobal.GlebChallengerList.Remove(glebChall);
                    }
                    //end Претендент русского сервера

                    break;
                case "Краборак":
                    //Хождение боком:
                    var craboRack = _gameGlobal.CraboRackSidewaysBooleList.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId &&
                        x.RoundItTriggered == game.RoundNo);

                    if (craboRack != null)
                    {
                        var regularStats = craboRack.MadnessList.Find(x => x.Index == 1);
                        var madStats = craboRack.MadnessList.Find(x => x.Index == 2);
                        var speed = player.Character.GetSpeed() - madStats.Speed;
                        player.Character.SetSpeed(player.Status, regularStats.Speed + speed, "Хождение боком", false);
                        _gameGlobal.CraboRackSidewaysBooleList.Remove(craboRack);
                    }

                    //end Хождение боком
                    break;
                case "LeCrisp":


                    //Гребанные ассассин
                    var leCrip = _gameGlobal.LeCrispAssassins.Find(x =>
                        x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);

                    if (leCrip.AdditionalPsycheCurrent > 0)
                        player.Character.AddPsyche(player.Status, leCrip.AdditionalPsycheCurrent * -1,
                            "Гребанные ассассины", false);
                    if (leCrip.AdditionalPsycheForNextRound > 0)
                        player.Character.AddPsyche(player.Status, leCrip.AdditionalPsycheForNextRound,
                            "Гребанные ассассины");

                    leCrip.AdditionalPsycheCurrent = leCrip.AdditionalPsycheForNextRound;
                    leCrip.AdditionalPsycheForNextRound = 0;

                    //end Гребанные ассассин

                    //Импакт
                    var leImpact = _gameGlobal.LeCrispImpact.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                    if (leImpact.IsLost)
                    {
                        leImpact.ImpactTimes = 0;
                    }
                    else
                    {
                        leImpact.ImpactTimes += 1;
                        player.Status.AddBonusPoints(1, "Импакт");
                        player.Character.Justice.AddJusticeForNextRoundFromSkill();
                        game.Phrases.LeCrispImpactPhrase.SendLog(player, false, $"(x{leImpact.ImpactTimes}) ");
                    }

                    leImpact.IsLost = false;
                    //end Импакт


                    break;

                case "Толя":
                    //Великий Комментатор
                    if (game.RoundNo >= 3 && game.RoundNo <= 6)
                    {
                        var randNum = _rand.Random(1, 5);
                        if (randNum == 1)
                        {
                            var tolyaTalked = _gameGlobal.TolyaTalked.Find(x =>
                                x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());
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
                                    $"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {randomPlayer.Character.Name}");
                            }
                        }
                    }
                    //end Великий Комментатор

                    //Раммус мейн
                    var tolya = _gameGlobal.TolyaRammusTimes.Find(x =>
                        x.GameId == player.GameId &&
                        x.PlayerId == player.GetPlayerId());
                    if (tolya != null)
                    {
                        switch (tolya.FriendList.Count)
                        {
                            case 1:
                                game.Phrases.TolyaRammusPhrase.SendLog(player, false);
                                player.Character.Justice.AddJusticeForNextRoundFromSkill();
                                break;
                            case 2:
                                game.Phrases.TolyaRammus2Phrase.SendLog(player, false);
                                player.Character.Justice.AddJusticeForNextRoundFromSkill(2);
                                break;
                            case 3:
                                game.Phrases.TolyaRammus3Phrase.SendLog(player, false);
                                player.Character.Justice.AddJusticeForNextRoundFromSkill(3);
                                break;
                            case 4:
                                game.Phrases.TolyaRammus4Phrase.SendLog(player, false);
                                player.Character.Justice.AddJusticeForNextRoundFromSkill(4);
                                break;
                            case 5:
                                game.Phrases.TolyaRammus5Phrase.SendLog(player, false);
                                player.Character.Justice.AddJusticeForNextRoundFromSkill(5);
                                break;
                        }

                        tolya.FriendList.Clear();
                    }

                    //end Раммус мейн
                    break;

                case "Осьминожка":

                    //привет со дна
                    var extraPoints = game.SkipPlayersThisRound + game.PlayersList.Count(p => p.Status.IsBlock);
                    if (extraPoints > 0)
                        player.Status.AddBonusPoints(extraPoints, "Привет со дна");
                    //end привет со дна


                    break;

                case "Sirinoks":
                    //Обучение

                    var siri = _gameGlobal.SirinoksTraining.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());

                    if (siri != null && siri.Training.Count >= 1)
                    {
                        var stats = siri.Training.OrderByDescending(x => x.StatNumber).ToList().First();

                        switch (stats.StatIndex)
                        {
                            case 1:
                                player.Character.AddIntelligence(player.Status, 1, "Обучение");
                                if (player.Character.GetIntelligence() == stats.StatNumber)
                                    if (!siri.TriggeredBonusFromStat.Contains(stats.StatIndex))
                                    {
                                        player.Character.AddMoral(player.Status, 3, "Обучение");
                                        siri.TriggeredBonusFromStat.Add(stats.StatIndex);
                                    }

                                break;
                            case 2:
                                player.Character.AddStrength(player.Status, 1, "Обучение");
                                if (player.Character.GetStrength() == stats.StatNumber)
                                    if (!siri.TriggeredBonusFromStat.Contains(stats.StatIndex))
                                    {
                                        player.Character.AddMoral(player.Status, 3, "Обучение");
                                        siri.TriggeredBonusFromStat.Add(stats.StatIndex);
                                    }

                                break;
                            case 3:
                                player.Character.AddSpeed(player.Status, 1, "Обучение");
                                if (player.Character.GetSpeed() == stats.StatNumber)
                                    if (!siri.TriggeredBonusFromStat.Contains(stats.StatIndex))
                                    {
                                        player.Character.AddMoral(player.Status, 3, "Обучение");
                                        siri.TriggeredBonusFromStat.Add(stats.StatIndex);
                                    }

                                break;
                            case 4:
                                player.Character.AddPsyche(player.Status, 1, "Обучение");
                                if (player.Character.GetPsyche() == stats.StatNumber)
                                    if (!siri.TriggeredBonusFromStat.Contains(stats.StatIndex))
                                    {
                                        player.Character.AddMoral(player.Status, 3, "Обучение");
                                        siri.TriggeredBonusFromStat.Add(stats.StatIndex);
                                    }

                                break;
                        }
                    }

                    //end Обучение
                    break;

                case "HardKitty":
                    //Одиночество
                    var hard = _gameGlobal.HardKittyLoneliness.Find(x =>
                        x.GameId == player.GameId && x.PlayerId == player.GetPlayerId());
                    if (hard != null) hard.Activated = false;
                    //Одиночество
                    break;


                case "Загадочный Спартанец в маске":

                    //Им это не понравится
                    if (game.RoundNo == 2 || game.RoundNo == 4 || game.RoundNo == 6 || game.RoundNo == 8)
                    {
                        var Spartan = _gameGlobal.SpartanMark.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());
                        Spartan.FriendList.Clear();

                        Guid enemy1;
                        Guid enemy2;

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy1 = game.PlayersList[randIndex].GetPlayerId();
                            if (game.PlayersList[randIndex].Character.Name is "Глеб" or "mylorik" or
                                "Загадочный Спартанец в маске")
                                enemy1 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].Character.Name is "Mit*suki*" && game.RoundNo < 4)
                                enemy1 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].Character.Name is "Вампур" && game.RoundNo >= 4)
                                enemy1 = player.GetPlayerId();
                        } while (enemy1 == player.GetPlayerId());

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy2 = game.PlayersList[randIndex].GetPlayerId();
                            if (game.PlayersList[randIndex].Character.Name is "Глеб" or "mylorik" or
                                "Загадочный Спартанец в маске")
                                enemy2 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].Character.Name is "Mit*suki*" && game.RoundNo < 4)
                                enemy2 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].Character.Name is "Вампур" && game.RoundNo >= 4)
                                enemy2 = player.GetPlayerId();
                            if (enemy2 == enemy1)
                                enemy2 = player.GetPlayerId();
                        } while (enemy2 == player.GetPlayerId());


                        Spartan.FriendList.Add(enemy2);
                        Spartan.FriendList.Add(enemy1);
                    }
                    //end Им это не понравится

                    break;
                case "Mit*suki*":

                    //Дерзкая школота:
                    if (!player.Status.IsSkip)
                    {
                        player.Character.AddExtraSkill(player.Status, -20, "Дерзкая школота");

                        var randStat1 = _rand.Random(1, 4);
                        var randStat2 = _rand.Random(1, 4);
                        switch (randStat1)
                        {
                            case 1:
                                player.Character.AddIntelligence(player.Status, -1, "Дерзкая школота");
                                break;
                            case 2:
                                player.Character.AddStrength(player.Status, -1, "Дерзкая школота");
                                break;
                            case 3:
                                player.Character.AddSpeed(player.Status, -1, "Дерзкая школота");
                                break;
                            case 4:
                                player.Character.AddPsyche(player.Status, -1, "Дерзкая школота");
                                break;
                        }

                        switch (randStat2)
                        {
                            case 1:
                                player.Character.AddIntelligence(player.Status, -1, "Дерзкая школота");
                                break;
                            case 2:
                                player.Character.AddStrength(player.Status, -1, "Дерзкая школота");
                                break;
                            case 3:
                                player.Character.AddSpeed(player.Status, -1, "Дерзкая школота");
                                break;
                            case 4:
                                player.Character.AddPsyche(player.Status, -1, "Дерзкая школота");
                                break;
                        }
                    }


                    //end  Дерзкая школота

                    //Много выебывается
                    if (true)
                    {
                        var noAttack = true;

                        foreach (var target in game.PlayersList)
                        {
                            if (target.GetPlayerId() == player.GetPlayerId()) continue;
                            if (target.Status.WhoToAttackThisTurn == player.GetPlayerId())
                                noAttack = false;
                        }

                        if (noAttack)
                        {
                            player.Status.AddRegularPoints(1, "Много выебывается");
                            game.Phrases.MitsukiTooMuchFuckingNoAttack.SendLog(player, true);
                        }
                    }
                    //end Много выебывается

                    break;
                case "Вампур":
                    //Вампуризм
                    var vampyr = _gameGlobal.VampyrHematophagiaList.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);
                    if (vampyr.Hematophagia.Count > 0)
                        if (game.RoundNo is 2 or 4 or 6 or 8 or 10)
                            player.Character.AddMoral(player.Status, vampyr.Hematophagia.Count, "Вампуризм");
                    //end Вампуризм
                    break;
            }
        }
    }


    public async Task HandleNextRound(GameClass game)
    {
        foreach (var player in game.PlayersList)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "Осьминожка":
                    //Чернильная завеса
                    if (game.RoundNo == 11)
                    {
                        var octopusInk = _gameGlobal.OctopusInkList.Find(x => x.GameId == game.GameId);
                        var octopusInv = _gameGlobal.OctopusInvulnerabilityList.Find(x => x.GameId == game.GameId);

                        if (octopusInk != null)
                            foreach (var t in octopusInk.RealScoreList)
                            {
                                var pl = game.PlayersList.Find(x => x.GetPlayerId() == t.PlayerId);
                                pl?.Status.AddBonusPoints(t.RealScore, "🐙");
                            }

                        if (octopusInv != null)
                        {
                            var octoPlayer =
                                game.PlayersList.Find(x => x.GetPlayerId() == octopusInv.PlayerId);
                            octoPlayer.Status.AddBonusPoints(octopusInv.Count, "🐙");
                        }

                        //sort
                        //     game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
                        //    for (var i = 0; i < game.PlayersList.Count; i++) game.PlayersList[i].Status.PlaceAtLeaderBoard = i + 1;
                        //end sorting
                    }

                    //end  Чернильная завеса
                    break;
                case "Загадочный Спартанец в маске":
                    //Они позорят военное искусство:
                    if (game.RoundNo == 10)
                        player.Character.SetStrength(player.Status, 0, "Они позорят военное искусство");
                    //end Они позорят военное искусство:
                    break;

                case "mylorik":
                    //Буль
                    if (player.Character.GetPsyche() < 7)
                    {
                        var random = _rand.Random(1, 4 + player.Character.GetPsyche() * 6);

                        if (random == 2)
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsAbleToTurn = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = Guid.Empty;

                            game.Phrases.MylorikBoolePhrase.SendLog(player, false);
                        }
                    }
                    //end Буль

                    // Повторяет за myloran
                    if (game.RoundNo == 5)
                    {
                        player.Status.AddInGamePersonalLogs(
                            "ZaRDaK: Ты никогда не возьмешь даймонд, Лорик. Удачи в промо.");
                        player.Status.AddInGamePersonalLogs("\nmylorik: ММММММММММ!!!!!  +5 Силы.\n");
                        player.Character.AddStrength(player.Status, 5, "Повторяет за myloran", false);
                    }

                    if (game.RoundNo == 10)
                    {
                        player.Status.AddInGamePersonalLogs(
                            "ZaRDaK: Ты так и не апнул чалланджер? Хах, неудивительно. ");
                        player.Status.AddInGamePersonalLogs(
                            "\nmylorik закупился у продавца сомнительных тактик: +228 *Скилла*!\n");
                        player.Character.AddExtraSkill(player.Status, 228, "Повторяет за myloran", false);
                    }
                    //end Повторяет за myloran

                    break;

                case "Тигр":

                    //Стримснайпят и банят и банят и банят:
                    if (game.RoundNo == 10)
                    {
                        player.Status.IsSkip = true;
                        player.Status.ConfirmedSkip = false;
                        player.Status.IsBlock = false;
                        player.Status.IsAbleToTurn = false;
                        player.Status.IsReady = true;
                        player.Status.WhoToAttackThisTurn = Guid.Empty;
                        player.Character.SetPsyche(player.Status, 0, "Стримснайпят и банят и банят и банят");
                        player.Character.SetIntelligence(player.Status, 0,
                            "Стримснайпят и банят и банят и банят");
                        player.Character.SetStrength(player.Status, 10, "Стримснайпят и банят и банят и банят");
                        game.AddGlobalLogs(
                            $"{player.DiscordUsername}: ЕБАННЫЕ БАНЫ НА 10 ЛЕТ");
                        continue;
                    }
                    //end Стримснайпят и банят и банят и банят:

                    //Тигр топ, а ты холоп:

                    var tigr = _gameGlobal.TigrTopWhen.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId &&
                        x.WhenToTrigger.Contains(game.RoundNo));

                    if (tigr != null)
                    {
                        var tigr2 = _gameGlobal.TigrTop.Find(x =>
                            x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                        if (tigr2 == null)
                        {
                            _gameGlobal.TigrTop.Add(new Tigr.TigrTopClass(player.GetPlayerId(),
                                game.GameId));
                        }
                        else
                        {
                            _gameGlobal.TigrTop.Remove(tigr2);
                            _gameGlobal.TigrTop.Add(new Tigr.TigrTopClass(player.GetPlayerId(),
                                game.GameId));
                        }
                    }

                    //end Тигр топ, а ты холоп:

                    break;


                case "Darksci":


                    break;


                case "Mit*suki*":
                    //Дерзкая школота
                    if (game.RoundNo == 1)
                    {
                        game.Phrases.MitsukiCheekyBriki.SendLog(player, true);
                        player.Status.AddRegularPoints(1, "Много выебывается");
                        game.Phrases.MitsukiTooMuchFucking.SendLog(player, false);
                    }
                    //end Дерзкая школота

                    //Школьник
                    var acc = _gameGlobal.MitsukiNoPcTriggeredWhen.Find(x =>
                        x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);

                    if (acc != null)
                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsAbleToTurn = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = Guid.Empty;

                            game.Phrases.MitsukiSchoolboy.SendLog(player, true);
                            player.Character.Justice.AddJusticeForNextRoundFromSkill(5);
                        }

                    //end Школьник
                    break;
                case "AWDKA":

                    //АФКА
                    var afkaChance = 32 - (game.RoundNo - player.Character.LastMoralRound) * 4;
                    if (afkaChance <= 0)
                        afkaChance = 1;
                    if (_rand.Random(1, afkaChance) == 1)
                    {
                        player.Status.IsSkip = true;
                        player.Status.ConfirmedSkip = false;
                        player.Status.IsBlock = false;
                        player.Status.IsAbleToTurn = false;
                        player.Status.IsReady = true;
                        player.Status.WhoToAttackThisTurn = Guid.Empty;

                        game.Phrases.AwdkaAfk.SendLog(player, true);
                    }
                    //end АФКА

                    //Я пытаюсь!:
                    var awdkaa = _gameGlobal.AwdkaTryingList.Find(x =>
                        x.GameId == player.GameId &&
                        x.PlayerId == player.GetPlayerId());

                    foreach (var enemy in awdkaa.TryingList)
                        if (enemy != null)
                            if (enemy.Times >= 2 && enemy.IsUnique == false)
                            {
                                player.Status.LvlUpPoints += 2;
                                player.Character.AddExtraSkill(player.Status, 20, "Я пытаюсь!");
                                await _gameUpdateMess.UpdateMessage(player);
                                enemy.IsUnique = true;
                                game.Phrases.AwdkaTrying.SendLog(player, true);
                            }
                    //end Я пытаюсь!:


                    //Научите играть 
                    var awdkaTempStats = _gameGlobal.AwdkaTeachToPlayTempStats.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                    var awdka = _gameGlobal.AwdkaTeachToPlay.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                    //remove stats from previos time
                    if (awdkaTempStats != null)
                    {
                        var regularStats = awdkaTempStats.MadnessList.Find(x => x.Index == 1);
                        var madStats = awdkaTempStats.MadnessList.Find(x => x.Index == 2);

                        var intel = player.Character.GetIntelligence() - madStats.Intel;
                        var str = player.Character.GetStrength() - madStats.Str;
                        var speed = player.Character.GetSpeed() - madStats.Speed;
                        var psy = player.Character.GetPsyche() - madStats.Psyche;

                        var intelToGive = regularStats.Intel + intel;
                        if (intelToGive > 10)
                            intelToGive = 10;
                        player.Character.SetIntelligence(player.Status, intelToGive, "Научите играть");
                        player.Character.SetStrength(player.Status, regularStats.Str + str, "Научите играть");
                        player.Character.SetSpeed(player.Status, regularStats.Speed + speed, "Научите играть");
                        player.Character.SetPsyche(player.Status, regularStats.Psyche + psy, "Научите играть");
                        player.Character.SetIntelligenceExtraText("");
                        player.Character.SetStrengthExtraText("");
                        player.Character.SetSpeedExtraText("");
                        player.Character.SetPsycheExtraText("");
                        _gameGlobal.AwdkaTeachToPlayTempStats.Remove(awdkaTempStats);
                    }
                    //end remove stats

                    //if there is no one have been attacked from awdka
                    if (awdka == null) continue;
                    //end if there..

                    //crazy shit
                    _gameGlobal.AwdkaTeachToPlayTempStats.Add(new DeepList.Madness(player.GetPlayerId(),
                        game.GameId, game.RoundNo));

                    awdkaTempStats = _gameGlobal.AwdkaTeachToPlayTempStats.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                    awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                        player.Character.GetStrength(), player.Character.GetSpeed(), player.Character.GetPsyche()));
                    //end crazy shit

                    //find out  the biggest stat
                    var bestSkill = awdka.Training.OrderByDescending(x => x.StatNumber).ToList().First();

                    var intel1 = player.Character.GetIntelligence();
                    var str1 = player.Character.GetStrength();
                    var speed1 = player.Character.GetSpeed();
                    var pshy1 = player.Character.GetPsyche();

                    switch (bestSkill.StatIndex)
                    {
                        case 1:
                            intel1 = bestSkill.StatNumber;
                            player.Character.SetIntelligenceExtraText(
                                $" (<:volibir:894286361895522434> Интеллект {intel1})");
                            break;
                        case 2:
                            str1 = bestSkill.StatNumber;
                            player.Character.SetStrengthExtraText($" (<:volibir:894286361895522434> Сила {str1})");
                            break;
                        case 3:
                            speed1 = bestSkill.StatNumber;
                            player.Character.SetSpeedExtraText(
                                $" (<:volibir:894286361895522434> Скорость {speed1})");
                            break;
                        case 4:
                            pshy1 = bestSkill.StatNumber;
                            player.Character.SetPsycheExtraText(
                                $" (<:volibir:894286361895522434> Психика {pshy1})");
                            break;
                    }

                    if (intel1 >= player.Character.GetIntelligence())
                        player.Character.SetIntelligence(player.Status, intel1, "Научите играть");

                    if (str1 >= player.Character.GetStrength())
                        player.Character.SetStrength(player.Status, str1, "Научите играть");

                    if (speed1 >= player.Character.GetSpeed())
                        player.Character.SetSpeed(player.Status, speed1, "Научите играть");

                    if (pshy1 >= player.Character.GetPsyche())
                        player.Character.SetPsyche(player.Status, pshy1, "Научите играть");
                    //end find out  the biggest stat

                    //crazy shit 2
                    awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(2, intel1, str1, speed1, pshy1));
                    _gameGlobal.AwdkaTeachToPlay.Remove(awdka);
                    //end crazy shit 2

                    game.Phrases.AwdkaTeachToPlay.SendLog(player, true);

                    //end Научите играть: 
                    break;


                case "Глеб":

                    // Я за чаем:
                    var rand = _rand.Random(1, 8);

                    var glebChalleger = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                        x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);


                    if (glebChalleger.WhenToTrigger.Contains(game.RoundNo))
                        rand = _rand.Random(1, 7);


                    var glebTea =
                        _gameGlobal.GlebTea.Find(x =>
                            x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);

                    if (rand == 1)
                    {
                        glebTea.Ready = true;
                        glebTea.TimesRolled++;
                    }

                    if (game.RoundNo == 9 && glebTea.TimesRolled == 0)
                    {
                        glebTea.Ready = true;
                        player.Status.AddInGamePersonalLogs("Я за чаем: Глебка чай не пропускает!");
                    }

                    if (glebTea.Ready)
                        game.Phrases.GlebTeaReadyPhrase.SendLog(player, true);
                    //end  Я за чаем:


                    //Спящее хуйло:
                    acc = _gameGlobal.GlebSleepingTriggeredWhen.Find(x =>
                        x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);

                    if (acc != null)
                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsAbleToTurn = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = Guid.Empty;

                            player.Character.AddExtraSkill(player.Status, -30, "Спящее хуйло");

                            player.Character.AvatarCurrent = player.Character.AvatarEvent
                                .Find(x => x.EventName == "Спящее хуйло").Url;
                            game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                        }
                        else
                        {
                            player.Character.AvatarCurrent = player.Character.Avatar;
                        }

                    if (game.RoundNo == 11)
                    {
                        player.Character.AvatarCurrent =
                            player.Character.AvatarEvent.Find(x => x.EventName == "Спящее хуйло").Url;
                        game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                    }
                    //end Спящее хуйло:

                    //Претендент русского сервера: 
                    acc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                        x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);

                    if (game.RoundNo == 10 && !acc.WhenToTrigger.Contains(game.RoundNo) &&
                        player.Status.PlaceAtLeaderBoard > 2)
                    {
                        // шанс = 1 / (40 - место глеба в таблице * 4)
                        var bonusChallenger = _rand.Random(1, 40 - player.Status.PlaceAtLeaderBoard * 4);
                        if (bonusChallenger == 15) acc.WhenToTrigger.Add(game.RoundNo);
                    }

                    if (acc.WhenToTrigger.Contains(game.RoundNo))
                    {
                        var gleb = _gameGlobal.GlebChallengerList.Find(x =>
                            x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);
                        //just check
                        if (gleb != null) _gameGlobal.GlebChallengerList.Remove(gleb);

                        _gameGlobal.GlebChallengerList.Add(new DeepList.Madness(player.GetPlayerId(), game.GameId,
                            game.RoundNo));
                        gleb = _gameGlobal.GlebChallengerList.Find(x =>
                            x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);
                        gleb.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                            player.Character.GetStrength(), player.Character.GetSpeed(), player.Character.GetPsyche()));

                        //  var randomNumber =  _rand.Random(1, 100);

                        var intel = player.Character.GetIntelligence() >= 10 ? 10 : 9;
                        var str = player.Character.GetStrength() >= 10 ? 10 : 9;
                        var speed = player.Character.GetSpeed() >= 10 ? 10 : 9;
                        var pshy = player.Character.GetPsyche() >= 10 ? 10 : 9;


                        player.Character.SetIntelligence(player.Status, intel, "Претендент русского сервера");
                        player.Character.SetStrength(player.Status, str, "Претендент русского сервера");
                        player.Character.SetSpeed(player.Status, speed, "Претендент русского сервера");
                        player.Character.SetPsyche(player.Status, pshy, "Претендент русского сервера");
                        player.Character.AddExtraSkill(player.Status, 99, "Претендент русского сервера");


                        gleb.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));

                        game.Phrases.GlebChallengerPhrase.SendLog(player, true);
                        await game.Phrases.GlebChallengerSeparatePhrase.SendLogSeparate(player, true);
                    }

                    //end Претендент русского сервера
                    break;

                case "Краборак":
                    //Хождение боком:
                    acc = _gameGlobal.CraboRackSidewaysBooleTriggeredWhen.Find(x =>
                        x.PlayerId == player.GetPlayerId() && player.GameId == x.GameId);
                    if (acc != null)
                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            var craboRack = _gameGlobal.CraboRackSidewaysBooleList.Find(x =>
                                x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);
                            //just check
                            if (craboRack != null) _gameGlobal.CraboRackSidewaysBooleList.Remove(craboRack);

                            _gameGlobal.CraboRackSidewaysBooleList.Add(new DeepList.Madness(player.GetPlayerId(),
                                game.GameId, game.RoundNo));
                            craboRack = _gameGlobal.CraboRackSidewaysBooleList.Find(x =>
                                x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);
                            craboRack.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                                player.Character.GetStrength(), player.Character.GetSpeed(),
                                player.Character.GetPsyche()));


                            var speed = 10;

                            player.Character.SetSpeed(player.Status, speed, "Хождение боком");
                            craboRack.MadnessList.Add(new DeepList.MadnessSub(2, player.Character.GetIntelligence(),
                                player.Character.GetStrength(), speed, player.Character.GetPsyche()));
                            game.Phrases.CraboRackSidewaysBoolePhrase.SendLog(player, true);
                        }

                    //end Хождение боком
                    break;
                case "DeepList":

                    //Сверхразум
                    var currentDeepList = _gameGlobal.DeepListSupermindTriggeredWhen.Find(x =>
                        x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);

                    if (currentDeepList != null)
                        if (currentDeepList.WhenToTrigger.Any(x => x == game.RoundNo))
                        {
                            GamePlayerBridgeClass randPlayer;

                            do
                            {
                                randPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];

                                var check1 = _gameGlobal.DeepListSupermindKnown.Find(x =>
                                    x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                                if (check1 != null)
                                    if (check1.KnownPlayers.Contains(randPlayer.GetPlayerId()))
                                        randPlayer = player;
                            } while (randPlayer.GetPlayerId() == player.GetPlayerId());

                            var check = _gameGlobal.DeepListSupermindKnown.Find(x =>
                                x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                            if (check == null)
                                _gameGlobal.DeepListSupermindKnown.Add(new DeepList.SuperMindKnown(
                                    player.GetPlayerId(), game.GameId,
                                    randPlayer.GetPlayerId()));
                            else
                                check.KnownPlayers.Add(randPlayer.GetPlayerId());

                            game.Phrases.DeepListSuperMindPhrase.SendLog(player, randPlayer, true);
                        }
                    //end Сверхразум

                    //Безумие

                    var madd = _gameGlobal.DeepListMadnessTriggeredWhen.Find(x =>
                        x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                    if (madd != null)
                        if (madd.WhenToTrigger.Contains(game.RoundNo))
                        {
                            //trigger maddness
                            //player.Status.AddBonusPoints(-3, "Безумие");

                            var curr = _gameGlobal.DeepListMadnessList.Find(x =>
                                x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);
                            //just check
                            if (curr != null) _gameGlobal.DeepListMadnessList.Remove(curr);

                            _gameGlobal.DeepListMadnessList.Add(
                                new DeepList.Madness(player.GetPlayerId(), game.GameId, game.RoundNo));
                            curr = _gameGlobal.DeepListMadnessList.Find(x =>
                                x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);
                            curr.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                                player.Character.GetStrength(), player.Character.GetSpeed(),
                                player.Character.GetPsyche()));


                            var intel = 0;
                            var str = 0;
                            var speed = 0;
                            var pshy = 0;

                            for (var i = 0; i < 4; i++)
                            {
                                /*
                                var randomNumber = _rand.Random(1, 100);
                                var statNumber = 0;
                                switch (randomNumber)
                                {
                                    case int n when n == 1:
                                        statNumber = 1;
                                        break;
                                    case int n when n == 2 || n == 3:
                                        statNumber = 2;
                                        break;
                                    case int n when n == 4 || n == 5 || n == 6:
                                        statNumber = 3;
                                        break;
                                    case int n when n >= 7 && n <= 16:
                                        statNumber = 4;
                                        break;
                                    case int n when n >= 17 && n <= 31:
                                        statNumber = 5;
                                        break;
                                    case int n when n >= 32 && n <= 51:
                                        statNumber = 6;
                                        break;
                                    case int n when n >= 52 && n <= 71:
                                        statNumber = 7;
                                        break;
                                    case int n when n >= 72 && n <= 86:
                                        statNumber = 8;
                                        break;
                                    case int n when n >= 87 && n <= 96:
                                        statNumber = 9;
                                        break;
                                    case int n when n >= 97:
                                        statNumber = 10;
                                        break;
                                }*/
                                var statNumber = _rand.Random(0, 10);

                                if (i == 0)
                                    intel = statNumber;
                                else if (i == 1)
                                    str = statNumber;
                                else if (i == 2)
                                    speed = statNumber;
                                else if (i == 3) pshy = statNumber;
                            }

                            player.Character.SetIntelligence(player.Status, intel, "Безумие");
                            player.Character.SetStrength(player.Status, str, "Безумие");
                            player.Character.SetSpeed(player.Status, speed, "Безумие");
                            player.Character.SetPsyche(player.Status, pshy, "Безумие");
                            //2 это х3
                            player.Character.SetSkillMultiplier(3);
                            //player.Status.AddBonusPoints(-3, "Безумие");

                            game.Phrases.DeepListMadnessPhrase.SendLog(player, true);
                            curr.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));
                        }
                    //end Безумие

                    break;

                case "Sirinoks":

                    //Дракон
                    if (game.RoundNo == 10)
                    {
                        player.Character.SetIntelligence(player.Status, 10, "Дракон");
                        player.Character.SetStrength(player.Status, 10, "Дракон");
                        player.Character.SetSpeed(player.Status, 10, "Дракон");
                        player.Character.SetPsyche(player.Status, 10, "Дракон");

                        player.Character.AddExtraSkill(player.Status, (int)player.Character.GetSkill(), "Дракон");

                        var pointsToGive = (int)(player.Character.GetSkill() / 20);


                        var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());

                        if (siri != null)
                            for (var i = player.Status.PlaceAtLeaderBoard + 1; i < game.PlayersList.Count + 1; i++)
                            {
                                var player2 = game.PlayersList[i - 1];
                                if (siri.FriendList.Contains(player2.GetPlayerId()))
                                    pointsToGive -= 1;
                            }

                        player.Status.AddBonusPoints(pointsToGive, "Дракон");
                        game.Phrases.SirinoksDragonPhrase.SendLog(player, true);
                    }

                    //end Дракон
                    break;
                case "Вампур":
                    //vampyr unique
                    if (game.RoundNo == 1)
                    {
                        game.Phrases.VampyrVampyr.SendLog(player, true);
                        if (game.PlayersList.Any(x => x.Character.Name == "mylorik"))
                            game.AddGlobalLogs(
                                " \n<:Y_:562885385395634196> *mylorik: Гребанный Вампур!* <:Y_:562885385395634196>",
                                "\n\n");
                    }

                    //end vampyr unique
                    break;
            }

            //Я за чаем
            var isSkip = _gameGlobal.GlebTeaTriggeredWhen.Find(x =>
                x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId &&
                x.WhenToTrigger.Contains(game.RoundNo));

            if (isSkip != null)
            {
                player.Status.IsSkip = true;
                player.Status.ConfirmedSkip = false;
                player.Status.IsBlock = false;
                player.Status.IsAbleToTurn = false;
                player.Status.IsReady = true;
                player.Status.WhoToAttackThisTurn = Guid.Empty;
                player.Status.AddInGamePersonalLogs("Тебя усыпили...\n");
            }
            //end Я за чаем
        }
    }

    public void HandleNextRoundAfterSorting(GameClass game)
    {
        foreach (var player in game.PlayersList)
        {
            if (player == null) _log.Critical("HandleNextRoundAfterSorting - octopus == null");

            var characterName = player?.Character.Name;
            if (characterName == null) return;

            switch (characterName)
            {
                case "Братишка":
                    //Булькает:
                    if (player.Status.PlaceAtLeaderBoard != 1)
                        player.Character.Justice.AddFullJusticeNow();
                    //end Булькает:

                    //Челюсти:
                    if (game.RoundNo > 1)
                    {
                        var shark = _gameGlobal.SharkJawsLeader.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());


                        if (!shark.FriendList.Contains(player.Status.PlaceAtLeaderBoard))
                        {
                            shark.FriendList.Add(player.Status.PlaceAtLeaderBoard);
                            player.Character.AddSpeed(player.Status, 1, "Челюсти");
                        }
                    }

                    //end Челюсти:
                    break;

                case "Тигр":
                    //Тигр топ, а ты холоп: 
                    if (player.Status.PlaceAtLeaderBoard == 1 && game.RoundNo > 1)
                        if (game.RoundNo < 10)
                        {
                            player.Character.AddPsyche(player.Status, 1, "Тигр топ, а ты холоп");
                            player.Character.AddMoral(player.Status, 3, "Тигр топ, а ты холоп");
                            game.Phrases.TigrTop.SendLog(player, false);
                        }

                    //end Тигр топ, а ты холоп: 
                    break;

                case "Mit*suki*":
                    //Много выебывается:
                    if (player.Status.PlaceAtLeaderBoard == 1)
                    {
                        player.Status.AddRegularPoints(1, "Много выебывается");
                        game.Phrases.MitsukiTooMuchFucking.SendLog(player, false);
                    }

                    //end Много выебывается:

                    //Запах мусора:

                    if (game.RoundNo == 11)
                    {
                        var mitsuki = _gameGlobal.MitsukiGarbageList.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());
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

                    //end Запах мусора:
                    break;

                case "Осьминожка":
                    //Раскинуть щупальца:
                    if (game.RoundNo > 1)
                    {
                        var octo = _gameGlobal.OctopusTentaclesList.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.GetPlayerId());
                        if (!octo.LeaderboardPlace.Contains(player.Status.PlaceAtLeaderBoard))
                        {
                            octo.LeaderboardPlace.Add(player.Status.PlaceAtLeaderBoard);
                            player.Status.AddRegularPoints(1, "Раскинуть щупальца");
                        }
                    }

                    //end Раскинуть щупальца:
                    break;
                case "HardKitty":
                    //Никому не нужен:
                    if (game.RoundNo is 9 or 7 or 5 or 3)
                    {
                        var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                            x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);
                        foreach (var target in game.PlayersList)
                        {
                            if (player.GetPlayerId() == target.GetPlayerId()) continue;
                            var found = hardKitty.LostSeries.Find(x => x.EnemyPlayerId == target.GetPlayerId());

                            if (found != null)
                                found.Series += 1;
                            else
                                hardKitty.LostSeries.Add(new HardKitty.DoebatsyaSubClass(target.GetPlayerId()));
                        }
                    }

                    //end Никому не нужен:
                    break;

                case "Darksci":
                    //Не повезло
                    var darksciType = _gameGlobal.DarksciTypeList.Find(x =>
                        x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);

                    if (darksciType.IsStableType)
                    {
                        player.Character.AddExtraSkill(player.Status, 20, "Не повезло");
                        player.Character.AddMoral(player.Status, 2, "Не повезло");
                    }

                    //end Не повезло

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
                    if (game.RoundNo == 9 && player.Character.GetPsyche() < 4)
                        if (game.RoundNo == 9 ||
                            game.RoundNo == 10 && !game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                            game.AddGlobalLogs(
                                $"{player.DiscordUsername}: Нахуй эту игру..");
                    //end Да всё нахуй эту игру: Part #3


                    //Да всё нахуй эту игру (3, 6 and 9 are in LVL up): Part #1
                    if (game.RoundNo != 9 && game.RoundNo != 7 && game.RoundNo != 5 && game.RoundNo != 3)
                        if (player.Character.GetPsyche() <= 0)
                        {
                            player.Status.IsSkip = true;
                            player.Status.IsBlock = false;
                            player.Status.IsAbleToTurn = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = Guid.Empty;
                            game.Phrases.DarksciFuckThisGame.SendLog(player, true);

                            if (game.RoundNo == 9 ||
                                game.RoundNo == 10 && !game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                                game.AddGlobalLogs(
                                    $"{player.DiscordUsername}: Нахуй эту игру..");
                        }

                    //end Да всё нахуй эту игру: Part #1
                    break;
                case "Толя":

                    //Подсчет
                    var tolya = _gameGlobal.TolyaCount.Find(x =>
                        x.GameId == player.GameId &&
                        x.PlayerId == player.GetPlayerId());

                    tolya.Cooldown--;

                    if (tolya.Cooldown <= 0)
                    {
                        tolya.IsReadyToUse = true;
                        game.Phrases.TolyaCountReadyPhrase.SendLog(player, false);
                    }

                    //end Подсчет
                    break;
                case "mylorik":
                    if (player.Character.GetPsyche() == 10)
                    {
                        player.Character.Name = "Братишка";
                        _gameGlobal.SharkJawsLeader.Add(new Shark.SharkLeaderClass(player.GetPlayerId(), game.GameId));

                        _gameGlobal.SharkJawsWin.Add(new FriendsClass(player.GetPlayerId(), game.GameId));
                        _gameGlobal.SharkBoole.Add(new FriendsClass(player.GetPlayerId(), game.GameId));
                        player.Status.AddInGamePersonalLogs(
                            "Братишка: **Буууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууль**\n");
                    }

                    break;
            }
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
                    case 6:
                    case 7:
                    case 8:
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
                    case 6:
                    case 7:
                    case 8:
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


                switch (player.Character.Name)
                {
                    case "AWDKA":
                        try
                        {
                            if (lastRoundEvents.Contains("напал на игрока"))
                            {
                                var playerName = lastRoundEvents.Split("напал на игрока")[1].Split("\n")[0].TrimStart();
                                var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);

                                if (player.Character.GetIntelligenceString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.Character.GetIntelligenceString()
                                        .Replace("Интеллект ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("DeepList",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Mit*suki*",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Толя", playerClass.GetPlayerId()));
                                            break;
                                        case 7:
                                            break;
                                        case 6:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(
                                                    new PredictClass("Вампур", playerClass.GetPlayerId()));
                                            break;
                                        case 5:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Sirinoks",
                                                    playerClass.GetPlayerId()));
                                            break;
                                    }
                                }

                                if (player.Character.GetStrengthString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.Character.GetStrengthString().Replace("Сила ", "")
                                        .Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Загадочный Спартанец в маске",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
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

                                if (player.Character.GetSpeedString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.Character.GetSpeedString()
                                        .Replace("Скорость ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Краборак",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("mylorik",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
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

                                if (player.Character.GetPsycheString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.Character.GetPsycheString()
                                        .Replace("Психика ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:

                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Осьминожка", playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Краборак",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()) &&
                                                playerClass.Status.PlaceAtLeaderBoard == 6)
                                                player.Predict.Add(new PredictClass("HardKitty",
                                                    playerClass.GetPlayerId()));
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Глеб", playerClass.GetPlayerId()));
                                            break;
                                        case 7:
                                            if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()))
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
                        var deepList = _gameGlobal.DeepListSupermindKnown.Find(x =>
                            x.PlayerId == player.GetPlayerId() && x.GameId == game.GameId);

                        if (deepList != null)
                            foreach (var knownPlayer in deepList.KnownPlayers)
                            {
                                var playerClass = game.PlayersList.Find(x => x.GetPlayerId() == knownPlayer);

                                if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()) &&
                                    playerClass.GetPlayerId() != player.GetPlayerId())
                                    player.Predict.Add(new PredictClass(playerClass.Character.Name,
                                        playerClass.GetPlayerId()));
                            }

                        break;
                }

                //game.AddGlobalLogs($"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {randomPlayer.Character.Name}");
                //100%
                if (globalLogs.Contains("Толя запизделся"))
                {
                    var playerName = globalLogs.Split("запизделся и спалил")[1].Replace(", что ", "").Split(" - ")[^2];
                    var playerCharacter =
                        globalLogs.Split("запизделся и спалил")[1].Replace(", что ", "").Split(" - ")[^1]
                            .Replace("\n", "");
                    var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);
                    if (playerClass.GetPlayerId() != player.GetPlayerId())
                    {
                        if (player.Predict.Any(x => x.PlayerId == playerClass.GetPlayerId()))
                            player.Predict.Remove(player.Predict.Find(x => x.PlayerId == playerClass.GetPlayerId()));
                        player.Predict.Add(new PredictClass(playerCharacter, playerClass.GetPlayerId()));
                    }
                }

                //100%
                if (lastRoundEvents.Contains("Ничего не понимает"))
                {
                    var playerName = lastRoundEvents.Split(" напал на игрока ")[1].Split("\n")[0];
                    var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);

                    if (player.Predict.Any(x => x.PlayerId == playerClass.GetPlayerId()))
                        player.Predict.Remove(player.Predict.Find(x => x.PlayerId == playerClass.GetPlayerId()));
                    player.Predict.Add(new PredictClass("Братишка", playerClass.GetPlayerId()));
                }

                //not 100%
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
                            .Replace($" {player.DiscordUsername}", "").Replace("<:war:561287719838547981>", "").Trim();
                        var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);
                        if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()) &&
                            playerClass.GetPlayerId() != player.GetPlayerId())
                            player.Predict.Add(new PredictClass("Загадочный Спартанец в маске",
                                playerClass.GetPlayerId()));
                    }
                }

                //not 100%
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
                            .Replace($" {player.DiscordUsername}", "").Replace("<:war:561287719838547981>", "").Trim();
                        var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);
                        if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()) &&
                            playerClass.GetPlayerId() != player.GetPlayerId())
                            player.Predict.Add(new PredictClass("DeepList", playerClass.GetPlayerId()));
                    }
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
        //shark Лежит на дне:
        if (game.PlayersList.Any(x => x.Character.Name == "Братишка"))
        {
            var shark = game.PlayersList.Find(x => x.Character.Name == "Братишка");

            var enemyTop =
                game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard - 1 == shark.Status.PlaceAtLeaderBoard);
            var enemyBottom =
                game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard + 1 == shark.Status.PlaceAtLeaderBoard);
            if (enemyTop != null && enemyTop.Status.IsLostThisCalculation != Guid.Empty)
                shark.Status.AddRegularPoints(1, "Лежит на дне");

            if (enemyBottom != null && enemyBottom.Status.IsLostThisCalculation != Guid.Empty)
                shark.Status.AddRegularPoints(1, "Лежит на дне");
        }
        //end Лежит на дне:
    }

    public async Task<int> HandleJews(GamePlayerBridgeClass player, GameClass game)
    {
        //Еврей
        if (!game.PlayersList.Any(x => x.Character.Name is "LeCrisp" or "Толя")) return 1;
        if (player.Character.Name is "LeCrisp" or "Толя") return 1;

        var leCrisp = game.PlayersList.Find(x => x.Character.Name == "LeCrisp");
        var tolya = game.PlayersList.Find(x => x.Character.Name == "Толя");


        if (leCrisp != null && tolya != null)
            if (leCrisp.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn &&
                tolya.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn)
            {
                leCrisp.Status.AddRegularPoints(1, "Еврей");
                tolya.Status.AddRegularPoints(1, "Еврей");
                game.Phrases.TolyaJewPhrase.SendLog(tolya, true);
                game.Phrases.LeCrispJewPhrase.SendLog(leCrisp, true);

                if (!leCrisp.IsBot())
                    try
                    {
                        await _help.SendMsgAndDeleteItAfterRound(leCrisp, "__**МЫ**__ жрём деньги!");
                    }
                    catch (Exception exception)
                    {
                        _log.Critical(exception.Message);
                        _log.Critical(exception.StackTrace);
                    }

                if (!tolya.IsBot())
                    try
                    {
                        await _help.SendMsgAndDeleteItAfterRound(tolya, "__**МЫ**__ жрём деньги!");
                    }
                    catch (Exception exception)
                    {
                        _log.Critical(exception.Message);
                        _log.Critical(exception.StackTrace);
                    }

                return 0;
            }


        if (leCrisp != null)
            if (leCrisp.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn)
            {
                if (player.Character.Name == "DeepList")
                {
                    game.Phrases.LeCrispBoolingPhrase.SendLog(leCrisp, false);
                    return 1;
                }

                leCrisp.Status.AddRegularPoints(1, "Еврей");
                game.Phrases.LeCrispJewPhrase.SendLog(leCrisp, true);
                return 0;
            }

        if (tolya != null)
            if (tolya.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn)
            {
                tolya.Status.AddRegularPoints(1, "Еврей");
                game.Phrases.TolyaJewPhrase.SendLog(tolya, true);
                return 0;
            }

        return 1;
    }

    public async Task<int> HandleOctopus(GamePlayerBridgeClass octopus, GamePlayerBridgeClass attacker, GameClass game)
    {
        if (octopus.Character.Name != "Осьминожка") return 0;

        //deeplist
        if (attacker.Character.Name == "DeepList")
        {
            var deepListDoubtfulTactic =
                _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                    x.PlayerId == attacker.GetPlayerId() && x.GameId == game.GameId);

            if (deepListDoubtfulTactic != null)
                if (!deepListDoubtfulTactic.FriendList.Contains(octopus.GetPlayerId()))
                    return 0;
        }
        //end deeplist

        var enemyIds = new List<Guid> { attacker.GetPlayerId() };

        //jew
        var point = await HandleJews(attacker, game);

        if (point == 0)
        {
            var jews = game.PlayersList.FindAll(x => x.Character.Name is "Толя" or "LeCrisp");

            switch (jews.Count)
            {
                case 1:
                    enemyIds = new List<Guid> { jews.FirstOrDefault()!.Status.PlayerId };
                    break;
                case 2:
                    enemyIds.Clear();
                    enemyIds.AddRange(jews.Where(x => x.Status.ScoreSource.Contains("Еврей"))
                        .Select(j => j.Status.PlayerId));
                    break;
            }
        }
        //end jew

        foreach (var enemyId in enemyIds)
        {
            var octopusInkList =
                _gameGlobal.OctopusInkList.Find(x => x.PlayerId == octopus.GetPlayerId() && x.GameId == game.GameId);
            if (octopusInkList == null)
            {
                _gameGlobal.OctopusInkList.Add(new Octopus.InkClass(octopus.GetPlayerId(), game, enemyId));
            }
            else
            {
                var enemyRealScore = octopusInkList.RealScoreList.Find(x => x.PlayerId == enemyId);
                var octopusRealScore = octopusInkList.RealScoreList.Find(x => x.PlayerId == octopus.GetPlayerId());

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