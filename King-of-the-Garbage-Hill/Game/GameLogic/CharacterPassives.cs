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
        //end helpers

        //chars
        private readonly Awdka _awdka;

        private readonly Darksci _darksci;
        private readonly DeepList _deepList;
        private readonly InGameGlobal _gameGlobal;
        private readonly GameUpdateMess _gameUpdateMess;
        private readonly Gleb _gleb;


        private readonly HardKitty _hardKitty;

        //helpers
        private readonly HelperFunctions _help;
        private readonly LeCrisp _leCrisp;
        private readonly LoginFromConsole _log;
        private readonly Mitsuki _mitsuki;
        private readonly Mylorik _mylorik;
        private readonly Octopus _octopus;
        private readonly Panth _panth;

        private readonly SecureRandom _rand;
        private readonly Shark _shark;
        private readonly Sirinoks _sirinoks;
        private readonly Tigr _tigr;
        private readonly Tolya _tolya;

        private readonly Vampyr _vampyr;
        //end chars


        public CharacterPassives(SecureRandom rand, HelperFunctions help, Awdka awdka, DeepList deepList,
            Gleb gleb, HardKitty hardKitty, Mitsuki mitsuki, LeCrisp leCrisp, Mylorik mylorik,
            Octopus octopus, Shark shark, Sirinoks sirinoks, Tigr tigr, Tolya tolya, InGameGlobal gameGlobal,
            Darksci darksci, LoginFromConsole log, GameUpdateMess gameUpdateMess,
            Panth panth, Vampyr vampyr)
        {
            _rand = rand;
            _help = help;
            _awdka = awdka;
            _deepList = deepList;
            _gleb = gleb;
            _hardKitty = hardKitty;
            _mitsuki = mitsuki;
            _leCrisp = leCrisp;
            _mylorik = mylorik;
            _octopus = octopus;
            _shark = shark;
            _sirinoks = sirinoks;
            _tigr = tigr;
            _tolya = tolya;
            _gameGlobal = gameGlobal;
            _darksci = darksci;
            _log = log;
            _gameUpdateMess = gameUpdateMess;
            _panth = panth;
            _vampyr = vampyr;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        public void CalculatePassiveChances(GameClass game)
        {
            foreach (var player in game.PlayersList)
            {
                var characterName = player.Character.Name;
                WhenToTriggerClass when;
                switch (characterName)
                {
                    case "HardKitty":
                        _gameGlobal.HardKittyMute.Add(new HardKitty.MuteClass(player.Status.PlayerId, game.GameId));
                        break;
                    case "Осьминожка":
                        _gameGlobal.OctopusTentaclesList.Add(new Octopus.TentaclesClass(player.Status.PlayerId,
                            game.GameId));
                        break;
                    case "Darksci":
                        _gameGlobal.DarksciLuckyList.Add(new Darksci.LuckyClass(player.Status.PlayerId,
                            game.GameId));
                        break;
                    case "Бог ЛоЛа":
                        _gameGlobal.LolGodPushAndDieSubList.Add(
                            new LolGod.PushAndDieClass(player.Status.PlayerId, game.GameId, game.PlayersList));
                        _gameGlobal.LolGodUdyrList.Add(new LolGod.Udyr(player.Status.PlayerId, game.GameId));
                        break;
                    case "Вампур":
                        _gameGlobal.VampyrKilledList.Add(new FriendsClass(player.Status.PlayerId,
                            game.GameId));
                        break;
                    case "Sirinoks":
                        _gameGlobal.SirinoksFriendsList.Add(new FriendsClass(player.Status.PlayerId,
                            game.GameId));
                        break;
                    case "Братишка":
                        _gameGlobal.SharkJawsLeader.Add(new Shark.SharkLeaderClass(player.Status.PlayerId,
                            game.GameId));
                        _gameGlobal.SharkJawsWin.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                        _gameGlobal.SharkBoole.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                        break;
                    case "Загадочный Спартанец в маске":


                        Guid enemy1;
                        Guid enemy2;

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy1 = game.PlayersList[randIndex].Status.PlayerId;
                        } while (enemy1 == player.Status.PlayerId);

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy2 = game.PlayersList[randIndex].Status.PlayerId;
                        } while (enemy2 == player.Status.PlayerId || enemy2 == enemy1);

                        _gameGlobal.PanthMark.Add(new FriendsClass(player.Status.PlayerId,
                            game.GameId, enemy1));
                        var panth = _gameGlobal.PanthMark.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);
                        panth.FriendList.Add(enemy2);


                        _gameGlobal.PanthShame.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                        _gameGlobal.PanthFirstBlood.Add(new FriendsClass(player.Status.PlayerId, game.GameId));


                        break;
                    case "DeepList":
                        _gameGlobal.DeepListDoubtfulTactic.Add(new FriendsClass(player.Status.PlayerId,
                            game.GameId));

                        when = _gameGlobal.GetWhenToTrigger(player, true, 6, 2, false, 6);
                        _gameGlobal.DeepListSupermindTriggeredWhen.Add(when);

                        when = _gameGlobal.GetWhenToTrigger(player, true, 6, 2);
                        _gameGlobal.DeepListMadnessTriggeredWhen.Add(when);

                        break;
                    case "mylorik":
                        when = _gameGlobal.GetWhenToTrigger(player, false, 10, 2);
                        _gameGlobal.MylorikBooleTriggeredWhen.Add(when);
                        break;
                    case "Тигр":
                        _gameGlobal.TigrTwoBetterList.Add(
                            new FriendsClass(player.Status.PlayerId, game.GameId));
                        when = _gameGlobal.GetWhenToTrigger(player, true, 10, 1);
                        _gameGlobal.TigrTopWhen.Add(when);
                        break;
                    case "AWDKA":
                        when = _gameGlobal.GetWhenToTrigger(player, false, 10, 1);
                        _gameGlobal.AwdkaAfkTriggeredWhen.Add(when);
                        _gameGlobal.AwdkaTrollingList.Add(new Awdka.TrollingClass(player.Status.PlayerId,
                            game.GameId));
                        _gameGlobal.AwdkaTryingList.Add(new Awdka.TryingClass(player.Status.PlayerId, game.GameId));
                        break;

                    case "Толя":
                        _gameGlobal.TolyaCount.Add(new Tolya.TolyaCountClass(game.GameId, player.Status.PlayerId));
                        _gameGlobal.TolyaTalked.Add(new Tolya.TolyaTalkedlClass(game.GameId, player.Status.PlayerId));
                        _gameGlobal.TolyaRammusTimes.Add(new FriendsClass(player.Status.PlayerId,
                            game.GameId));
                        break;

                    case "Mit*suki*":
                        when = _gameGlobal.GetWhenToTrigger(player, true, 0, 0);
                        _gameGlobal.MitsukiNoPcTriggeredWhen.Add(when);
                        break;

                    case "Глеб":
                        //Спящее хуйло chance   
                        when = _gameGlobal.GetWhenToTrigger(player, true, 3, 3);
                        _gameGlobal.GlebSleepingTriggeredWhen.Add(when);

                        //challenger when
                        var li = new List<int>();

                        foreach (var t in when.WhenToTrigger) li.Add(t);

                        bool flag;
                        do
                        {
                            when = _gameGlobal.GetWhenToTrigger(player, true, 6, 3, true, 12);
                            flag = false;
                            for (var i = 0; i < li.Count; i++)
                                if (when.WhenToTrigger.Contains(li[i]))
                                    flag = true;
                        } while (flag);

                        _gameGlobal.GlebChallengerTriggeredWhen.Add(when);

                        break;
                }
            }
        }


        public async Task HandleEveryAttackOnHim(GamePlayerBridgeClass playerIamAttacking,
            GamePlayerBridgeClass playerAttackFrom,
            GameClass game)
        {
            var characterName = playerIamAttacking.Character.Name;

            switch (characterName)
            {
                case "Братишка":
                    //Ничего не понимает: 
                    var shark = _gameGlobal.SharkBoole.Find(x =>
                        x.PlayerId == playerIamAttacking.Status.PlayerId &&
                        game.GameId == x.GameId);


                    if (!shark.FriendList.Contains(playerAttackFrom.Status.PlayerId))
                    {
                        shark.FriendList.Add(playerAttackFrom.Status.PlayerId);
                        playerAttackFrom.Character.AddIntelligence(playerAttackFrom.Status, -1,
                            "Ничего не понимает: ");
                    }

                    //end Ничего не понимает: 
                    break;

                case "Глеб":
                    //Я щас приду:
                    var rand = _rand.Random(1, 8);
                    if (rand == 1)
                    {
                        var acc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.PlayerId == playerIamAttacking.Status.PlayerId &&
                            playerIamAttacking.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                                return;


                        if (!playerIamAttacking.Status.IsSkip)
                        {
                            playerIamAttacking.Status.IsSkip = true;
                            _gameGlobal.GlebSkipList.Add(
                                new Gleb.GlebSkipClass(playerIamAttacking.Status.PlayerId, game.GameId));
                            game.Phrases.GlebComeBackPhrase.SendLog(playerIamAttacking, true);
                        }
                    }

                    //end Я щас приду:
                    break;
                case "LeCrisp":
                    //гребанные ассасисны
                    if (playerAttackFrom.Character.GetStrength() - playerIamAttacking.Character.GetStrength() >= 2
                        && !playerIamAttacking.Status.IsBlock
                        && !playerIamAttacking.Status.IsSkip)
                    {
                        playerIamAttacking.Status.IsAbleToWin = false;
                        game.Phrases.LeCrispAssassinsPhrase.SendLog(playerIamAttacking, false);
                    }
                    //end гребанные ассасисны

                    //Импакт: 
                    var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                        x.PlayerId == playerIamAttacking.Status.PlayerId && x.GameId == game.GameId);

                    if (lePuska == null)
                        _gameGlobal.LeCrispImpact.Add(
                            new LeCrisp.LeCrispImpactClass(playerIamAttacking.Status.PlayerId, game.GameId));
                    // end Импакт: 
                    break;

                case "Толя":
                    if (playerIamAttacking.Status.IsBlock)
                    {
                        // playerIamAttacking.Status.IsBlock = false;
                        playerAttackFrom.Status.IsAbleToWin = false;
                        var tolya = _gameGlobal.TolyaRammusTimes.Find(x =>
                            x.GameId == playerIamAttacking.GameId &&
                            x.PlayerId == playerIamAttacking.Status.PlayerId);
                        tolya.FriendList.Add(playerAttackFrom.Status.PlayerId);
                    }

                    break;

                case "HardKitty":
                    //Одиночество
                    playerIamAttacking.Status.AddRegularPoints(1, "Одиночество");
                    game.Phrases.HardKittyLonelyPhrase.SendLog(playerIamAttacking, true);
                    //Одиночество
                    break;

                case "Mit*suki*":
                    var mitsuki = _gameGlobal.MitsukiGarbageList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == playerIamAttacking.Status.PlayerId);

                    if (mitsuki == null)
                    {
                        _gameGlobal.MitsukiGarbageList.Add(new Mitsuki.GarbageClass(
                            playerIamAttacking.Status.PlayerId, game.GameId,
                            playerAttackFrom.Status.PlayerId));
                    }
                    else
                    {
                        if (!mitsuki.Training.Contains(playerAttackFrom.Status.PlayerId))
                            mitsuki.Training.Add(playerAttackFrom.Status.PlayerId);
                    }

                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleEveryAttackFromMe(GamePlayerBridgeClass player1,
            GamePlayerBridgeClass playerIamAttacking,
            GameClass game)
        {
            var characterName = player1.Character.Name;

            switch (characterName)
            {
                case "Загадочный Спартанец в маске":

                    //Первая кровь: 
                    var pant = _gameGlobal.PanthFirstBlood.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);
                    if (pant.FriendList.Count == 0) pant.FriendList.Add(playerIamAttacking.Status.PlayerId);

                    //end Первая кровь: 

                    //Они позорят военное искусство:
                    var panth = _gameGlobal.PanthShame.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);


                    if (!panth.FriendList.Contains(playerIamAttacking.Status.PlayerId))
                    {
                        panth.FriendList.Add(playerIamAttacking.Status.PlayerId);
                        playerIamAttacking.Character.AddStrength(playerIamAttacking.Status, -1,
                            "Они позорят военное искусство: ");
                        playerIamAttacking.Character.AddSpeed(playerIamAttacking.Status, -1,
                            "Они позорят военное искусство: ");
                    }


                    //end Они позорят военное искусство:
                    break;


                case "Глеб":
                    // Я за чаем:
                    var rand = _rand.Random(1, 8);

                    var gleb = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                        x.PlayerId == player1.Status.PlayerId &&
                        game.GameId == x.GameId);

                    if (gleb != null)
                        if (gleb.WhenToTrigger.Contains(game.RoundNo))
                            rand = _rand.Random(1, 7);


                    if (rand == 1)
                    {
                        _gameGlobal.AllSkipTriggeredWhen.Add(new WhenToTriggerClass(player1.Status.WhoToAttackThisTurn,
                            game.GameId,
                            game.RoundNo + 1));
                        player1.Status.AddRegularPoints(1, "Я за чаем");
                        game.Phrases.GlebTeaPhrase.SendLog(player1, true);
                    }
                    //end  Я за чаем:


                    break;

                case "Sirinoks":

                    //Friends
                    var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);

                    if(siri != null)
                        if (siri.FriendList.Contains(playerIamAttacking.Status.PlayerId))
                            playerIamAttacking.Status.IsBlock = false;

                    if (!siri.FriendList.Contains(playerIamAttacking.Status.PlayerId))
                    {
                        siri.FriendList.Add(playerIamAttacking.Status.PlayerId);
                        player1.Status.AddRegularPoints(1, "Заводить друзей");
                        game.Phrases.SirinoksFriendsPhrase.SendLog(player1, true);
                    }


                    //Friends end
                    break;

                case "AWDKA":

                    //Научите играть
                    var awdka = _gameGlobal.AwdkaTeachToPlay.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);

                    var player2Stats = new List<Sirinoks.TrainingSubClass>
                    {
                        new(1, playerIamAttacking.Character.GetIntelligence()),
                        new(2, playerIamAttacking.Character.GetStrength()),
                        new(3, playerIamAttacking.Character.GetSpeed()),
                        new(4, playerIamAttacking.Character.GetPsyche())
                    };
                    var sup = player2Stats.OrderByDescending(x => x.StatNumber).ToList()[0];
                    if (awdka == null)
                        _gameGlobal.AwdkaTeachToPlay.Add(new Sirinoks.TrainingClass(player1.Status.PlayerId,
                            game.GameId, sup.StatIndex, sup.StatNumber, Guid.Empty));
                    else
                        awdka.Training.Add(new Sirinoks.TrainingSubClass(sup.StatIndex, sup.StatNumber));

                    //end Научите играть

                    break;
            }

            await Task.CompletedTask;
        }



        public async Task HandleEventsAfterEveryBattle(GameClass game)
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
                {
                    shark.Status.AddRegularPoints(1, "Лежит на дне");
                    _log.Critical("shark + 1 TOP");
                }

                if (enemyBottom != null && enemyBottom.Status.IsLostThisCalculation != Guid.Empty)
                {
                    shark.Status.AddRegularPoints(1, "Лежит на дне");
                    _log.Critical("shark + 1 BOT");
                }
            }
            //end Лежит на дне:

            await Task.CompletedTask;
        }

        public void HandleCharacterAfterCalculations(GamePlayerBridgeClass player, GameClass game)
        {
            //TODO: test it
            //tolya count
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
            //tolya count end 


            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    _deepList.HandleDeepListAfter(player, game);
                    break;
                case "mylorik":
                    _mylorik.HandleMylorikAfter(player, game);
                    break;
                case "Глеб":
                    _gleb.HandleGlebAfter(player, game);
                    break;
                case "LeCrisp":
                    _leCrisp.HandleLeCrispAfter(player, game);
                    break;
                case "Толя":
                    _tolya.HandleTolyaAfter(player, game);
                    break;
                case "HardKitty":
                    _hardKitty.HandleHardKittyAfter(player, game);
                    break;
                case "Sirinoks":
                    _sirinoks.HandleSirinoksAfter(player, game);
                    break;
                case "Mit*suki*":
                    _mitsuki.HandleMitsukiAfter(player);
                    break;
                case "AWDKA":
                    _awdka.HandleAwdkaAfter(player, game);
                    break;
                case "Осьминожка":
                    _octopus.HandleOctopusAfter(player);
                    break;
                case "Darksci":
                    _darksci.HandleDarksiAfter(player, game);
                    break;
                case "Тигр":
                    _tigr.HandleTigrAfter(player, game);
                    break;
                case "Братишка":
                    _shark.HandleSharkAfter(player, game);
                    break;
                case "Загадочный Спартанец в маске":
                    _panth.HandlePanthAfter(player, game);
                    break;
                case "Вампур":
                    _vampyr.HandleVampyrAfter(player, game);
                    break;
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
                        //Ink
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

                        //end   //Ink
                        break;
                    case "Загадочный Спартанец в маске":
                        if (game.RoundNo == 10)
                            player.Character.SetStrength(player.Status, 0, "Они позорят военное искусство: ");

                        break;

                    case "mylorik":
                        var acc = _gameGlobal.MylorikBooleTriggeredWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = Guid.Empty;

                                game.Phrases.MylorikBoolePhrase.SendLog(player, false);
                            }

                        break;

                    case "Тигр":

                        //Стримснайпят и банят и банят и банят:
                        if (game.RoundNo == 10)
                        {
                            player.Status.IsSkip = true;
                            player.Status.IsBlock = false;
                            player.Status.IsAbleToTurn = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = Guid.Empty;
                            player.Character.SetPsyche(player.Status, 0, "Стримснайпят и банят и банят и банят: ");
                            player.Character.SetIntelligence(player.Status, 0,
                                "Стримснайпят и банят и банят и банят: ");
                            player.Character.SetStrength(player.Status, 10, "Стримснайпят и банят и банят и банят: ");
                            game.AddPreviousGameLogs(
                                $"**{player.DiscordUsername}:** ЕБАННЫЕ БАНЫ НА 10 ЛЕТ");
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
                        acc = _gameGlobal.MitsukiNoPcTriggeredWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = Guid.Empty;

                                game.Phrases.MitsukiSchoolboy.SendLog(player, true);
                            }

                        break;
                    case "AWDKA":

                        //trolling
                        if (game.RoundNo == 11)
                        {
                            var awdkaTroll = _gameGlobal.AwdkaTrollingList.Find(x =>
                                x.GameId == player.GameId &&
                                x.PlayerId == player.Status.PlayerId);

                            var enemy = awdkaTroll.EnemyList.Find(x =>
                                x.EnemyId == game.PlayersList[0].Status.PlayerId);

                            if (enemy != null)
                            {
                                player.Status.AddBonusPoints((enemy.Score + 1) / 2, "Троллинг: ");
                                game.Phrases.AwdkaTrolling.SendLog(player, true);
                            }
                        }

                        //end //trolling
                        //АФКА

                        var awdkaaa = _gameGlobal.AwdkaAfkTriggeredWhen.Find(x =>
                            x.GameId == player.GameId && x.PlayerId == player.Status.PlayerId);

                        if (awdkaaa != null)
                            if (awdkaaa.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
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
                                if (enemy.Times >= 2 && enemy.IsUnique == false && player.Status.LvlUpPoints != 3)
                                {
                                    player.Status.LvlUpPoints = 3;
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
                                player.Character.SetIntelligenceExtraText(" <:volibir:894286361895522434>");
                                break;
                            case 2:
                                str1 = bestSkill.StatNumber;
                                player.Character.SetStrengthExtraText(" <:volibir:894286361895522434>");
                                break;
                            case 3:
                                speed1 = bestSkill.StatNumber;
                                player.Character.SetSpeedExtraText(" <:volibir:894286361895522434>");
                                break;
                            case 4:
                                pshy1 = bestSkill.StatNumber;
                                player.Character.SetPsycheExtraText(" <:volibir:894286361895522434>");
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
                        //Спящее хуйло:
                        if (game.RoundNo == 11)
                        {
                            game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                        }

                        acc = _gameGlobal.GlebSleepingTriggeredWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = Guid.Empty;

                                game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                            }

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


                                gleb.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));

                                game.Phrases.GlebChallengerPhrase.SendLog(player, true);
                                await game.Phrases.GlebChallengerSeparatePhrase.SendLogSeparate(player, true);
                            }

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

                        //Madness

                        var madd = _gameGlobal.DeepListMadnessTriggeredWhen.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        if (madd != null)
                            if (madd.WhenToTrigger.Contains(game.RoundNo))
                            {
                                //trigger maddness
                                player.Status.AddBonusPoints(-3, "Безумие: ");

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

                                game.Phrases.DeepListMadnessPhrase.SendLog(player, true);
                                curr.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));
                            }
                        //end madness

                        break;

                    case "Sirinoks":

                        if (game.RoundNo == 10)
                        {
                            player.Character.SetIntelligence(player.Status, 10, "Дракон: ");
                            player.Character.SetStrength(player.Status, 10, "Дракон: ");
                            player.Character.SetSpeed(player.Status, 10, "Дракон: ");
                            player.Character.SetPsyche(player.Status, 10, "Дракон: ");

                            var pointsToGive = 10;


                            var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                            if (siri != null)
                                for (var i = player.Status.PlaceAtLeaderBoard + 1; i < game.PlayersList.Count + 1; i++)
                                {
                                    var player2 = game.PlayersList[i - 1];
                                    if (siri.FriendList.Contains(player2.Status.PlayerId))
                                        pointsToGive--;
                                }

                            player.Status.AddBonusPoints(pointsToGive, "Дракон: ");
                            game.Phrases.SirinoksDragonPhrase.SendLog(player, true);
                        }

                        break;
                }

                var isSkip = _gameGlobal.AllSkipTriggeredWhen.Find(x =>
                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId &&
                    x.WhenToTrigger.Contains(game.RoundNo));

                if (isSkip != null)
                {
                    player.Status.IsSkip = true;
                    player.Status.IsBlock = false;
                    player.Status.IsAbleToTurn = false;
                    player.Status.IsReady = true;
                    player.Status.WhoToAttackThisTurn = Guid.Empty;
                    player.Status.AddInGamePersonalLogs("Тебя усыпили...\n");
                }
            }
        }

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
                                    // player.Status.AddRegularPoints();
                                    player.Status.AddBonusPoints(3, "Лучше с двумя, чем с адекватными: ");
                                    game.Phrases.TigrTwoBetter.SendLog(player, false);
                                }
                            }
                        }

                        //end Лучше с двумя, чем с адекватными:
                        break;

                    case "DeepList":

                        //madness
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


                            player.Character.SetIntelligence(player.Status, regularStats.Intel + intel, "Безумие: ", false);
                            player.Character.SetStrength(player.Status, regularStats.Str + str, "Безумие: ", false);
                            player.Character.SetSpeed(player.Status, regularStats.Speed + speed, "Безумие: ", false);
                            player.Character.SetPsyche(player.Status, regularStats.Psyche + psy, "Безумие: ", false);
                            game.Phrases.DeepListMadnessEndPhrase.SendLog(player, true);
                            _gameGlobal.DeepListMadnessList.Remove(madd);
                        }

                        // end madness 
                        break;
                    case "Глеб":
                        //challenger
                        var glebChall = _gameGlobal.GlebChallengerList.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId &&
                            x.RoundItTriggered == game.RoundNo);

                        if (glebChall != null)
                        {
                            //x3 point:
                            player.Status.SetScoresToGiveAtEndOfRound(
                                (int) player.Status.GetScoresToGiveAtEndOfRound() * 3, "Претендент русского сервера");
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
                            _gameGlobal.GlebChallengerList.Remove(glebChall);
                        }

                        break;
                    case "LeCrisp":
                        //impact

                        var leImpact = _gameGlobal.LeCrispImpact.Find(x =>
                            x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                        if (leImpact == null || !leImpact.IsTriggered)
                        {
                            player.Status.AddBonusPoints(1, "Импакт: ");
                            player.Character.Justice.AddJusticeForNextRound();
                            game.Phrases.LeCrispImpactPhrase.SendLog(player, false);
                        }
                        else if (leImpact != null)
                        {
                            leImpact.IsTriggered = false;
                        }

                        //end impact

                        break;

                    case "Толя":
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

                                    tolyaTalked.PlayerHeTalkedAbout.Add(randomPlayer.Status.PlayerId);
                                    game.AddPreviousGameLogs(
                                        $"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {randomPlayer.Character.Name}");
                                }
                            }
                        }

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

                        break;

                    case "Осьминожка":

                        //привет со дна
                        if (game.SkipPlayersThisRound > 0)
                            player.Status.AddRegularPoints(game.SkipPlayersThisRound, "привет со дна");
                        //end привет со дна


                        break;

                    case "Sirinoks":
                        //training

                        var siri = _gameGlobal.SirinoksTraining.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                        if (siri != null && siri.Training.Count >= 1)
                        {
                            var stats = siri.Training.OrderByDescending(x => x.StatNumber).ToList()[0];

                            switch (stats.StatIndex)
                            {
                                case 1:
                                    player.Character.AddIntelligence(player.Status, 1, "Обучение: ");
                                    break;
                                case 2:
                                    player.Character.AddStrength(player.Status, 1, "Обучение: ");
                                    break;
                                case 3:
                                    player.Character.AddSpeed(player.Status, 1, "Обучение: ");
                                    break;
                                case 4:
                                    player.Character.AddPsyche(player.Status, 1, "Обучение: ");
                                    break;
                            }
                        }

                        //end training
                        break;

                    case "Mit*suki*":

                        //Дерзкая школота:
                        if (game.RoundNo == 1) game.Phrases.MitsukiCheekyBriki.SendLog(player, true);

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

                        //end  Дерзкая школота:

                        //Много выебывается:

                        //end Много выебывается:

                        break;
                }
            }

            await Task.CompletedTask;
        }

        //end общие говно

        public async Task HandleEveryAttackOnHimAfterCalculations(GamePlayerBridgeClass playerIamAttacking,
            GamePlayerBridgeClass player, GameClass game)
        {
            var characterName = playerIamAttacking.Character.Name;


            switch (characterName)
            {
                case "HardKitty":
                    //Mute passive
                    if (playerIamAttacking.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var hardKitty = _gameGlobal.HardKittyMute.Find(x =>
                            x.PlayerId == playerIamAttacking.Status.PlayerId &&
                            x.GameId == game.GameId);


                        if (!hardKitty.UniquePlayers.Contains(player.Status.PlayerId))
                        {
                            hardKitty.UniquePlayers.Add(player.Status.PlayerId);
                            player.Status.AddRegularPoints(1, "Mute");
                            game.Phrases.HardKittyMutedPhrase.SendLog(playerIamAttacking, false);
                        }
                    }

                    //Mute passive end
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleEveryAttackFromMeAfterCalculations(GamePlayerBridgeClass player,
            GamePlayerBridgeClass playerIamAttacking, GameClass game)
        {
            var characterName = player.Character.Name;


            switch (characterName)
            {
                case "Бог ЛоЛа":
                    _gameGlobal.LolGodUdyrList.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId)
                        .EnemyPlayerId = playerIamAttacking.Status.PlayerId;
                    game.Phrases.SecondСommandmentBan.SendLog(player, false);
                    break;
                case "Вампур":
                    //Вампуризм
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                        player.Character.Justice.SetJusticeForNextRound(playerIamAttacking.Character.Justice
                            .GetJusticeForNextRound());
                    //end   Вампуризм
                    break;


                case "Осьминожка":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var octo = _gameGlobal.OctopusInvulnerabilityList.Find(x =>
                            x.GameId == player.GameId &&
                            x.PlayerId == player.Status.PlayerId);

                        if (octo == null)
                            _gameGlobal.OctopusInvulnerabilityList.Add(
                                new Octopus.InvulnerabilityClass(player.Status.PlayerId, game.GameId));
                        else
                            octo.Count++;
                    }

                    break;

                case "Darksci":
                    var darscsi = _gameGlobal.DarksciLuckyList.Find(x =>
                        x.GameId == player.GameId &&
                        x.PlayerId == player.Status.PlayerId);

                    if (!darscsi.TouchedPlayers.Contains(playerIamAttacking.Status.PlayerId))
                        darscsi.TouchedPlayers.Add(playerIamAttacking.Status.PlayerId);

                    if (darscsi.TouchedPlayers.Count == game.PlayersList.Count - 1 && darscsi.Triggered == false)
                    {
                        player.Status.AddBonusPoints(player.Status.GetScore() * 3, "Повезло: ");

                        player.Character.AddPsyche(player.Status, 2, "Повезло: ");
                        darscsi.TouchedPlayers.Clear();
                        darscsi.Triggered = true;
                        game.Phrases.DarksciLucky.SendLog(player, true);
                    }


                    break;
            }

            await Task.CompletedTask;
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
                        if (player.Status.PlaceAtLeaderBoard != 1) player.Character.Justice.AddJusticeNow();
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
                        {
                            player.Character.AddPsyche(player.Status, 1, "Тигр топ, а ты холоп: ");
                            game.Phrases.TigrTop.SendLog(player, false);
                        }

                        //end Тигр топ, а ты холоп: 
                        break;

                    case "Mit*suki*":
                        //Много выебывается:
                        if (player.Status.PlaceAtLeaderBoard == 1 && game.RoundNo > 1)
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
                                foreach (var t in mitsuki.Training)
                                {
                                    var player2 = game.PlayersList.Find(x =>
                                        x.Status.PlayerId == t);
                                    if (player2 != null)
                                    {
                                        player2.Status.AddBonusPoints(-5, "Запах мусора: ");

                                        game.Phrases.MitsukiGarbageSmell.SendLog(player2, true);
                                        count++;
                                    }
                                }

                                game.AddPreviousGameLogs($"Mitsuki отнял в общей сумме {count * 5} очков.");
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
                    case "AWDKA":

                        break;

                    case "Darksci":

                        //Да всё нахуй эту игру (3, 6 and 9 are in LVL up):
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
                                    game.RoundNo == 10 && !game.GetAllGameLogs().Contains("Нахуй эту игру"))
                                    game.AddPreviousGameLogs(
                                        $"{player.DiscordUsername}: Нахуй эту игру..");
                            }
                        //end Да всё нахуй эту игру:
                        break;
                    case "Толя":

                        var tolya = _gameGlobal.TolyaCount.Find(x =>
                            x.GameId == player.GameId &&
                            x.PlayerId == player.Status.PlayerId);

                        tolya.Cooldown--;

                        if (tolya.Cooldown < 0)
                        {
                            tolya.IsReadyToUse = true;
                            game.Phrases.TolyaCountReadyPhrase.SendLog(player, false);
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


        public async Task HandleCharacterWithKnownEnemyBeforeCalculations(GamePlayerBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "Толя":

                    var tolya = _gameGlobal.TolyaCount.Find(x =>
                        x.GameId == player.GameId &&
                        x.PlayerId == player.Status.PlayerId);

                    if (tolya.IsReadyToUse && player.Status.WhoToAttackThisTurn != Guid.Empty)
                    {
                        tolya.TargetList.Add(new Tolya.TolyaCountSubClass(player.Status.WhoToAttackThisTurn,
                            game.RoundNo));
                        tolya.IsReadyToUse = false;
                        tolya.Cooldown = 2;
                    }

                    break;

                case "DeepList":
                    await _deepList.HandleDeepListTactics(player, game);
                    break;

                case "Вампур":
                    //игнор 1 справедливости
                    var enemy = game.PlayersList.Find(x =>
                        x.Status.PlayerId == player.Status.WhoToAttackThisTurn);
                    if (enemy != null)
                        if (enemy.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                            enemy.Character.Justice.SetJusticeNow(enemy.Status,
                                enemy.Character.Justice.GetJusticeNow() - 1, "Падальщик: ", false);
                    //игнор 1 справедливости
                    break;
            }


            await Task.CompletedTask;
        }


        public async Task<int> HandleJewPassive(GamePlayerBridgeClass player, GameClass game)
        {
            if (game.PlayersList.Any(x => x.Character.Name == "LeCrisp" || x.Character.Name == "Толя"))
            {
                if (player.Character.Name == "LeCrisp" || player.Character.Name == "Толя") return 1;

                var leCrisp = game.PlayersList.Find(x => x.Character.Name == "LeCrisp");
                var tolya = game.PlayersList.Find(x => x.Character.Name == "Толя");


                if (leCrisp != null && tolya != null)
                    if (leCrisp.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn &&
                        tolya.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn)
                    {
                        if (game.RoundNo > 4)
                        {
                            leCrisp.Status.AddRegularPoints(1, "Еврей");
                            tolya.Status.AddRegularPoints(1, "Еврей");
                            if (!leCrisp.IsBot())
                            {
                                try
                                {
                                    var mess =
                                        await leCrisp.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                            "МЫ жрём деньги!");
#pragma warning disable 4014
                                    _help.DeleteMessOverTime(mess);
                                }
                                catch (Exception e)
                                {
                                    _log.Critical(e.StackTrace);
                                }
#pragma warning restore 4014
                            }

                            if (!tolya.IsBot())
                            {
                                try
                                {
                                    var mess =
                                        await tolya.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                            "МЫ жрём деньги!");
#pragma warning disable 4014
                                    _help.DeleteMessOverTime(mess);
                                }
                                catch (Exception e)
                                {
                                    _log.Critical(e.StackTrace);
                                }
#pragma warning restore 4014
                            }


                            return 0;
                        }

                        return 1;
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
            }

            return 1;
        }


        public bool HandleOctopus(GamePlayerBridgeClass octopusPlayer, GamePlayerBridgeClass playerAttackedOctopus,
            GameClass game)
        {
            if (octopusPlayer.Character.Name != "Осьминожка") return true;


            game.AddPreviousGameLogs($" ⟶ {playerAttackedOctopus.DiscordUsername}");

            //еврей
            var point = HandleJewPassive(playerAttackedOctopus, game);
            //end еврей

            playerAttackedOctopus.Status.AddRegularPoints(point.Result, "Чернильная завеса");

            playerAttackedOctopus.Status.WonTimes++;
            playerAttackedOctopus.Character.Justice.IsWonThisRound = true;

            octopusPlayer.Character.Justice.AddJusticeForNextRound();

            playerAttackedOctopus.Status.IsWonThisCalculation = octopusPlayer.Status.PlayerId;
            octopusPlayer.Status.IsLostThisCalculation = playerAttackedOctopus.Status.PlayerId;
            octopusPlayer.Status.WhoToLostEveryRound.Add(new InGameStatus.WhoToLostPreviousRoundClass(playerAttackedOctopus.Status.PlayerId, game.RoundNo, false));

            var octo = _gameGlobal.OctopusInkList.Find(x =>
                x.PlayerId == octopusPlayer.Status.PlayerId &&
                x.GameId == game.GameId);

            if (octo == null)
            {
                _gameGlobal.OctopusInkList.Add(new Octopus.InkClass(octopusPlayer.Status.PlayerId, game,
                    playerAttackedOctopus.Status.PlayerId));
            }
            else
            {
                var enemyRealScore =
                    octo.RealScoreList.Find(x => x.PlayerId == playerAttackedOctopus.Status.PlayerId);
                var octoRealScore = octo.RealScoreList.Find(x => x.PlayerId == octopusPlayer.Status.PlayerId);

                if (enemyRealScore == null)
                {
                    octo.RealScoreList.Add(new Octopus.InkSubClass(playerAttackedOctopus.Status.PlayerId,
                        game.RoundNo, -1));
                    octoRealScore.AddRealScore(game.RoundNo);
                }
                else
                {
                    enemyRealScore.AddRealScore(game.RoundNo, -1);
                    octoRealScore.AddRealScore(game.RoundNo);
                }
            }

            octopusPlayer.Status.AddRegularPoints(1, "Чернильная завеса");
            playerAttackedOctopus.Status.AddRegularPoints(-1, "Чернильная завеса");


            return false;
        }
    }
}