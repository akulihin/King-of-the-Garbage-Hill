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

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class CharacterPassives : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        private readonly GameUpdateMess _gameUpdateMess;
        private readonly HelperFunctions _help;
        private readonly LoginFromConsole _log;
        private readonly SecureRandom _rand;


        public CharacterPassives(SecureRandom rand, HelperFunctions help,
            InGameGlobal gameGlobal, LoginFromConsole log, GameUpdateMess gameUpdateMess)
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
        public async Task HandleDefenseBeforeFight(GamePlayerBridgeClass target,
            GamePlayerBridgeClass me,
            GameClass game)
        {
            var characterName = target.Character.Name;

            switch (characterName)
            {
                case "Братишка":
                    //Ничего не понимает: 
                    var shark = _gameGlobal.SharkBoole.Find(x =>
                        x.PlayerId == target.Status.PlayerId &&
                        game.GameId == x.GameId);


                    if (!shark.FriendList.Contains(me.Status.PlayerId))
                    {
                        shark.FriendList.Add(me.Status.PlayerId);
                        me.Character.AddIntelligence(me.Status, -1,
                            "Ничего не понимает: ");
                    }

                    var sharkDontUndertand = _gameGlobal.SharkDontUnderstand.Find(x =>
                        x.PlayerId == target.Status.PlayerId && game.GameId == x.GameId);

                    sharkDontUndertand.EnemyId = me.Status.PlayerId;
                    sharkDontUndertand.IntelligenceToReturn = me.Character.GetIntelligence();
                    me.Character.SetIntelligence(me.Status, 0, "Ничего не понимает: ", false);

                    //end Ничего не понимает: 
                    break;

                case "Глеб":
                    //Я щас приду:
                    var rand = _rand.Random(1, 8);
                    if (rand == 1)
                    {
                        var acc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.PlayerId == target.Status.PlayerId &&
                            target.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                                return;


                        if (!target.Status.IsSkip)
                        {
                            target.Status.IsSkip = true;
                            _gameGlobal.GlebSkipList.Add(
                                new Gleb.GlebSkipClass(target.Status.PlayerId, game.GameId));
                            game.Phrases.GlebComeBackPhrase.SendLog(target, true);
                        }
                    }

                    //end Я щас приду:
                    break;
                case "LeCrisp":
                    //Гребанные ассассин

                    //Гребанные ассассин + Сомнительная тактика
                    var ok = true;
                    var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                        x.PlayerId == target.Status.PlayerId && game.GameId == x.GameId);

                    if (deep != null)
                        if (!deep.FriendList.Contains(me.Status.PlayerId))
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
                            x.PlayerId == target.Status.PlayerId);
                        tolya.FriendList.Add(me.Status.PlayerId);
                    }
                    //end Раммус мейн

                    break;

                case "HardKitty":
                    //Одиночество
                    var hard = _gameGlobal.HardKittyLoneliness.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == target.Status.PlayerId);
                    if (hard != null)
                        if (!hard.Activated)
                        {
                            target.Status.AddRegularPoints(1, "Одиночество");
                            game.Phrases.HardKittyLonelyPhrase.SendLog(target, true);
                            hard.Activated = true;
                            var hardEnemy = hard.AttackHistory.Find(x => x.EnemyId == me.Status.PlayerId);
                            if (hardEnemy == null)
                            {
                                hard.AttackHistory.Add(new HardKitty.LonelinessSubClass(me.Status.PlayerId));
                                hardEnemy = hard.AttackHistory.Find(x => x.EnemyId == me.Status.PlayerId);
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
                        x.GameId == game.GameId && x.PlayerId == target.Status.PlayerId);

                    if (mitsuki == null)
                    {
                        _gameGlobal.MitsukiGarbageList.Add(new Mitsuki.GarbageClass(target.Status.PlayerId, game.GameId,
                            me.Status.PlayerId));
                    }
                    else
                    {
                        var found = mitsuki.Training.Find(x => x.EnemyId == me.Status.PlayerId);
                        if (found != null)
                            found.Times++;
                        else
                            mitsuki.Training.Add(new Mitsuki.GarbageSubClass(me.Status.PlayerId));
                    }

                    //end Запах мусора
                    break;
            }

            await Task.CompletedTask;
        }


        public async Task HandleDefenseAfterFight(GamePlayerBridgeClass target,
            GamePlayerBridgeClass me, GameClass game)
        {
            var characterName = target.Character.Name;


            switch (characterName)
            {
                case "Братишка":
                    //Ничего не понимает: 
                    var sharkDontUndertand = _gameGlobal.SharkDontUnderstand.Find(x =>
                        x.PlayerId == target.Status.PlayerId && game.GameId == x.GameId);
                    if (sharkDontUndertand.EnemyId == me.Status.PlayerId)
                    {
                        me.Character.SetIntelligence(me.Status, sharkDontUndertand.IntelligenceToReturn,
                            "Ничего не понимает: ", false);
                        sharkDontUndertand.EnemyId = Guid.Empty;
                        sharkDontUndertand.IntelligenceToReturn = 0;
                    }

                    //end Ничего не понимает: 
                    break;
                case "LeCrisp":
                    //Гребанные ассассин
                    if (me.Character.GetStrength() - target.Character.GetStrength() >= 2
                        && !target.Status.IsBlock
                        && !target.Status.IsSkip)
                    {
                        target.Status.IsAbleToWin = true;
                    }
                    else
                    {
                        var leCrip = _gameGlobal.LeCrispAssassins.Find(x =>
                            x.PlayerId == target.Status.PlayerId && game.GameId == x.GameId);
                        leCrip.AdditionalPsycheForNextRound += 1;
                    }
                    //end Гребанные ассассин

                    //Импакт:
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                            x.PlayerId == target.Status.PlayerId && x.GameId == game.GameId);

                        lePuska.IsLost = true;
                    }
                    //end Импакт

                    break;
                case "HardKitty":
                    //Mute passive
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var hardKittyMute = _gameGlobal.HardKittyMute.Find(x =>
                            x.PlayerId == target.Status.PlayerId &&
                            x.GameId == game.GameId);


                        if (!hardKittyMute.UniquePlayers.Contains(me.Status.PlayerId))
                        {
                            hardKittyMute.UniquePlayers.Add(me.Status.PlayerId);
                            me.Status.AddRegularPoints(1, "Mute");
                            game.Phrases.HardKittyMutedPhrase.SendLog(target, false);
                        }
                    }
                    //Mute passive end


                    break;
            }

            await Task.CompletedTask;
        }


        public async Task HandleAttackBeforeFight(GamePlayerBridgeClass me,
            GamePlayerBridgeClass target,
            GameClass game)
        {
            var characterName = me.Character.Name;

            switch (characterName)
            {
                case "Загадочный Спартанец в маске":

                    //Им это не понравится:
                    var spartanMark =
                        _gameGlobal.SpartanMark.Find(x => x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);
                    if (spartanMark != null)
                        if (target.Status.IsBlock && spartanMark.FriendList.Contains(target.Status.PlayerId))
                        {
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
                                               $"\"{me.DiscordUsername}\" побеждает дракона и забирает **1000 голды**!");
                            foreach (var p in game.PlayersList) game.Phrases.SpartanDragonSlayer.SendLog(p, false);
                        }
                    //end DragonSlayer


                    //Первая кровь: 
                    var pant = _gameGlobal.SpartanFirstBlood.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);
                    if (pant.FriendList.Count == 0) pant.FriendList.Add(target.Status.PlayerId);


                    //end Первая кровь: 

                    //Они позорят военное искусство:
                    var Spartan = _gameGlobal.SpartanShame.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);


                    if (target.Character.Name == "mylorik" && !Spartan.FriendList.Contains(target.Status.PlayerId))
                    {
                        Spartan.FriendList.Add(target.Status.PlayerId);
                        me.Character.AddPsyche(me.Status, 1, "ОН уважает военное искусство!: ");
                        target.Character.AddPsyche(target.Status, 1, "ОН уважает военное искусство!: ");
                        game.Phrases.SpartanShameMylorik.SendLog(me, false);
                    }

                    if (!Spartan.FriendList.Contains(target.Status.PlayerId))
                    {
                        Spartan.FriendList.Add(target.Status.PlayerId);
                        target.Character.AddStrength(target.Status, -1, "Они позорят военное искусство: ");
                        target.Character.AddSpeed(target.Status, -1, "Они позорят военное искусство: ");
                    }


                    //end Они позорят военное искусство:
                    break;


                case "Глеб":
                    // Я за чаем:
                    var geblTea =
                        _gameGlobal.GlebTea.Find(x => x.PlayerId == me.Status.PlayerId && game.GameId == x.GameId);

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
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);
                    var siriAttack = _gameGlobal.SirinoksFriendsAttack.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);

                    if (siri != null && siriAttack != null)
                        if (siri.FriendList.Contains(target.Status.PlayerId) && target.Status.IsBlock)
                        {
                            target.Status.IsBlock = false;
                            siriAttack.EnemyId = target.Status.PlayerId;
                            siriAttack.IsBlock = true;
                        }

                    if (siri != null && siriAttack != null)
                        if (siri.FriendList.Contains(target.Status.PlayerId) && target.Status.IsSkip)
                        {
                            target.Status.IsSkip = false;
                            siriAttack.EnemyId = target.Status.PlayerId;
                            siriAttack.IsSkip = true;
                        }

                    if (!siri.FriendList.Contains(target.Status.PlayerId))
                    {
                        siri.FriendList.Add(target.Status.PlayerId);
                        me.Status.AddRegularPoints(1, "Заводить друзей");
                        game.Phrases.SirinoksFriendsPhrase.SendLog(me, true);
                    }


                    //Заводить друзей end
                    break;

                case "AWDKA":

                    //Научите играть
                    var awdka = _gameGlobal.AwdkaTeachToPlay.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);
                    var awdkaHistory = _gameGlobal.AwdkaTeachToPlayHistory.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);

                    var player2Stats = new List<Sirinoks.TrainingSubClass>
                    {
                        new(1, target.Character.GetIntelligence()),
                        new(2, target.Character.GetStrength()),
                        new(3, target.Character.GetSpeed()),
                        new(4, target.Character.GetPsyche())
                    };
                    var sup = player2Stats.OrderByDescending(x => x.StatNumber).ToList()[0];

                    if (awdka == null)
                        _gameGlobal.AwdkaTeachToPlay.Add(new Sirinoks.TrainingClass(me.Status.PlayerId, game.GameId,
                            sup.StatIndex, sup.StatNumber, Guid.Empty));
                    else
                        awdka.Training.Add(new Sirinoks.TrainingSubClass(sup.StatIndex, sup.StatNumber));


                    var enemy = awdkaHistory.History.Find(x => x.EnemyPlayerId == target.Status.PlayerId);
                    if (enemy == null)
                    {
                        awdkaHistory.History.Add(new Awdka.TeachToPlayHistoryListClass(target.Status.PlayerId,
                            $"{sup.StatIndex}", sup.StatNumber));
                    }
                    else
                    {
                        enemy.Text = $"{sup.StatIndex}";
                        enemy.Stat = sup.StatNumber;
                    }


                    //end Научите играть

                    break;
                case "Вампур":

                    //Падальщик
                    if (target.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                    {
                        var scavenger = _gameGlobal.VampyrScavengerList.Find(x =>
                            x.PlayerId == me.Status.PlayerId && x.GameId == game.GameId);
                        scavenger.EnemyId = target.Status.PlayerId;
                        scavenger.EnemyJustice = target.Character.Justice.GetJusticeNow();
                        target.Character.Justice.AddJusticeNow(target.Status, -1);
                    }

                    //end Падальщик
                    break;
                case "mylorik":
                    // Cпарта
                    var mylorikSpartan =
                        _gameGlobal.MylorikSpartan.Find(x => x.GameId == me.GameId && x.PlayerId == me.Status.PlayerId);
                    var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.Status.PlayerId);
                    if (mylorikEnemy == null)
                    {
                        mylorikSpartan.Enemies.Add(new Mylorik.MylorikSpartanSubClass(target.Status.PlayerId));
                        mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.Status.PlayerId);
                    }

                    if (me.Status.WhoToAttackThisTurn == target.Status.PlayerId)
                        //set FightMultiplier
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
                                break;
                            default:
                                me.Character.SetSkillFightMultiplier();
                                break;
                        }

                    //end Cпарта
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleAttackAfterFight(GamePlayerBridgeClass me,
            GamePlayerBridgeClass target, GameClass game)
        {
            var characterName = me.Character.Name;


            switch (characterName)
            {
                case "Загадочный Спартанец в маске":

                    //Им это не понравится:
                    var spartanMark =
                        _gameGlobal.SpartanMark.Find(x => x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);
                    if (spartanMark != null)
                        if (target.Status.IsBlock && spartanMark.FriendList.Contains(target.Status.PlayerId))
                        {
                            target.Status.IsAbleToWin = true;
                            target.Status.IsBlock = true;
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
                            x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId)
                        .EnemyPlayerId = target.Status.PlayerId;
                    game.Phrases.SecondСommandmentBan.SendLog(me, false);
                    break;
                case "Вампур":
                    //Падальщик
                    if (target.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                    {
                        var scavenger = _gameGlobal.VampyrScavengerList.Find(x =>
                            x.PlayerId == me.Status.PlayerId && x.GameId == game.GameId);

                        if (scavenger.EnemyId == target.Status.PlayerId)
                        {
                            target.Character.Justice.SetJusticeNow(target.Status, scavenger.EnemyJustice, "Падальщик: ",
                                false);
                            scavenger.EnemyId = Guid.Empty;
                            scavenger.EnemyJustice = 0;
                        }
                    }
                    //end Падальщик

                    //Вампуризм
                    if (me.Status.IsWonThisCalculation == target.Status.PlayerId)
                        me.Character.Justice.AddJusticeForNextRound(target.Character.Justice.GetJusticeNow());
                    //Вампуризм

                    break;


                case "Осьминожка":
                    //Неуязвимость
                    if (me.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var octo = _gameGlobal.OctopusInvulnerabilityList.Find(x =>
                            x.GameId == me.GameId &&
                            x.PlayerId == me.Status.PlayerId);

                        if (octo == null)
                            _gameGlobal.OctopusInvulnerabilityList.Add(
                                new Octopus.InvulnerabilityClass(me.Status.PlayerId, game.GameId));
                        else
                            octo.Count++;
                    }
                    //end Неуязвимость

                    break;

                case "Sirinoks":

                    //Заводить друзей
                    var siriAttack = _gameGlobal.SirinoksFriendsAttack.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);

                    if (siriAttack != null)
                        if (siriAttack.EnemyId == target.Status.PlayerId)
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
                        x.PlayerId == me.Status.PlayerId);

                    if (!darscsi.TouchedPlayers.Contains(target.Status.PlayerId))
                        darscsi.TouchedPlayers.Add(target.Status.PlayerId);

                    if (darscsi.TouchedPlayers.Count == game.PlayersList.Count - 1 && darscsi.Triggered == false)
                    {
                        me.Status.AddBonusPoints(me.Status.GetScore() * 3, "Повезло: ");

                        me.Character.AddPsyche(me.Status, 3, "Повезло: ");
                        darscsi.Triggered = true;
                        game.Phrases.DarksciLucky.SendLog(me, true);
                    }
                    //end Повезло

                    break;
                case "mylorik":
                    // Cпарта
                    if (me.Status.WhoToAttackThisTurn == target.Status.PlayerId)
                    {
                        var mylorikSpartan = _gameGlobal.MylorikSpartan.Find(x =>
                            x.GameId == me.GameId && x.PlayerId == me.Status.PlayerId);
                        var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.Status.PlayerId);
                        if (mylorikEnemy == null)
                        {
                            mylorikSpartan.Enemies.Add(new Mylorik.MylorikSpartanSubClass(target.Status.PlayerId));
                            mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.Status.PlayerId);
                        }

                        if (me.Status.IsLostThisCalculation == me.Status.WhoToAttackThisTurn) mylorikEnemy.LostTimes++;

                        if (me.Status.IsWonThisCalculation == me.Status.WhoToAttackThisTurn) mylorikEnemy.LostTimes = 0;
                    }

                    //reset FightMultiplier
                    me.Character.SetSkillFightMultiplier();
                    //end Cпарта
                    break;
            }

            await Task.CompletedTask;
        }


        public void HandleCharacterAfterFight(GamePlayerBridgeClass player, GameClass game)
        {
            //TODO: test it
            //Подсчет
            if (player.Status.IsLostThisCalculation != Guid.Empty && player.Character.Name != "Толя" &&
                game.PlayersList.Any(x => x.Character.Name == "Толя"))
            {
                var tolyaAcc = game.PlayersList.Find(x => x.Character.Name == "Толя");

                var tolyaCount = _gameGlobal.TolyaCount.Find(x =>
                    x.PlayerId == tolyaAcc.Status.PlayerId && x.GameId == game.GameId);


                if (tolyaCount.TargetList.Any(x =>
                    x.RoundNumber == game.RoundNo - 1 && x.Target == player.Status.PlayerId))
                {
                    tolyaAcc.Status.AddRegularPoints(2, "Подсчет");
                    tolyaAcc.Character.Justice.AddJusticeForNextRound(2);
                    game.Phrases.TolyaCountPhrase.SendLog(tolyaAcc, false);
                }
            }
            //Подсчет end


            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    //Сомнительная тактика
                    var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                        x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

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
                                game.Phrases.DeepListDoubtfulTacticPhrase.SendLog(player, false);
                            }
                    //end Сомнительная тактика

                    // Стёб
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var target =
                            game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation);
                        //Стёб
                        var currentDeepList = _gameGlobal.DeepListMockeryList.Find(x =>
                            x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);

                        if (currentDeepList != null)
                        {
                            var currentDeepList2 =
                                currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == target.Status.PlayerId);

                            if (currentDeepList2 != null)
                            {
                                currentDeepList2.Times++;

                                if (currentDeepList2.Times == 2 && !currentDeepList2.Triggered)
                                {
                                    currentDeepList2.Triggered = true;

                                    if (target.Character.Name != "LeCrisp")
                                    {
                                        target.Character.AddPsyche(target.Status, -1, "Стёб: ");
                                        target.MinusPsycheLog(game);
                                    }


                                    player.Status.AddRegularPoints(1, "Стёб");
                                    game.Phrases.DeepListPokePhrase.SendLog(player, true);
                                    if (target.Character.GetPsyche() < 4)
                                        if (target.Character.Justice.GetJusticeNow() > 0)
                                            if (target.Character.Name != "LeCrisp")
                                                target.Character.Justice.AddJusticeForNextRound(-1);
                                }
                            }
                            else
                            {
                                currentDeepList.WhoWonTimes.Add(new DeepList.MockerySub(target.Status.PlayerId, 1));
                            }
                        }
                        else
                        {
                            var toAdd = new DeepList.Mockery(
                                new List<DeepList.MockerySub> {new(target.Status.PlayerId, 1)}, game.GameId,
                                player.Status.PlayerId);
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
                        x.GameId == player.GameId && x.PlayerId == player.Status.PlayerId);

                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        //check if very first lost
                        if (mylorik == null)
                        {
                            _gameGlobal.MylorikRevenge.Add(new Mylorik.MylorikRevengeClass(player.Status.PlayerId,
                                player.GameId, player.Status.IsLostThisCalculation, game.RoundNo));
                            game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                        }
                        else
                        {
                            if (mylorik.EnemyListPlayerIds.All(x =>
                                x.EnemyPlayerId != player.Status.IsLostThisCalculation))
                            {
                                mylorik.EnemyListPlayerIds.Add(
                                    new Mylorik.MylorikRevengeClassSub(player.Status.IsLostThisCalculation,
                                        game.RoundNo));
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
                            player.Character.AddPsyche(player.Status, 1, "Месть: ");
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
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        if (rand == 1)
                        {
                            boole.Times = 0;
                            player.Character.AddPsyche(player.Status, -1, "Испанец: ");
                            //player.Character.AddExtraSkill(player.Status,  "Испанец: ", 5);
                            player.MinusPsycheLog(game);
                            game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                        }
                        else
                        {
                            boole.Times++;

                            if (boole.Times == 2)
                            {
                                boole.Times = 0;
                                player.Character.AddPsyche(player.Status, -1, "Испанец: ");
                                player.Character.AddExtraSkill(player.Status, "Испанец: ", 5);
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
                        x.PlayerId == player.Status.PlayerId && x.GameId == player.GameId);
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
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        
                        player.Character.AddMoral(player.Status, lePuska.ImpactTimes+1, "Импакт: ");
                    }

                    //Импакт
                    break;
                case "Толя":
                    //Раммус мейн
                    if (player.Status.IsBlock && player.Status.IsWonThisCalculation != Guid.Empty)
                        game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation).Status
                            .IsAbleToWin = true;
                    //end Раммус мейн
                    break;
                case "HardKitty":
                    //Доебаться
                    var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                        x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);

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
                                player.Status.AddRegularPoints(found.Series, "Доебаться");
                                game.Phrases.HardKittyDoebatsyaPhrase.SendLog(player, false);
                                found.Series = 0;
                            }
                    }

                    //end Доебаться
                    break;
                case "Sirinoks":
                    //Обучение
                    var siri = _gameGlobal.SirinoksTraining.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);


                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var playerSheLostLastTime =
                            game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsLostThisCalculation);
                        var intel = new List<Sirinoks.StatsClass>
                        {
                            new(1, playerSheLostLastTime.Character.GetIntelligence()),
                            new(2, playerSheLostLastTime.Character.GetStrength()),
                            new(3, playerSheLostLastTime.Character.GetSpeed()),
                            new(4, playerSheLostLastTime.Character.GetPsyche())
                        };
                        var best = intel.OrderByDescending(x => x.Number).ToList()[0];


                        if (siri == null)
                        {
                            _gameGlobal.SirinoksTraining.Add(new Sirinoks.TrainingClass(player.Status.PlayerId,
                                game.GameId, best.Index, best.Number, playerSheLostLastTime.Status.PlayerId));
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
                            game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation);

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

                        switch (player.Character.GetCurrentSkillTarget())
                        {
                            case "Интеллект":
                                if (playerIamAttacking.Character.GetClassStatInt() == 0)
                                    player.Character.AddExtraSkill(player.Status, "Много выебывается: ",
                                        howMuchToAdd * 2);
                                break;
                            case "Сила":
                                if (playerIamAttacking.Character.GetClassStatInt() == 1)
                                    player.Character.AddExtraSkill(player.Status, "Много выебывается: ",
                                        howMuchToAdd * 2);
                                break;
                            case "Скорость":
                                if (playerIamAttacking.Character.GetClassStatInt() == 2)
                                    player.Character.AddExtraSkill(player.Status, "Много выебывается: ",
                                        howMuchToAdd * 2);
                                break;
                        }
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
                            x.PlayerId == player.Status.PlayerId);

                        var enemy = awdka.EnemyList.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);

                        if (enemy == null)
                            awdka.EnemyList.Add(new Awdka.TrollingSubClass(player.Status.IsWonThisCalculation,
                                game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation)
                                    .Status
                                    .GetScore()));
                        else
                            enemy.Score = game.PlayersList
                                .Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation)
                                .Status.GetScore();
                    }
                    //end Произошел троллинг:

                    //Я пытаюсь!
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var awdka = _gameGlobal.AwdkaTryingList.Find(x =>
                            x.GameId == player.GameId && x.PlayerId == player.Status.PlayerId);


                        var enemy = awdka.TryingList.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);
                        if (enemy == null)
                            awdka.TryingList.Add(new Awdka.TryingSubClass(player.Status.IsLostThisCalculation));
                        else
                            enemy.Times++;
                    }

                    //Я пытаюсь!
                    break;
                case "Осьминожка":

                    break;
                case "Darksci":
                    //Не повезло
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        //LOL GOD, EXAMPLE:
                        /*
                        if (game.PlayersList.All(x => x.Character.Name != "Бог ЛоЛа") || _gameGlobal.LolGodUdyrList.Any(
                                x =>
                                    x.GameId == game.GameId && x.EnemyDiscordId == player.Status.PlayerId))
                        {
                            player.Character.AddPsyche(player.Status, -1);
                            player.MinusPsycheLog(game);
                            await game.Phrases.DarksciNotLucky.SendLog(player);
                        }
                        else
                            await game.Phrases.ThirdСommandment.SendLog(player);*/
                        player.Character.AddPsyche(player.Status, -1, "Не повезло: ");
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
                            x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);


                        if (tigr == null)
                        {
                            _gameGlobal.TigrThreeZeroList.Add(new Tigr.ThreeZeroClass(player.Status.PlayerId,
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
                                    player.Status.AddRegularPoints(2, "3-0 обоссан");
                                    player.Character.AddExtraSkill(player.Status, "3-0 обоссан: ", 20);


                                    var enemyAcc = game.PlayersList.Find(x =>
                                        x.Status.PlayerId == player.Status.IsWonThisCalculation);

                                    if (enemyAcc != null)
                                    {
                                        enemyAcc.Character.AddIntelligence(enemyAcc.Status, -1, "3-0 обоссан: ");

                                        enemyAcc.Character.AddPsyche(enemyAcc.Status, -1, "3-0 обоссан: ");
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
                            x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                        var enemy = tigr?.FriendList.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);

                        if (enemy != null && enemy.IsUnique) enemy.WinsSeries = 0;
                    }

                    //end 3-0 обоссан: 

                    /*//Тигр топ, а ты холоп: 
                    if (player.Status.IsLostThisCalculation != Guid.Empty && player.Status.PlaceAtLeaderBoard == 1)
                    {
                        player.Character.Justice.AddJusticeForNextRound(-1);
                    }
                    //end //Тигр топ, а ты холоп*/
                    break;
                case "Братишка":
                    //Челюсти: 
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var shark = _gameGlobal.SharkJawsWin.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);


                        if (!shark.FriendList.Contains(player.Status.IsWonThisCalculation))
                        {
                            shark.FriendList.Add(player.Status.IsWonThisCalculation);
                            player.Character.AddSpeed(player.Status, 1, "Челюсти: ");
                        }
                    }

                    //end Челюсти: 
                    break;
                case "Загадочный Спартанец в маске":
                    //Первая кровь: 
                    var Spartan = _gameGlobal.SpartanFirstBlood.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                    if (Spartan.FriendList.Count == 1)
                    {
                        if (Spartan.FriendList.Contains(player.Status.IsWonThisCalculation))
                        {
                            player.Character.AddSpeed(player.Status, 1, "Первая кровь: ");
                            game.AddGlobalLogs("Они познают войну!\n");
                        }
                        else if (Spartan.FriendList.Contains(player.Status.IsLostThisCalculation))
                        {
                            var ene = game.PlayersList.Find(x =>
                                x.Status.PlayerId == player.Status.IsLostThisCalculation);
                            ene.Character.AddSpeed(ene.Status, 1, "Первая кровь: ");
                        }

                        Spartan.FriendList.Add(Guid.Empty);
                    }
                    //end Первая кровь: 

                    //Это привилегия - умереть от моей руки
                    if (player.Status.IsWonThisCalculation != Guid.Empty && game.RoundNo > 4)
                    {
                        game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation).Character
                            .Justice.AddJusticeForNextRound();
                        player.Character.AddIntelligence(player.Status, -1, "Это привилегия: ");
                    }
                    //end Это привилегия - умереть от моей руки

                    //Им это не понравится: 
                    Spartan = _gameGlobal.SpartanMark.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                    if (Spartan.FriendList.Contains(player.Status.IsWonThisCalculation))
                    {
                        player.Status.AddRegularPoints(1, "Им это не понравится");
                        player.Status.AddBonusPoints(1, "Им это не понравится: ");
                    }

                    //end Им это не понравится: 
                    break;
                case "Вампур":
                    //Гематофагия

                    var vampyr = _gameGlobal.VampyrHematophagiaList.Find(x =>
                        x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

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
                                            player.Character.AddIntelligence(player.Status, 2, "Гематофагия: ");
                                            found = true;
                                        }

                                        break;
                                    case 2:
                                        if (player.Character.GetStrength() < 10)
                                        {
                                            player.Character.AddStrength(player.Status, 2, "Гематофагия: ");
                                            found = true;
                                        }

                                        break;
                                    case 3:
                                        if (player.Character.GetSpeed() < 10)
                                        {
                                            player.Character.AddSpeed(player.Status, 2, "Гематофагия: ");
                                            found = true;
                                        }

                                        break;
                                    case 4:
                                        if (player.Character.GetPsyche() < 10)
                                        {
                                            player.Character.AddPsyche(player.Status, 2, "Гематофагия: ");
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
                                    player.Character.AddIntelligence(player.Status, -2, "СОсиновый кол: ");
                                    player.Status.AddRegularPoints(-1, "СОсиновый кол: ");
                                    break;
                                case 2:
                                    player.Character.AddStrength(player.Status, -2, "СОсиновый кол: ");
                                    player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                    break;
                                case 3:
                                    player.Character.AddSpeed(player.Status, -2, "СОсиновый кол: ");
                                    player.Status.AddRegularPoints(-1, "СОсиновый кол");
                                    break;
                                case 4:
                                    player.Character.AddPsyche(player.Status, -2, "СОсиновый кол: ");
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
                                        player.Character.AddIntelligence(player.Status, -2, "Гематофагия: ");
                                        break;
                                    case 2:
                                        player.Character.AddStrength(player.Status, -2, "Гематофагия: ");
                                        break;
                                    case 3:
                                        player.Character.AddSpeed(player.Status, -2, "Гематофагия: ");
                                        break;
                                    case 4:
                                        player.Character.AddPsyche(player.Status, -2, "Гематофагия: ");
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
        public async Task HandleCharacterWithKnownEnemyBeforeFight(GamePlayerBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "Толя":

                    //Подсчет
                    var tolya = _gameGlobal.TolyaCount.Find(x =>
                        x.GameId == player.GameId &&
                        x.PlayerId == player.Status.PlayerId);

                    if (tolya.IsReadyToUse && player.Status.WhoToAttackThisTurn != Guid.Empty)
                    {
                        tolya.TargetList.Add(new Tolya.TolyaCountSubClass(player.Status.WhoToAttackThisTurn,
                            game.RoundNo));
                        tolya.IsReadyToUse = false;
                        tolya.Cooldown = 4;
                    }

                    //Подсчет end
                    break;

                case "DeepList":
                    //Сомнительная тактика
                    var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                        x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);


                    if (deep != null)
                        if (player.Status.IsFighting != Guid.Empty)
                        {
                            var target = game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsFighting);
                            if (!deep.FriendList.Contains(player.Status.IsFighting) && !target.Status.IsSkip &&
                                !target.Status.IsBlock) player.Status.IsAbleToWin = false;
                        }


                    //end Сомнительная тактика
                    break;
            }


            await Task.CompletedTask;
        }
        //

        //after all fight

        public async Task HandleEndOfRound(GameClass game)
        {
            foreach (var player in game.PlayersList)
            {
                if (player == null) _log.Critical("HandleEndOfRound - octopusPlayer == null");

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
                                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                                if (!tigr.FriendList.Contains(t.Status.PlayerId))
                                {
                                    tigr.FriendList.Add(t.Status.PlayerId);
                                    // me.Status.AddRegularPoints();
                                    player.Status.AddBonusPoints(3, "Лучше с двумя, чем с адекватными: ");
                                    game.Phrases.TigrTwoBetter.SendLog(player, false);
                                }
                            }
                        }

                        //end Лучше с двумя, чем с адекватными:
                        break;

                    case "DeepList":

                        //Безумие
                        var madd = _gameGlobal.DeepListMadnessList.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId &&
                            x.RoundItTriggered == game.RoundNo);

                        if (madd != null)
                        {
                            var regularStats = madd.MadnessList.Find(x => x.Index == 1);
                            var madStats = madd.MadnessList.Find(x => x.Index == 2);


                            var intel = player.Character.GetIntelligence() - madStats.Intel;
                            var str = player.Character.GetStrength() - madStats.Str;
                            var speed = player.Character.GetSpeed() - madStats.Speed;
                            var psy = player.Character.GetPsyche() - madStats.Psyche;


                            player.Character.SetIntelligence(player.Status, regularStats.Intel + intel, "Безумие: ",
                                false);
                            player.Character.SetStrength(player.Status, regularStats.Str + str, "Безумие: ", false);
                            player.Character.SetSpeed(player.Status, regularStats.Speed + speed, "Безумие: ", false);
                            player.Character.SetPsyche(player.Status, regularStats.Psyche + psy, "Безумие: ", false);
                            player.Character.SetSkillMultiplier();
                            _gameGlobal.DeepListMadnessList.Remove(madd);
                        }

                        // end Безумие 
                        break;
                    case "Глеб":
                        //Претендент русского сервера
                        var glebChall = _gameGlobal.GlebChallengerList.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId &&
                            x.RoundItTriggered == game.RoundNo);

                        if (glebChall != null)
                        {
                            //x3 point:
                            player.Status.SetScoresToGiveAtEndOfRound(
                                (int)player.Status.GetScoresToGiveAtEndOfRound() * 3, "Претендент русского сервера");
                            //end x3 point:

                            var regularStats = glebChall.MadnessList.Find(x => x.Index == 1);
                            var madStats = glebChall.MadnessList.Find(x => x.Index == 2);


                            var intel = player.Character.GetIntelligence() - madStats.Intel;
                            var str = player.Character.GetStrength() - madStats.Str;
                            var speed = player.Character.GetSpeed() - madStats.Speed;
                            var psy = player.Character.GetPsyche() - madStats.Psyche;


                            player.Character.SetIntelligence(player.Status, regularStats.Intel + intel,
                                "Претендент русского сервера: ", false);
                            player.Character.SetStrength(player.Status, regularStats.Str + str,
                                "Претендент русского сервера: ", false);
                            player.Character.SetSpeed(player.Status, regularStats.Speed + speed,
                                "Претендент русского сервера: ", false);
                            player.Character.SetPsyche(player.Status, regularStats.Psyche + psy,
                                "Претендент русского сервера: ", false);
                            player.Character.AddExtraSkill(player.Status, "Претендент русского сервера: ", -100, false);
                            _gameGlobal.GlebChallengerList.Remove(glebChall);
                        }
                        //end Претендент русского сервера

                        break;
                    case "LeCrisp":


                        //Гребанные ассассин
                        var leCrip = _gameGlobal.LeCrispAssassins.Find(x =>
                            x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);

                        if (leCrip.AdditionalPsycheCurrent > 0)
                            player.Character.AddPsyche(player.Status, leCrip.AdditionalPsycheCurrent * -1,
                                "Гребанные ассассины: ", false);
                        if (leCrip.AdditionalPsycheForNextRound > 0)
                            player.Character.AddPsyche(player.Status, leCrip.AdditionalPsycheForNextRound,
                                "Гребанные ассассины: ");

                        leCrip.AdditionalPsycheCurrent = leCrip.AdditionalPsycheForNextRound;
                        leCrip.AdditionalPsycheForNextRound = 0;

                        //end Гребанные ассассин

                        //Импакт
                        var leImpact = _gameGlobal.LeCrispImpact.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        if (leImpact.IsLost)
                        {
                            leImpact.ImpactTimes = 0;
                        }
                        else
                        {
                            leImpact.ImpactTimes += 1;
                            player.Status.AddBonusPoints(1, "Импакт: ");
                            player.Character.Justice.AddJusticeForNextRound();
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
                                    x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);
                                if (tolyaTalked.PlayerHeTalkedAbout.Count < 2)
                                {
                                    var randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];

                                    while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.Status.PlayerId))
                                        randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];


                                    if (randomPlayer.Status.PlayerId == player.Status.PlayerId)
                                        while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.Status.PlayerId))
                                            randomPlayer =
                                                game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];
                                    if (randomPlayer.Status.PlayerId == player.Status.PlayerId)
                                        while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.Status.PlayerId))
                                            randomPlayer =
                                                game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];


                                    tolyaTalked.PlayerHeTalkedAbout.Add(randomPlayer.Status.PlayerId);
                                    game.AddGlobalLogs(
                                        $"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {randomPlayer.Character.Name}");
                                }
                            }
                        }
                        //end Великий Комментатор

                        //Раммус мейн
                        var tolya = _gameGlobal.TolyaRammusTimes.Find(x =>
                            x.GameId == player.GameId &&
                            x.PlayerId == player.Status.PlayerId);
                        if (tolya != null)
                        {
                            switch (tolya.FriendList.Count)
                            {
                                case 1:
                                    game.Phrases.TolyaRammusPhrase.SendLog(player, false);
                                    player.Character.Justice.AddJusticeForNextRound();
                                    break;
                                case 2:
                                    game.Phrases.TolyaRammus2Phrase.SendLog(player, false);
                                    player.Character.Justice.AddJusticeForNextRound(2);
                                    break;
                                case 3:
                                    game.Phrases.TolyaRammus3Phrase.SendLog(player, false);
                                    player.Character.Justice.AddJusticeForNextRound(3);
                                    break;
                                case 4:
                                    game.Phrases.TolyaRammus4Phrase.SendLog(player, false);
                                    player.Character.Justice.AddJusticeForNextRound(4);
                                    break;
                                case 5:
                                    game.Phrases.TolyaRammus5Phrase.SendLog(player, false);
                                    player.Character.Justice.AddJusticeForNextRound(5);
                                    break;
                            }

                            tolya.FriendList.Clear();
                        }

                        //end Раммус мейн
                        break;

                    case "Осьминожка":

                        //привет со дна
                        if (game.SkipPlayersThisRound > 0)
                            player.Status.AddBonusPoints(game.SkipPlayersThisRound, "привет со дна");
                        //end привет со дна


                        break;

                    case "Sirinoks":
                        //Обучение

                        var siri = _gameGlobal.SirinoksTraining.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                        if (siri != null && siri.Training.Count >= 1)
                        {
                            var stats = siri.Training.OrderByDescending(x => x.StatNumber).ToList()[0];

                            switch (stats.StatIndex)
                            {
                                case 1:
                                    player.Character.AddIntelligence(player.Status, 1, "Обучение: ");
                                    if (player.Character.GetIntelligence() == stats.StatNumber)
                                        if (!siri.TriggeredBonusFromStat.Contains(stats.StatIndex))
                                        {
                                            player.Character.AddMoral(player.Status, 3, "Обучение: ");
                                            siri.TriggeredBonusFromStat.Add(stats.StatIndex);
                                        }

                                    break;
                                case 2:
                                    player.Character.AddStrength(player.Status, 1, "Обучение: ");
                                    if (player.Character.GetStrength() == stats.StatNumber)
                                        if (!siri.TriggeredBonusFromStat.Contains(stats.StatIndex))
                                        {
                                            player.Character.AddMoral(player.Status, 3, "Обучение: ");
                                            siri.TriggeredBonusFromStat.Add(stats.StatIndex);
                                        }

                                    break;
                                case 3:
                                    player.Character.AddSpeed(player.Status, 1, "Обучение: ");
                                    if (player.Character.GetSpeed() == stats.StatNumber)
                                        if (!siri.TriggeredBonusFromStat.Contains(stats.StatIndex))
                                        {
                                            player.Character.AddMoral(player.Status, 3, "Обучение: ");
                                            siri.TriggeredBonusFromStat.Add(stats.StatIndex);
                                        }

                                    break;
                                case 4:
                                    player.Character.AddPsyche(player.Status, 1, "Обучение: ");
                                    if (player.Character.GetPsyche() == stats.StatNumber)
                                        if (!siri.TriggeredBonusFromStat.Contains(stats.StatIndex))
                                        {
                                            player.Character.AddMoral(player.Status, 3, "Обучение: ");
                                            siri.TriggeredBonusFromStat.Add(stats.StatIndex);
                                        }

                                    break;
                            }
                        }

                        //end Обучение
                        break;

                    case "HardKitty":
                        //Одиночество
                        var hard = _gameGlobal.HardKittyLoneliness.Find(x => x.GameId == player.GameId &&
                                                                             x.PlayerId == player.Status.PlayerId);
                        if (hard != null) hard.Activated = false;
                        //Одиночество


                        //Доебаться
                        var hardKittyDoebatsya = _gameGlobal.HardKittyDoebatsya.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        foreach (var target in game.PlayersList)
                            if (target.Status.WhoToAttackThisTurn == player.Status.PlayerId)
                            {
                                var found = hardKittyDoebatsya.LostSeries.Find(x =>
                                    x.EnemyPlayerId == target.Status.PlayerId);
                                if (found != null)
                                    if (found.Series > 0)
                                    {
                                        found.Series = 0;
                                        game.Phrases.HardKittyDoebatsyaAnswerPhrase.SendLog(player, false);
                                    }
                            }

                        //end Доебаться
                        break;


                    case "Загадочный Спартанец в маске":

                        //Им это не понравится
                        if (game.RoundNo == 2 || game.RoundNo == 4 || game.RoundNo == 6 || game.RoundNo == 8)
                        {
                            var Spartan = _gameGlobal.SpartanMark.Find(x =>
                                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);
                            Spartan.FriendList.Clear();

                            Guid enemy1;
                            Guid enemy2;

                            do
                            {
                                var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                                enemy1 = game.PlayersList[randIndex].Status.PlayerId;
                                if (game.PlayersList[randIndex].Character.Name is "Глеб" or "mylorik" or
                                    "Загадочный Спартанец в маске")
                                    enemy1 = player.Status.PlayerId;
                                if (game.PlayersList[randIndex].Character.Name is "Mit*suki*" && game.RoundNo < 4)
                                    enemy1 = player.Status.PlayerId;
                                if (game.PlayersList[randIndex].Character.Name is "Вампур" && game.RoundNo >= 4)
                                    enemy1 = player.Status.PlayerId;
                            } while (enemy1 == player.Status.PlayerId);

                            do
                            {
                                var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                                enemy2 = game.PlayersList[randIndex].Status.PlayerId;
                                if (game.PlayersList[randIndex].Character.Name is "Глеб" or "mylorik" or
                                    "Загадочный Спартанец в маске")
                                    enemy2 = player.Status.PlayerId;
                                if (game.PlayersList[randIndex].Character.Name is "Mit*suki*" && game.RoundNo < 4)
                                    enemy2 = player.Status.PlayerId;
                                if (game.PlayersList[randIndex].Character.Name is "Вампур" && game.RoundNo >= 4)
                                    enemy2 = player.Status.PlayerId;
                                if (enemy2 == enemy1)
                                    enemy2 = player.Status.PlayerId;
                            } while (enemy2 == player.Status.PlayerId);


                            Spartan.FriendList.Add(enemy2);
                            Spartan.FriendList.Add(enemy1);
                        }
                        //end Им это не понравится

                        break;
                    case "Mit*suki*":

                        //Дерзкая школота:
                        if (!player.Status.IsSkip)
                        {
                            player.Character.AddExtraSkill(player.Status, "Дерзкая школота: ", -20);

                            var randStat1 = _rand.Random(1, 4);
                            var randStat2 = _rand.Random(1, 4);
                            switch (randStat1)
                            {
                                case 1:
                                    player.Character.AddIntelligence(player.Status, -1, "Дерзкая школота: ");
                                    break;
                                case 2:
                                    player.Character.AddStrength(player.Status, -1, "Дерзкая школота: ");
                                    break;
                                case 3:
                                    player.Character.AddSpeed(player.Status, -1, "Дерзкая школота: ");
                                    break;
                                case 4:
                                    player.Character.AddPsyche(player.Status, -1, "Дерзкая школота: ");
                                    break;
                            }

                            switch (randStat2)
                            {
                                case 1:
                                    player.Character.AddIntelligence(player.Status, -1, "Дерзкая школота: ");
                                    break;
                                case 2:
                                    player.Character.AddStrength(player.Status, -1, "Дерзкая школота: ");
                                    break;
                                case 3:
                                    player.Character.AddSpeed(player.Status, -1, "Дерзкая школота: ");
                                    break;
                                case 4:
                                    player.Character.AddPsyche(player.Status, -1, "Дерзкая школота: ");
                                    break;
                            }
                        }


                        //end  Дерзкая школота:
                        if (game.RoundNo > 1)
                        {
                            var noAttack = true;

                            foreach (var target in game.PlayersList)
                            {
                                if (target.Status.PlayerId == player.Status.PlayerId) continue;
                                if (target.Status.WhoToAttackThisTurn == player.Status.PlayerId)
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
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);
                        if (vampyr.Hematophagia.Count > 0)
                            if (game.RoundNo == 3 || game.RoundNo == 6 || game.RoundNo == 9)
                                player.Character.AddMoral(player.Status, vampyr.Hematophagia.Count, "Вампуризм: ");
                        //end Вампуризм
                        break;
                }
            }

            await Task.CompletedTask;
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
                                    var pl = game.PlayersList.Find(x => x.Status.PlayerId == t.PlayerId);
                                    pl?.Status.AddBonusPoints(t.RealScore, "🐙: ");
                                }

                            if (octopusInv != null)
                            {
                                var octoPlayer =
                                    game.PlayersList.Find(x => x.Status.PlayerId == octopusInv.PlayerId);
                                octoPlayer.Status.AddBonusPoints(octopusInv.Count, "🐙: ");
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
                            player.Character.SetStrength(player.Status, 0, "Они позорят военное искусство: ");
                        //end Они позорят военное искусство:
                        break;

                    case "mylorik":
                        //Буль
                        var random = _rand.Random(1, 2 + player.Character.GetPsyche() * 3);

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
                        //end Буль

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
                            player.Character.SetPsyche(player.Status, 0, "Стримснайпят и банят и банят и банят: ");
                            player.Character.SetIntelligence(player.Status, 0,
                                "Стримснайпят и банят и банят и банят: ");
                            player.Character.SetStrength(player.Status, 10, "Стримснайпят и банят и банят и банят: ");
                            game.AddGlobalLogs(
                                $"{player.DiscordUsername}: ЕБАННЫЕ БАНЫ НА 10 ЛЕТ");
                            continue;
                        }
                        //end Стримснайпят и банят и банят и банят:

                        //Тигр топ, а ты холоп:

                        var tigr = _gameGlobal.TigrTopWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId &&
                            x.WhenToTrigger.Contains(game.RoundNo));

                        if (tigr != null)
                        {
                            var tigr2 = _gameGlobal.TigrTop.Find(x =>
                                x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                            if (tigr2 == null)
                            {
                                _gameGlobal.TigrTop.Add(new Tigr.TigrTopClass(player.Status.PlayerId,
                                    game.GameId));
                            }
                            else
                            {
                                _gameGlobal.TigrTop.Remove(tigr2);
                                _gameGlobal.TigrTop.Add(new Tigr.TigrTopClass(player.Status.PlayerId,
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
                            x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

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
                                player.Character.Justice.AddJusticeForNextRound(5);
                            }

                        //end Школьник
                        break;
                    case "AWDKA":

                        //АФКА

                        var awdkaaa = _gameGlobal.AwdkaAfkTriggeredWhen.Find(x =>
                            x.GameId == player.GameId && x.PlayerId == player.Status.PlayerId);

                        if (awdkaaa != null)
                            if (awdkaaa.WhenToTrigger.Contains(game.RoundNo))
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
                            x.PlayerId == player.Status.PlayerId);

                        foreach (var enemy in awdkaa.TryingList)
                            if (enemy != null)
                                if (enemy.Times >= 2 && enemy.IsUnique == false)
                                {
                                    player.Status.LvlUpPoints += 2;
                                    await _gameUpdateMess.UpdateMessage(player);
                                    enemy.IsUnique = true;
                                    game.Phrases.AwdkaTrying.SendLog(player, true);
                                }
                        //end Я пытаюсь!:


                        //Научите играть 
                        var awdkaTempStats = _gameGlobal.AwdkaTeachToPlayTempStats.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        var awdka = _gameGlobal.AwdkaTeachToPlay.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

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
                            player.Character.SetIntelligence(player.Status, intelToGive, "Научите играть: ");
                            player.Character.SetStrength(player.Status, regularStats.Str + str, "Научите играть: ");
                            player.Character.SetSpeed(player.Status, regularStats.Speed + speed, "Научите играть: ");
                            player.Character.SetPsyche(player.Status, regularStats.Psyche + psy, "Научите играть: ");
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
                        _gameGlobal.AwdkaTeachToPlayTempStats.Add(new DeepList.Madness(player.Status.PlayerId,
                            game.GameId, game.RoundNo));

                        awdkaTempStats = _gameGlobal.AwdkaTeachToPlayTempStats.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                            player.Character.GetStrength(), player.Character.GetSpeed(), player.Character.GetPsyche()));
                        //end crazy shit

                        //find out  the biggest stat
                        var bestSkill = awdka.Training.OrderByDescending(x => x.StatNumber).ToList()[0];

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
                            player.Character.SetIntelligence(player.Status, intel1, "Научите играть: ");

                        if (str1 >= player.Character.GetStrength())
                            player.Character.SetStrength(player.Status, str1, "Научите играть: ");

                        if (speed1 >= player.Character.GetSpeed())
                            player.Character.SetSpeed(player.Status, speed1, "Научите играть: ");

                        if (pshy1 >= player.Character.GetPsyche())
                            player.Character.SetPsyche(player.Status, pshy1, "Научите играть: ");
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
                            x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);

                        if (glebChalleger != null)
                            if (glebChalleger.WhenToTrigger.Contains(game.RoundNo))
                                rand = _rand.Random(1, 7);


                        var geblTea =
                            _gameGlobal.GlebTea.Find(x =>
                                x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);

                        if (rand == 1)
                        {
                            geblTea.Ready = true;
                        }
                        
                        if(geblTea.Ready)
                            game.Phrases.GlebTeaReadyPhrase.SendLog(player, true);
                        //end  Я за чаем:


                        //Спящее хуйло:
                        if (game.RoundNo == 11) game.Phrases.GlebSleepyPhrase.SendLog(player, false);

                        acc = _gameGlobal.GlebSleepingTriggeredWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.ConfirmedSkip = false;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = Guid.Empty;

                                game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                            }
                        //end Спящее хуйло:

                        //Претендент русского сервера: 
                        acc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                var gleb = _gameGlobal.GlebChallengerList.Find(x =>
                                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);
                                //just check
                                if (gleb != null) _gameGlobal.GlebChallengerList.Remove(gleb);

                                _gameGlobal.GlebChallengerList.Add(new DeepList.Madness(player.Status.PlayerId,
                                    game.GameId, game.RoundNo));
                                gleb = _gameGlobal.GlebChallengerList.Find(x =>
                                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);
                                gleb.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                                    player.Character.GetStrength(), player.Character.GetSpeed(),
                                    player.Character.GetPsyche()));

                                //  var randomNumber =  _rand.Random(1, 100);

                                var intel = player.Character.GetIntelligence() >= 10 ? 10 : 9;
                                var str = player.Character.GetStrength() >= 10 ? 10 : 9;
                                var speed = player.Character.GetSpeed() >= 10 ? 10 : 9;
                                var pshy = player.Character.GetPsyche() >= 10 ? 10 : 9;


                                player.Character.SetIntelligence(player.Status, intel, "Претендент русского сервера: ");
                                player.Character.SetStrength(player.Status, str, "Претендент русского сервера: ");
                                player.Character.SetSpeed(player.Status, speed, "Претендент русского сервера: ");
                                player.Character.SetPsyche(player.Status, pshy, "Претендент русского сервера: ");
                                player.Character.AddExtraSkill(player.Status, "Претендент русского сервера: ", 100);


                                gleb.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));

                                game.Phrases.GlebChallengerPhrase.SendLog(player, true);
                                await game.Phrases.GlebChallengerSeparatePhrase.SendLogSeparate(player, true);
                            }

                        //end Претендент русского сервера
                        break;
                    case "DeepList":

                        //Сверхразум
                        var currentDeepList = _gameGlobal.DeepListSupermindTriggeredWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);

                        if (currentDeepList != null)
                            if (currentDeepList.WhenToTrigger.Any(x => x == game.RoundNo))
                            {
                                GamePlayerBridgeClass randPlayer;

                                do
                                {
                                    randPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];

                                    var check1 = _gameGlobal.DeepListSupermindKnown.Find(x =>
                                        x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                                    if (check1 != null)
                                        if (check1.KnownPlayers.Contains(randPlayer.Status.PlayerId))
                                            randPlayer = player;
                                } while (randPlayer.Status.PlayerId == player.Status.PlayerId);

                                var check = _gameGlobal.DeepListSupermindKnown.Find(x =>
                                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                                if (check == null)
                                    _gameGlobal.DeepListSupermindKnown.Add(new DeepList.SuperMindKnown(
                                        player.Status.PlayerId, game.GameId,
                                        randPlayer.Status.PlayerId));
                                else
                                    check.KnownPlayers.Add(randPlayer.Status.PlayerId);

                                await game.Phrases.DeepListSuperMindPhrase.SendLog(player, randPlayer, true);
                            }
                        //end Сверхразум

                        //Безумие

                        var madd = _gameGlobal.DeepListMadnessTriggeredWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        if (madd != null)
                            if (madd.WhenToTrigger.Contains(game.RoundNo))
                            {
                                //trigger maddness
                                //player.Status.AddBonusPoints(-3, "Безумие: ");

                                var curr = _gameGlobal.DeepListMadnessList.Find(x =>
                                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);
                                //just check
                                if (curr != null) _gameGlobal.DeepListMadnessList.Remove(curr);

                                _gameGlobal.DeepListMadnessList.Add(
                                    new DeepList.Madness(player.Status.PlayerId, game.GameId, game.RoundNo));
                                curr = _gameGlobal.DeepListMadnessList.Find(x =>
                                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);
                                curr.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                                    player.Character.GetStrength(), player.Character.GetSpeed(),
                                    player.Character.GetPsyche()));


                                var intel = 0;
                                var str = 0;
                                var speed = 0;
                                var pshy = 0;

                                for (var i = 0; i < 4; i++)
                                {
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
                                    }

                                    if (i == 0)
                                        intel = statNumber;
                                    else if (i == 1)
                                        str = statNumber;
                                    else if (i == 2)
                                        speed = statNumber;
                                    else if (i == 3) pshy = statNumber;
                                }

                                player.Character.SetIntelligence(player.Status, intel, "Безумие: ");
                                player.Character.SetStrength(player.Status, str, "Безумие: ");
                                player.Character.SetSpeed(player.Status, speed, "Безумие: ");
                                player.Character.SetPsyche(player.Status, pshy, "Безумие: ");
                                //2 это х3
                                player.Character.SetSkillMultiplier(3);

                                game.Phrases.DeepListMadnessPhrase.SendLog(player, true);
                                curr.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));
                            }
                        //end Безумие

                        break;

                    case "Sirinoks":

                        //Дракон
                        if (game.RoundNo == 10)
                        {
                            player.Character.SetIntelligence(player.Status, 10, "Дракон: ");
                            player.Character.SetStrength(player.Status, 10, "Дракон: ");
                            player.Character.SetSpeed(player.Status, 10, "Дракон: ");
                            player.Character.SetPsyche(player.Status, 10, "Дракон: ");

                            player.Character.AddExtraSkill(player.Status, "Дракон: ",
                                (int) player.Character.GetSkill());

                            var pointsToGive = (int) (player.Character.GetSkill() / 10);


                            var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                            if (siri != null)
                                for (var i = player.Status.PlaceAtLeaderBoard + 1; i < game.PlayersList.Count + 1; i++)
                                {
                                    var player2 = game.PlayersList[i - 1];
                                    if (siri.FriendList.Contains(player2.Status.PlayerId))
                                        pointsToGive -= 1;
                                }

                            player.Status.AddBonusPoints(pointsToGive, "Дракон: ");
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
                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId &&
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
                if (player == null) _log.Critical("HandleNextRoundAfterSorting - octopusPlayer == null");

                var characterName = player?.Character.Name;
                if (characterName == null) return;

                switch (characterName)
                {
                    case "Братишка":
                        //Булькает:
                        if (player.Status.PlaceAtLeaderBoard != 1)
                            player.Character.Justice.AddJusticeNow(player.Status);
                        //end Булькает:

                        //Челюсти:
                        if (game.RoundNo > 1)
                        {
                            var shark = _gameGlobal.SharkJawsLeader.Find(x =>
                                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);


                            if (!shark.FriendList.Contains(player.Status.PlaceAtLeaderBoard))
                            {
                                shark.FriendList.Add(player.Status.PlaceAtLeaderBoard);
                                player.Character.AddSpeed(player.Status, 1, "Челюсти: ");
                            }
                        }

                        //end Челюсти:
                        break;

                    case "Тигр":
                        //Тигр топ, а ты холоп: 
                        if (player.Status.PlaceAtLeaderBoard == 1 && game.RoundNo > 1)
                            if (game.RoundNo != 10)
                            {
                                player.Character.AddPsyche(player.Status, 1, "Тигр топ, а ты холоп: ");
                                //player.Character.AddMoral(player.Status, 1, "Тигр топ, а ты холоп: ");
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
                                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);
                            if (mitsuki != null)
                            {
                                var count = 0;
                                foreach (var t in mitsuki.Training.Where(x => x.Times >= 2))
                                {
                                    var player2 = game.PlayersList.Find(x => x.Status.PlayerId == t.EnemyId);
                                    if (player2 != null)
                                    {
                                        player2.Status.AddBonusPoints(-5, "Запах мусора: ");

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
                                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);
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
                                x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);
                            foreach (var target in game.PlayersList)
                            {
                                if (player.Status.PlayerId == target.Status.PlayerId) continue;
                                var found = hardKitty.LostSeries.Find(x => x.EnemyPlayerId == target.Status.PlayerId);

                                if (found != null)
                                    found.Series += 1;
                                else
                                    hardKitty.LostSeries.Add(new HardKitty.DoebatsyaSubClass(target.Status.PlayerId));
                            }
                        }

                        //end Никому не нужен:
                        break;

                    case "Darksci":

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
                            x.PlayerId == player.Status.PlayerId);

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
                            _gameGlobal.SharkJawsLeader.Add(new Shark.SharkLeaderClass(player.Status.PlayerId,
                                game.GameId));
                            _gameGlobal.SharkDontUnderstand.Add(
                                new Shark.SharkDontUnderstand(player.Status.PlayerId, game.GameId));
                            _gameGlobal.SharkJawsWin.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                            _gameGlobal.SharkBoole.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                            player.Status.AddInGamePersonalLogs(
                                "Братишка: **Буууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууль**\n");
                        }

                        break;
                }
            }

            //Если фидишь то пропушь, если пушишь то нафидь
            var god = game.PlayersList.Find(x => x.Character.Name == "Бог ЛоЛа");
            if (god != null && game.RoundNo >= 2)
            {
                var players = _gameGlobal.LolGodPushAndDieSubList.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == god.Status.PlayerId);

                players.PlayersEveryRound.Add(new LolGod.PushAndDieSubClass(game.RoundNo, game.PlayersList));

                var playersLastRound = players.PlayersEveryRound.Find(x => x.RoundNo == game.RoundNo - 1).PlayerList;
                var playersThisRound = players.PlayersEveryRound.Find(x => x.RoundNo == game.RoundNo).PlayerList;

                var top1ThisRound = playersThisRound.Find(x => x.PlayerPlaceAtLeaderBoard == 1).PlayerId;
                var isTop1LastRound =
                    playersLastRound.Find(x => x.PlayerId == top1ThisRound).PlayerPlaceAtLeaderBoard == 1;
                if (!isTop1LastRound)
                    game.PlayersList.Find(x => x.Status.PlayerId == top1ThisRound).Status
                        .AddRegularPoints(-1, "Если фидишь то пропушь, если пушишь то нафидь");


                foreach (var player in game.PlayersList)
                {
                    var placeAtLastRound = playersLastRound.Find(x => x.PlayerId == player.Status.PlayerId)
                        .PlayerPlaceAtLeaderBoard;
                    var placeAtThisRound = playersThisRound.Find(x => x.PlayerId == player.Status.PlayerId)
                        .PlayerPlaceAtLeaderBoard;

                    if (placeAtLastRound > placeAtThisRound)
                    {
                        player.Character.Justice.AddJusticeForNextRound();
                        game.Phrases.FirstСommandmentLost.SendLog(player, false);
                    }
                }
            }

            //end Если фидишь то пропушь, если пушишь то нафидь
        }


        //end after all fight

        //unique
        public async Task HandleShark(GameClass game)
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

            await Task.CompletedTask;
        }

        public int HandleJews(GamePlayerBridgeClass player, GameClass game)
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
                            _help.SendMsgAndDeleteItAfterRound(leCrisp, "МЫ жрём деньги!");
                        }
                        catch (Exception e)
                        {
                            _log.Critical(e.StackTrace);
                        }

                    if (!tolya.IsBot())
                        try
                        {
                            _help.SendMsgAndDeleteItAfterRound(tolya, "МЫ жрём деньги!");
                        }
                        catch (Exception e)
                        {
                            _log.Critical(e.StackTrace);
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

        public bool HandleOctopus(GamePlayerBridgeClass octopusPlayer, GamePlayerBridgeClass playerAttackedOctopus,
            GameClass game)
        {
            if (octopusPlayer.Character.Name != "Осьминожка") return true;
            if (playerAttackedOctopus.Character.Name == "DeepList")
            {
                var doubtfull = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                    x.PlayerId == playerAttackedOctopus.Status.PlayerId && x.GameId == game.GameId);

                if (doubtfull != null)
                    if (!doubtfull.FriendList.Contains(octopusPlayer.Status.PlayerId))
                        return true;
            }


            game.AddGlobalLogs($" ⟶ {playerAttackedOctopus.DiscordUsername}");

            //еврей
            var point = HandleJews(playerAttackedOctopus, game);
            //end еврей

            playerAttackedOctopus.Status.AddRegularPoints(point, "Победа");

            playerAttackedOctopus.Status.WonTimes++;
            playerAttackedOctopus.Character.Justice.IsWonThisRound = true;

            octopusPlayer.Character.Justice.AddJusticeForNextRound();

            playerAttackedOctopus.Status.IsWonThisCalculation = octopusPlayer.Status.PlayerId;
            octopusPlayer.Status.IsLostThisCalculation = playerAttackedOctopus.Status.PlayerId;
            octopusPlayer.Status.WhoToLostEveryRound.Add(
                new InGameStatus.WhoToLostPreviousRoundClass(playerAttackedOctopus.Status.PlayerId, game.RoundNo,
                    false));

            var octo = _gameGlobal.OctopusInkList.Find(x =>
                x.PlayerId == octopusPlayer.Status.PlayerId && x.GameId == game.GameId);

            if (octo == null)
            {
                _gameGlobal.OctopusInkList.Add(new Octopus.InkClass(octopusPlayer.Status.PlayerId, game,
                    playerAttackedOctopus.Status.PlayerId));
            }
            else
            {
                var enemyRealScore = octo.RealScoreList.Find(x => x.PlayerId == playerAttackedOctopus.Status.PlayerId);
                var octoRealScore = octo.RealScoreList.Find(x => x.PlayerId == octopusPlayer.Status.PlayerId);

                if (enemyRealScore == null)
                {
                    octo.RealScoreList.Add(new Octopus.InkSubClass(playerAttackedOctopus.Status.PlayerId, game.RoundNo,
                        -1));
                    octoRealScore.AddRealScore(game.RoundNo);
                }
                else
                {
                    enemyRealScore.AddRealScore(game.RoundNo, -1);
                    octoRealScore.AddRealScore(game.RoundNo);
                }
            }

            //octopusPlayer.Status.AddRegularPoints(1, "Чернильная завеса");
            //playerAttackedOctopus.Status.AddRegularPoints(-1, "Чернильная завеса");


            return false;
        }
        //end unique
    }
}