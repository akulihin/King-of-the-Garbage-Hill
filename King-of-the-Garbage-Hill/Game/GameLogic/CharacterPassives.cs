using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.BotFramework;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
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
        private readonly DeepList2 _deepList2;
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
        private readonly CharactersUniquePhrase _phrase;
        private readonly SecureRandom _rand;
        private readonly Shark _shark;
        private readonly Sirinoks _sirinoks;
        private readonly Tigr _tigr;

        private readonly Tolya _tolya;
        //end chars
      

        public CharacterPassives(SecureRandom rand, HelperFunctions help, Awdka awdka, DeepList deepList,
            DeepList2 deepList2, Gleb gleb, HardKitty hardKitty, Mitsuki mitsuki, LeCrisp leCrisp, Mylorik mylorik,
            Octopus octopus, Shark shark, Sirinoks sirinoks, Tigr tigr, Tolya tolya, InGameGlobal gameGlobal,
            Darksci darksci, CharactersUniquePhrase phrase, LoginFromConsole log, GameUpdateMess gameUpdateMess,
            Panth panth)
        {
            _rand = rand;
            _help = help;
            _awdka = awdka;
            _deepList = deepList;
            _deepList2 = deepList2;
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
            _phrase = phrase;
            _log = log;
            _gameUpdateMess = gameUpdateMess;
            _panth = panth;
           
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        //общее говно
        public async Task HandleEveryAttackOnHim(GameBridgeClass playerIamAttacking, GameBridgeClass playerAttackFrom,
            GameClass game)
        {
            var characterName = playerIamAttacking.Character.Name;

            //еврей

            //end еврей


            switch (characterName)
            {
                case "Братишка":
                    //Ничего не понимает: 
                    var shark = _gameGlobal.SharkBoole.Find(x =>
                        x.PlayerDiscordId == playerIamAttacking.DiscordAccount.DiscordId &&
                        game.GameId == x.GameId);

                    if (shark == null)
                    {
                        _gameGlobal.SharkBoole.Add(new Sirinoks.FriendsClass(
                            playerIamAttacking.DiscordAccount.DiscordId, game.GameId,
                            playerAttackFrom.DiscordAccount.DiscordId));
                        playerAttackFrom.Character.AddIntelligence(playerAttackFrom.Status, -1);
                    }
                    else
                    {
                        if (!shark.FriendList.Contains(playerAttackFrom.DiscordAccount.DiscordId))
                        {
                            shark.FriendList.Add(playerAttackFrom.DiscordAccount.DiscordId);
                            playerAttackFrom.Character.AddIntelligence(playerAttackFrom.Status, -1);
                        }
                    }

                    //end Ничего не понимает: 
                    break;

                case "Глеб":
                    //Я щас приду:
                    var rand = _rand.Random(1, 8);
                    if (rand == 1)
                    {
                        var acc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.DiscordId == playerIamAttacking.DiscordAccount.DiscordId &&
                            playerIamAttacking.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                                return;

                        if (!playerIamAttacking.Status.IsSkip && playerIamAttacking.Character.GetStrength() < 9 
                                                              && playerIamAttacking.Character.GetSpeed() < 9 
                                                              && playerIamAttacking.Character.GetIntelligence() < 9)
                        {
                            playerIamAttacking.Status.IsSkip = true;
                            _gameGlobal.GlebSkipList.Add(
                                new Gleb.GlebSkipClass(playerIamAttacking.DiscordAccount.DiscordId, game.GameId));
                            await _phrase.GlebComeBackPhrase.SendLog(playerIamAttacking);
                        }
                    }
                    //end Я щас приду:
                    break;
                case "LeCrisp":
                    //гребанные ассасисны
                    if (playerAttackFrom.Character.GetStrength() - playerIamAttacking.Character.GetStrength() >= 2)
                    {
                        playerIamAttacking.Status.IsAbleToWin = false;
                        await _phrase.LeCrispAssassinsPhrase.SendLog(playerIamAttacking);
                    }
                    //end гребанные ассасисны

                    //Импакт: 
                    var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                        x.DiscordId == playerIamAttacking.DiscordAccount.DiscordId && x.GameId == game.GameId);

                    if (lePuska == null)
                    {
                        _gameGlobal.LeCrispImpact.Add(new LeCrisp.LeCrispImpactClass(playerIamAttacking.DiscordAccount.DiscordId, game.GameId));
                    }
                    else
                    {
                        lePuska.IsTriggered = true;
                    }
                    // end Импакт: 
                    break;

                case "Толя":
                    if (playerIamAttacking.Status.IsBlock)
                    {
                        // playerIamAttacking.Status.IsBlock = false;
                        playerAttackFrom.Status.IsAbleToWin = false;
                        var tolya = _gameGlobal.TolyaRammusTimes.Find(x =>
                            x.GameId == playerIamAttacking.DiscordAccount.GameId &&
                            x.PlayerDiscordId == playerIamAttacking.DiscordAccount.DiscordId);
                        tolya.FriendList.Add(playerAttackFrom.DiscordAccount.DiscordId);

                   
                    }

                    break;

                case "HardKitty":
                    playerIamAttacking.Status.AddRegularPoints();
                    await _phrase.HardKittyLonelyPhrase.SendLog(playerIamAttacking);
                    break;

                case "Mitsuki":
                    var mitsuki = _gameGlobal.MitsukiGarbageList.Find(x =>
                        x.GameId == game.GameId && x.PlayerDiscordId == playerIamAttacking.DiscordAccount.DiscordId);

                    if (mitsuki == null)
                    {
                        _gameGlobal.MitsukiGarbageList.Add(new Mitsuki.GarbageClass(
                            playerIamAttacking.DiscordAccount.DiscordId, game.GameId,
                            playerAttackFrom.DiscordAccount.DiscordId));
                    }
                    else
                    {
                        if (!mitsuki.Training.Contains(playerAttackFrom.DiscordAccount.DiscordId))
                            mitsuki.Training.Add(playerAttackFrom.DiscordAccount.DiscordId);
                    }

                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleEveryAttackFromMe(GameBridgeClass player1, GameBridgeClass playerIamAttacking,
            GameClass game)
        {
            var characterName = player1.Character.Name;

            switch (characterName)
            {
                case "Загадочный Спартанец в маске":

                    //Первая кровь: 
                    var panth = _gameGlobal.PanthFirstBlood.Find(x =>
                        x.GameId == game.GameId && x.PlayerDiscordId == player1.DiscordAccount.DiscordId);

                    if (panth == null)
                        _gameGlobal.PanthFirstBlood.Add(new Sirinoks.FriendsClass(player1.DiscordAccount.DiscordId,
                            game.GameId, playerIamAttacking.DiscordAccount.DiscordId));
                    //end Первая кровь: 

                    //Они позорят военное искусство:
                    panth = _gameGlobal.PanthShame.Find(x =>
                        x.GameId == game.GameId && x.PlayerDiscordId == player1.DiscordAccount.DiscordId);

                    if (panth == null)
                    {
                        _gameGlobal.PanthShame.Add(new Sirinoks.FriendsClass(player1.DiscordAccount.DiscordId,
                            game.GameId, playerIamAttacking.DiscordAccount.DiscordId));
                        playerIamAttacking.Character.AddStrength(playerIamAttacking.Status,-1);
                    }
                    else
                    {
                        if (!panth.FriendList.Contains(playerIamAttacking.DiscordAccount.DiscordId))
                        {
                            panth.FriendList.Add(playerIamAttacking.DiscordAccount.DiscordId);
                            playerIamAttacking.Character.AddStrength(playerIamAttacking.Status, -1);
                        }
                    }

                    //end Они позорят военное искусство:
                    break;


                case "Глеб":
                    // Я за чаем:
                    var rand = _rand.Random(1, 10);
                    if (player1.Character.GetIntelligence() >= 9 && player1.Character.GetStrength() >= 9 &&
                        player1.Character.GetSpeed() >= 9 && player1.Character.GetPsyche() >= 9) rand = _rand.Random(1, 9);
                    if (rand == 1)
                    {
                        _gameGlobal.AllSkipTriggeredWhen.Add(new WhenToTriggerClass(player1.Status.WhoToAttackThisTurn,
                            game.GameId,
                            game.RoundNo + 1));
                        player1.Status.AddRegularPoints();
                        await _phrase.GlebTeaPhrase.SendLog(player1);
                    }
                    //end  Я за чаем:


                    break;

                case "Sirinoks":

                    //Friends
                    var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                        x.GameId == game.GameId && x.PlayerDiscordId == player1.DiscordAccount.DiscordId);

                    if (siri == null)
                    {
                        _gameGlobal.SirinoksFriendsList.Add(new Sirinoks.FriendsClass(player1.DiscordAccount.DiscordId,
                            game.GameId, playerIamAttacking.DiscordAccount.DiscordId));
                        player1.Status.AddRegularPoints();
                        await _phrase.SirinoksFriendsPhrase.SendLog(player1);
                        break;
                    }

                    if (!siri.FriendList.Contains(playerIamAttacking.DiscordAccount.DiscordId))
                    {
                        siri.FriendList.Add(playerIamAttacking.DiscordAccount.DiscordId);
                        player1.Status.AddRegularPoints();
                        await _phrase.SirinoksFriendsPhrase.SendLog(player1);
                    }

                    if (siri.FriendList.Contains(playerIamAttacking.DiscordAccount.DiscordId))
                        playerIamAttacking.Status.IsBlock = false;
                    //Friends end
                    break;

                case "AWDKA":

                    //Научите играть
                    var awdka = _gameGlobal.AwdkaTeachToPlay.Find(x =>
                        x.GameId == game.GameId && x.PlayerDiscordId == player1.DiscordAccount.DiscordId);

                    var player2Stats = new List<Sirinoks.TrainingSubClass>
                    {
                        new Sirinoks.TrainingSubClass(1, playerIamAttacking.Character.GetIntelligence()),
                        new Sirinoks.TrainingSubClass(2, playerIamAttacking.Character.GetStrength()),
                        new Sirinoks.TrainingSubClass(3, playerIamAttacking.Character.GetSpeed()),
                        new Sirinoks.TrainingSubClass(4, playerIamAttacking.Character.GetPsyche())
                    };
                    var sup = player2Stats.OrderByDescending(x => x.StatNumber).ToList()[0];
                    if (awdka == null)
                        _gameGlobal.AwdkaTeachToPlay.Add(new Sirinoks.TrainingClass(player1.DiscordAccount.DiscordId,
                            game.GameId, sup.StatIndex, sup.StatNumber));
                    else
                        awdka.Training.Add(new Sirinoks.TrainingSubClass(sup.StatIndex, sup.StatNumber));

                    //end Научите играть

                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleCharacterBeforeCalculations(GameBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    _deepList.HandleDeepList(player);
                    break;
                case "mylorik":
                    await _mylorik.HandleMylorik(player, game);
                    break;
                case "Глеб":
                    _gleb.HandleGleb(player);
                    break;
                case "LeCrisp":
                    _leCrisp.HandleLeCrisp(player);
                    break;
                case "Толя":
                    _tolya.HandleTolya(player);
                    break;
                case "HardKitty":
                    _hardKitty.HandleHardKitty(player);
                    break;
                case "Sirinoks":
                    _sirinoks.HandleSirinoks(player);
                    break;
                case "Mitsuki":
                    _mitsuki.HandleMitsuki(player);
                    break;
                case "AWDKA":
                    _awdka.HandleAwdka(player);
                    break;
                case "Осьминожка":
                    _octopus.HandleOctopus(player);
                    break;
                case "Darksci":
                    _darksci.HandleDarksci(player);
                    break;
                case "Тигр":
                    _tigr.HandleTigr(player);
                    break;
                case "Братишка":
                    _shark.HandleShark(player);
                    break;
                case "????":
                    _deepList2.HandleDeepList2(player);
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleCharacterAfterCalculations(GameBridgeClass player, GameClass game)
        {
            //tolya count
            if (player.Status.IsWonLastTime != 0 && player.Character.Name != "Толя" &&
                game.PlayersList.Any(x => x.Character.Name == "Толя"))
            {
                var tolya = _gameGlobal.TolyaCount.Find(x =>
                    x.PlayerDiscordId == player.DiscordAccount.DiscordId && x.GameId == player.DiscordAccount.GameId);

                if (tolya != null)
                    if (player.Status.IsWonLastTime == tolya.WhoToLostLastTime && tolya.Cooldown <= 0)
                    {
                        var tolyaAcc = game.PlayersList.Find(x =>
                            x.DiscordAccount.DiscordId == player.DiscordAccount.DiscordId &&
                            x.DiscordAccount.GameId == player.DiscordAccount.GameId);
                        tolyaAcc.Status.AddRegularPoints();
                        await _phrase.TolyaCountPhrase.SendLog(player);
                        tolya.Cooldown = 1;
                    }
            }
            //tolya count end


            //shark Лежит на дне:
            if (game.PlayersList.Any(x => x.Character.Name == "Братишка"))
            {
                var shark = game.PlayersList.Find(x => x.Character.Name == "Братишка");

                var enemyTop =
                    game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard - 1 == shark.Status.PlaceAtLeaderBoard);
                var enemyBottom =
                    game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard + 1 == shark.Status.PlaceAtLeaderBoard);
                if (enemyTop != null && enemyTop.Status.IsLostLastTime != 0) shark.Status.AddRegularPoints();
                if (enemyBottom != null && enemyBottom.Status.IsLostLastTime != 0) shark.Status.AddRegularPoints();
            }
            //end Лежит на дне:


            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    await _deepList.HandleDeepListAfter(player, game);
                    break;
                case "mylorik":
                    await _mylorik.HandleMylorikAfter(player, game);
                    break;
                case "Глеб":
                    _gleb.HandleGlebAfter(player);
                    break;
                case "LeCrisp":
                    _leCrisp.HandleLeCrispAfter(player);
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
                case "Mitsuki":
                    _mitsuki.HandleMitsukiAfter(player);
                    break;
                case "AWDKA":
                    _awdka.HandleAwdkaAfter(player);
                    break;
                case "Осьминожка":
                    _octopus.HandleOctopusAfter(player);
                    break;
                case "Darksci":
                    await _darksci.HandleDarksiAfter(player, game);
                    break;
                case "Тигр":
                    await _tigr.HandleTigrAfter(player, game);
                    break;
                case "Братишка":
                    _shark.HandleSharkAfter(player, game);
                    break;
                case "????":
                    _deepList2.HandleDeepList2After(player);
                    break;
                case "Загадочный Спартанец в маске":
                    _panth.HandlePanthAfter(player, game);
                    break;
            }

            if (player.Status.WhoToAttackThisTurn == 0 && player.Status.IsBlock == false) player.Status.IsBlock = true;
        }

        public void CalculatePassiveChances(GameClass game)
        {
            foreach (var player in game.PlayersList)
            {
                var characterName = player.Character.Name;
                WhenToTriggerClass when;
                switch (characterName)
                {
                    case "Загадочный Спартанец в маске":

                        ulong enemy1;
                        ulong enemy2;

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count);
                            enemy1 = game.PlayersList[randIndex].DiscordAccount.DiscordId;
                        } while (enemy1 == player.DiscordAccount.DiscordId);

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count);
                            enemy2 = game.PlayersList[randIndex].DiscordAccount.DiscordId;
                        } while (enemy2 == player.DiscordAccount.DiscordId || enemy2 == enemy1);

                        _gameGlobal.PanthMark.Add(new Sirinoks.FriendsClass(player.DiscordAccount.DiscordId,
                            game.GameId, enemy1));
                        var panth = _gameGlobal.PanthMark.Find(x =>
                            x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);
                        panth.FriendList.Add(enemy2);

                        break;
                    case "DeepList":

                        when = _gameGlobal.GetWhenToTrigger(player, true, 6, 2, false, 6);
                        _gameGlobal.DeepListSupermindTriggeredWhen.Add(when);

                        when = _gameGlobal.GetWhenToTrigger(player, false, 10, 2);
                        _gameGlobal.DeepListMadnessTriggeredWhen.Add(when);

                        break;
                    case "mylorik":
                        when = _gameGlobal.GetWhenToTrigger(player, false, 10, 2);
                        _gameGlobal.MylorikBooleTriggeredWhen.Add(when);
                        break;
                    case "Тигр":
                        when = _gameGlobal.GetWhenToTrigger(player, true, 10, 1);
                        _gameGlobal.TigrTopWhen.Add(when);
                        break;
                    case "AWDKA":
                        when = _gameGlobal.GetWhenToTrigger(player, false, 10, 1);
                        _gameGlobal.AwdkaAfkTriggeredWhen.Add(when);
                        _gameGlobal.AwdkaTrollingList.Add(new Awdka.TrollingClass(player.DiscordAccount.DiscordId,
                            game.GameId));
                        break;

                    case "Толя":
                        _gameGlobal.TolyaCount.Add(new Tolya.TolyaCountClass( game.GameId, player.DiscordAccount.DiscordId, 0 ));
                        _gameGlobal.TolyaRammusTimes.Add(new Sirinoks.FriendsClass(player.DiscordAccount.DiscordId, game.GameId));
                        break;

                    case "Mitsuki":
                        when = _gameGlobal.GetWhenToTrigger(player, true, 0, 0);
                        _gameGlobal.MitsukiNoPcTriggeredWhen.Add(when);
                        break;

                    case "Глеб":
                        //Спящее хуйло chance
                        when = _gameGlobal.GetWhenToTrigger(player, true, 4, 3);
                        _gameGlobal.GlebSleepingTriggeredWhen.Add(when);

                        //challenger when
                        var li = new List<int>();

                        foreach (var t in when.WhenToTrigger) li.Add(t);

                        bool flag;
                        do
                        {
                            when = _gameGlobal.GetWhenToTrigger(player, true, 12, 3, true);
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


        public async Task HandleNextRound(GameClass game)
        {
            foreach (var player in game.PlayersList)
            {
                var characterName = player.Character.Name;
                switch (characterName)
                {
                    case "Загадочный Спартанец в маске":
                        if (game.RoundNo == 10) player.Character.SetStrength(0);

                        break;

                    case "mylorik":
                        var acc = _gameGlobal.MylorikBooleTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = 0;

                                await _phrase.MylorikBoolePhrase.SendLog(player);

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
                            player.Status.WhoToAttackThisTurn = 0;
                            player.Character.SetPsyche(0);
                            player.Character.SetIntelligence(0);
                            player.Character.SetStrength(10);
                            game.PreviousGameLogs += $"\n**{player.DiscordAccount.DiscordUserName}:** ЕБАННЫЕ БАНЫ НА 10 ЛЕТ\n";
                        }
                        //end Стримснайпят и банят и банят и банят:

                        //Тигр топ, а ты холоп:

                        var tigr = _gameGlobal.TigrTopWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId &&
                            x.WhenToTrigger.Contains(game.RoundNo));

                        if (tigr != null)
                        {
                            var tigr2 = _gameGlobal.TigrTop.Find(x =>
                                x.PlayerDiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);

                            if (tigr2 == null)
                            {
                                _gameGlobal.TigrTop.Add(new Tigr.TigrTopClass(player.DiscordAccount.DiscordId,
                                    game.GameId));
                            }
                            else
                            {
                                _gameGlobal.TigrTop.Remove(tigr2);
                                _gameGlobal.TigrTop.Add(new Tigr.TigrTopClass(player.DiscordAccount.DiscordId,
                                    game.GameId));
                            }
                        }

                        //end Тигр топ, а ты холоп:

                        break;


                    case "Даркси":
                        //Дизмораль
                        if (game.RoundNo == 9)
                        {
                            player.Character.AddPsyche(player.Status, -4); 
                            await _phrase.DarksciDysmoral.SendLog(player);
                            game.PreviousGameLogs += "Всё, у меня горит!\n";

                        }
                        //end Дизмораль

                        //Да всё нахуй эту игру:
                        if (player.Character.GetPsyche() <= 0)
                        {
                            player.Status.IsSkip = true;
                            player.Status.IsBlock = false;
                            player.Status.IsAbleToTurn = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = 0;
                            await _phrase.DarksciFuckThisGame.SendLog(player);

                            if (game.RoundNo >= 9)
                                game.PreviousGameLogs += $"**{player.DiscordAccount.DiscordUserName}:** Нахуй эту игру...\n";
                        }
                        //end Да всё нахуй эту игру:


                        break;


                    case "Mitsuki":
                        acc = _gameGlobal.MitsukiNoPcTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = 0;

                                await _phrase.MitsukiSchoolboy.SendLog(player);
                            }

                        break;
                    case "AWDKA":

                        //АФКА

                        var awdkaaa = _gameGlobal.AwdkaAfkTriggeredWhen.Find(x =>
                            x.GameId == player.DiscordAccount.GameId && x.DiscordId == player.DiscordAccount.DiscordId);

                        if (awdkaaa != null)
                            if (awdkaaa.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = 0;

                                await _phrase.AwdkaAfk.SendLog(player);
                            }
                        //end АФКА

                        //Я пытаюсь!:
                        var awdkaa = _gameGlobal.AwdkaTryingList.Find(x =>
                            x.GameId == player.DiscordAccount.GameId &&
                            x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        if (awdkaa != null)
                            foreach (var enemy in awdkaa.TryingList)
                                if (enemy != null)
                                    if (enemy.Times >= 2 && enemy.IsUnique == false)
                                    {
                                        player.Status.LvlUpPoints = 3;
                                        await _gameUpdateMess.UpdateMessage(player);
                                        enemy.IsUnique = true;
                                        await _phrase.AwdkaTrying.SendLog(player);
                                    }
                        //end Я пытаюсь!:


                        //Научите играть 
                        var awdkaTempStats = _gameGlobal.AwdkaTeachToPlayTempStats.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                        var awdka = _gameGlobal.AwdkaTeachToPlay.Find(x =>
                            x.PlayerDiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);

                        //remove stats from previos time
                        if (awdkaTempStats != null)
                        {
                            var regularStats = awdkaTempStats.MadnessList.Find(x => x.Index == 1);
                            var madStats = awdkaTempStats.MadnessList.Find(x => x.Index == 2);

                            var intel = player.Character.GetIntelligence() - madStats.Intel;
                            var str = player.Character.GetStrength() - madStats.Str;
                            var speed = player.Character.GetSpeed() - madStats.Speed;
                            var psy = player.Character.GetPsyche() - madStats.Psyche;

                            player.Character.SetIntelligence(regularStats.Intel + intel);
                            player.Character.SetStrength(regularStats.Str + str);
                            player.Character.SetSpeed(regularStats.Speed + speed);
                            player.Character.SetPsyche(regularStats.Psyche + psy);

                            _gameGlobal.AwdkaTeachToPlayTempStats.Remove(awdkaTempStats);
                        }
                        //end remove stats

                        //if there is no one have been attacked from awdka
                        if (awdka == null) return;
                        //end if there..

                        //crazy shit
                        _gameGlobal.AwdkaTeachToPlayTempStats.Add(new DeepList.Madness(player.DiscordAccount.DiscordId,
                            game.GameId, game.RoundNo));
                        awdkaTempStats = _gameGlobal.AwdkaTeachToPlayTempStats.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
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
                                break;
                            case 2:
                                str1 = bestSkill.StatNumber;
                                break;
                            case 3:
                                speed1 = bestSkill.StatNumber;
                                break;
                            case 4:
                                pshy1 = bestSkill.StatNumber;
                                break;
                        }

                        player.Character.SetIntelligence(intel1);
                        player.Character.SetStrength(str1);
                        player.Character.SetSpeed(speed1);
                        player.Character.SetPsyche(pshy1);
                        //end find out  the biggest stat

                        //crazy shit 2
                        awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(2, intel1, str1, speed1, pshy1));
                        _gameGlobal.AwdkaTeachToPlay.Remove(awdka);
                        //end crazy shit 2

                        await _phrase.AwdkaTeachToPlay.SendLog(player);

                        //end Научите играть: 
                        break;

                    case "Глеб":
                        //Спящее хуйло:
                        acc = _gameGlobal.GlebSleepingTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = 0;

                               await _phrase.GlebSleepyPhrase.SendLog(player);
                            }

                        //Претендент русского сервера: 
                        acc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                var gleb = _gameGlobal.GlebChallengerList.Find(x =>
                                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                                //just check
                                if (gleb != null) _gameGlobal.GlebChallengerList.Remove(gleb);

                                _gameGlobal.GlebChallengerList.Add(new DeepList.Madness(player.DiscordAccount.DiscordId,
                                    game.GameId, game.RoundNo));
                                gleb = _gameGlobal.GlebChallengerList.Find(x =>
                                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                                gleb.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                                    player.Character.GetStrength(), player.Character.GetSpeed(), player.Character.GetPsyche()));

                                //  var randomNumber =  _rand.Random(1, 100);

                                var intel = player.Character.GetIntelligence() >= 10 ? 10 : 9;
                                var str = player.Character.GetStrength() >= 10 ? 10 : 9;
                                var speed = player.Character.GetSpeed() >= 10 ? 10 : 9;
                                var pshy = player.Character.GetPsyche() >= 10 ? 10 : 9;


                                player.Character.SetIntelligence(intel); 
                                player.Character.SetStrength(str); 
                                player.Character.SetSpeed(speed); 
                                player.Character.SetPsyche(pshy); 


                                gleb.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));

                                await _phrase.GlebChallengerPhrase.SendLog(player);
                            }

                        break;
                    case "DeepList":

                        //Сверхразум
                        var currentDeepList = _gameGlobal.DeepListSupermindTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && game.GameId == x.GameId);

                        if (currentDeepList != null)
                            if (currentDeepList.WhenToTrigger.Any(x => x == game.RoundNo))
                            {
                                GameBridgeClass randPlayer;

                                do
                                {
                                    randPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];

                                    var check1 = _gameGlobal.DeepListSupermindKnown.Find(x =>
                                        x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);

                                    if (check1 != null)
                                        if (check1.KnownPlayers.Contains(randPlayer.DiscordAccount.DiscordId))
                                            randPlayer = player;
                                } while (randPlayer.DiscordAccount.DiscordId == player.DiscordAccount.DiscordId);

                                var check = _gameGlobal.DeepListSupermindKnown.Find(x =>
                                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);

                                if (check == null)
                                    _gameGlobal.DeepListSupermindKnown.Add(new DeepList.SuperMindKnown(
                                        player.DiscordAccount.DiscordId, game.GameId,
                                        randPlayer.DiscordAccount.DiscordId));
                                else
                                    check.KnownPlayers.Add(randPlayer.DiscordAccount.DiscordId);

                                await _phrase.DeepListSuperMindPhrase.SendLog(player, randPlayer);
                            }
                        //end Сверхразум

                        //Madness

                        var madd = _gameGlobal.DeepListMadnessTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);

                        if (madd != null)
                            if (madd.WhenToTrigger.Contains(game.RoundNo))
                            {
                                //trigger maddness

                                var curr = _gameGlobal.DeepListMadnessList.Find(x =>
                                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                                //just check
                                if (curr != null) _gameGlobal.DeepListMadnessList.Remove(curr);

                                _gameGlobal.DeepListMadnessList.Add(
                                    new DeepList.Madness(player.DiscordAccount.DiscordId, game.GameId, game.RoundNo));
                                curr = _gameGlobal.DeepListMadnessList.Find(x =>
                                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                                curr.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.GetIntelligence(),
                                    player.Character.GetStrength(), player.Character.GetSpeed(), player.Character.GetPsyche()));


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
                                        case int n when n >= 7 || n <= 16:
                                            statNumber = 4;
                                            break;
                                        case int n when n >= 17 || n <= 31:
                                            statNumber = 5;
                                            break;
                                        case int n when n >= 32 || n <= 51:
                                            statNumber = 6;
                                            break;
                                        case int n when n >= 52 || n <= 71:
                                            statNumber = 7;
                                            break;
                                        case int n when n >= 72 || n <= 86:
                                            statNumber = 8;
                                            break;
                                        case int n when n >= 87 || n <= 96:
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

                                player.Character.SetIntelligence(intel);  
                                player.Character.SetStrength(str);  
                                player.Character.SetSpeed(speed);  
                                player.Character.SetPsyche(pshy);  

                                await _phrase.DeepListMadnessPhrase.SendLog(player);
                                curr.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));
                            }
                        //end madness

                        break;

                    case "Sirinoks":

                        if (game.RoundNo == 10)
                        {
                            player.Character.SetIntelligence(10);
                            player.Character.SetStrength(10);
                            player.Character.SetSpeed(10);
                            player.Character.SetPsyche(11);

                            player.Status.AddBonusPoints(10);

                            var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                                x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                            if (siri != null)
                                for (var i = player.Status.PlaceAtLeaderBoard + 1; i < game.PlayersList.Count + 1; i++)
                                {
                                    var player2 = game.PlayersList[i - 1];
                                    if (siri.FriendList.Contains(player2.DiscordAccount.DiscordId))
                                        player.Status.AddBonusPoints(-1);
                                }


                            await _phrase.SirinoksFriendsPhrase.SendLog(player);
                        }

                        break;
                }

                var isSkip = _gameGlobal.AllSkipTriggeredWhen.Find(x =>
                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId &&
                    x.WhenToTrigger.Contains(game.RoundNo));

                if (isSkip != null)
                {
                    player.Status.IsSkip = true;
                    player.Status.IsBlock = false;
                    player.Status.IsAbleToTurn = false;
                    player.Status.IsReady = true;
                    player.Status.WhoToAttackThisTurn = 0;
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
                                    x.PlayerDiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                                if (tigr == null)
                                {
                                    _gameGlobal.TigrTwoBetterList.Add(new Sirinoks.FriendsClass(
                                        player.DiscordAccount.DiscordId, game.GameId, t.DiscordAccount.DiscordId));
                                    player.Status.AddRegularPoints();
                                    await _phrase.TigrTwoBetter.SendLog(player);
                                }
                                else
                                {
                                    if (!tigr.FriendList.Contains(t.DiscordAccount.DiscordId))
                                    {
                                        tigr.FriendList.Add(t.DiscordAccount.DiscordId);
                                        player.Status.AddRegularPoints();
                                        await _phrase.TigrTwoBetter.SendLog(player);
                                    }
                                }
                            }
                        }

                        //end Лучше с двумя, чем с адекватными:
                        break;

                    case "DeepList":

                        //madness
                        var madd = _gameGlobal.DeepListMadnessList.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId &&
                            x.RoundItTriggered == game.RoundNo);

                        if (madd != null)
                        {
                            var regularStats = madd.MadnessList.Find(x => x.Index == 1);
                            var madStats = madd.MadnessList.Find(x => x.Index == 2);


                            var intel = player.Character.GetIntelligence() - madStats.Intel;
                            var str = player.Character.GetStrength() - madStats.Str;
                            var speed = player.Character.GetSpeed() - madStats.Speed;
                            var psy = player.Character.GetPsyche() - madStats.Psyche;


                            player.Character.SetIntelligence(regularStats.Intel + intel);  
                            player.Character.SetStrength(regularStats.Str + str);  
                            player.Character.SetSpeed(regularStats.Speed + speed);  
                            player.Character.SetPsyche(regularStats.Psyche + psy);  
                            _gameGlobal.DeepListMadnessList.Remove(madd);
                        }

                        // end madness 
                        break;
                    case "Глеб":
                        //challenger
                        madd = _gameGlobal.GlebChallengerList.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId &&
                            x.RoundItTriggered == game.RoundNo);

                        if (madd != null)
                        {
                            var regularStats = madd.MadnessList.Find(x => x.Index == 1);
                            var madStats = madd.MadnessList.Find(x => x.Index == 2);


                            var intel = player.Character.GetIntelligence() - madStats.Intel;
                            var str = player.Character.GetStrength() - madStats.Str;
                            var speed = player.Character.GetSpeed() - madStats.Speed;
                            var psy = player.Character.GetPsyche() - madStats.Psyche;


                            player.Character.SetIntelligence(regularStats.Intel + intel);  
                            player.Character.SetStrength(regularStats.Str + str);  
                            player.Character.SetSpeed(regularStats.Speed + speed);  
                            player.Character.SetPsyche(regularStats.Psyche + psy);  
                            _gameGlobal.GlebChallengerList.Remove(madd);
                        }

                        break;
                    case "LeCrisp":
                        //impact

                        var leImpact = _gameGlobal.LeCrispImpact.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);

                        if (leImpact == null || !leImpact.IsTriggered)
                        {
                            player.Status.AddBonusPoints(1);
                            await _phrase.LeCrispImpactPhrase.SendLog(player);
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
                                var randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];
                                game.PreviousGameLogs +=
                                    $"Толя запизделся и спалил, что {randomPlayer.DiscordAccount.DiscordUserName} - {randomPlayer.Character.Name}\n";
                            }
                        }

                        var tolya = _gameGlobal.TolyaRammusTimes.Find(x =>
                            x.GameId == player.DiscordAccount.GameId &&
                            x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        switch (tolya.FriendList.Count)
                        {
                            case 1:
                                await _phrase.TolyaRammusPhrase.SendLog(player);
                                break;
                            case 2:
                                await _phrase.TolyaRammus2Phrase.SendLog(player);
                                break;
                            case 3:
                                await _phrase.TolyaRammus3Phrase.SendLog(player);
                                break;
                            case 4:
                                await _phrase.TolyaRammus4Phrase.SendLog(player);
                                break;
                            case 5:
                                await _phrase.TolyaRammus5Phrase.SendLog(player);
                                break;
                        }
                       
                        tolya.FriendList.Clear();
                        break;

                    case "Осьминожка":
                        var octo = _gameGlobal.OctopusTentaclesList.Find(x =>
                            x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        if (octo != null)
                            for (var i = 0; i < octo.UniqePlacesList.Count; i++)
                            {
                                var uni = octo.UniqePlacesList[i];

                                if (!uni.IsActivated)
                                {
                                    player.Status.AddRegularPoints();
                                    uni.IsActivated = true;
                                }
                            }

                        break;

                    case "Sirinoks":
                        //training

                        var siri = _gameGlobal.SirinoksTraining.Find(x =>
                            x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        if (siri != null && siri.Training.Count >= 1)
                        {
                            var stats = siri.Training.OrderByDescending(x => x.StatNumber).ToList()[0];

                            switch (stats.StatIndex)
                            {
                                case 1:
                                    player.Character.AddIntelligence(player.Status);
                                    break;
                                case 2:
                                    player.Character.AddStrength(player.Status);
                                    break;
                                case 3:
                                    player.Character.AddSpeed(player.Status);
                                    break;
                                case 4:
                                    player.Character.AddPsyche(player.Status);
                                    break;
                            }


                            siri.Training.Clear();
                        }

                        //end training
                        break;

                    case "Mitsuki":

                        //Дерзкая школота:
                        if (game.RoundNo == 1) await _phrase.MitsukiCheekyBriki.SendLog(player);

                        var randStat1 = _rand.Random(1, 4);
                        var randStat2 = _rand.Random(1, 4);
                        switch (randStat1)
                        {
                            case 1:
                                player.Character.AddIntelligence(player.Status, -1);
                                break;
                            case 2:
                                player.Character.AddStrength(player.Status, -1);
                                break;
                            case 3:
                                player.Character.AddSpeed(player.Status, -1);
                                break;
                            case 4:
                                player.Character.AddPsyche(player.Status, -1);
                                break;
                        }

                        switch (randStat2)
                        {
                            case 1:
                                player.Character.AddIntelligence(player.Status, -1);
                                break;
                            case 2:
                                player.Character.AddStrength(player.Status, -1);
                                break;
                            case 3:
                                player.Character.AddSpeed(player.Status, -1);
                                break;
                            case 4:
                                player.Character.AddPsyche(player.Status, -1);
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

        public async Task<int> HandleJewPassive(GameBridgeClass player, GameClass game)
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
                            leCrisp.Status.AddRegularPoints(0.5);
                            tolya.Status.AddRegularPoints(0.5);
                            if (!leCrisp.IsBot())
                            {
                                var mess =
                                    await leCrisp.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                        "МЫ жрём деньги!");
#pragma warning disable 4014
                                _help.DeleteMessOverTime(mess, 10);
#pragma warning restore 4014
                            }

                            if (!tolya.IsBot())
                            {
                                var mess =
                                    await tolya.Status.SocketMessageFromBot.Channel.SendMessageAsync("МЫ жрём деньги!");
#pragma warning disable 4014
                                _help.DeleteMessOverTime(mess, 10);
#pragma warning restore 4014
                            }


                            return 0;
                        }

                        return 1;
                    }

                if (leCrisp != null)
                    if (leCrisp.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn)
                    {
                        leCrisp.Status.AddRegularPoints();
                        await _phrase.LeCrispJewPhrase.SendLog(leCrisp);
                        return 0;
                    }

                if (tolya != null)
                    if (tolya.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn)
                    {
                        tolya.Status.AddRegularPoints();
                        await _phrase.TolyaJewPhrase.SendLog(tolya);
                        return 0;
                    }
            }

            return 1;
        }

        /*
        public async Task<string> HandleBlock(GameBridgeClass octopusPlayer, GameBridgeClass player2, GameClass game)
        {
            switch (player2.Character.Name)
            {
             case "Толя":
                 break;
            }
        }
        */
        public async Task HandleEveryAttackOnHimAfterCalculations(GameBridgeClass playerIamAttacking,
            GameBridgeClass player, GameClass game)
        {
            var characterName = playerIamAttacking.Character.Name;


            switch (characterName)
            {
                case "HardKitty":
                    //Muted passive
                    if (playerIamAttacking.Status.IsLostLastTime != 0)
                    {
                        var hardKitty = _gameGlobal.HardKittyMute.Find(x =>
                            x.PlayerDiscordId == playerIamAttacking.DiscordAccount.DiscordId &&
                            x.GameId == game.GameId);
                        if (hardKitty == null)
                        {
                            _gameGlobal.HardKittyMute.Add(new HardKitty.MuteClass(
                                playerIamAttacking.DiscordAccount.DiscordId, game.GameId,
                                player.DiscordAccount.DiscordId));
                            player.Status.AddRegularPoints();
                            await _phrase.HardKittyMutedPhrase.SendLog(playerIamAttacking);
                        }
                        else
                        {
                            if (!hardKitty.UniquePlayers.Contains(player.DiscordAccount.DiscordId))
                            {
                                hardKitty.UniquePlayers.Add(player.DiscordAccount.DiscordId);
                                player.Status.AddRegularPoints();
                                await _phrase.HardKittyMutedPhrase.SendLog(playerIamAttacking);
                            }
                        }
                    }

                    //Muted passive end
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleEveryAttackFromMeAfterCalculations(GameBridgeClass player,
            GameBridgeClass playerIamAttacking, GameClass game)
        {
            var characterName = player.Character.Name;


            switch (characterName)
            {
                case "HardKitty":
                    //Doebatsya
                    var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                        x.GameId == player.DiscordAccount.GameId &&
                        x.PlayerDiscordId == player.DiscordAccount.DiscordId);
                    //can be null

                    if (player.Status.IsLostLastTime != 0)
                    {
                        if (hardKitty == null)
                        {
                            _gameGlobal.HardKittyDoebatsya.Add(new HardKitty.DoebatsyaClass(
                                player.DiscordAccount.DiscordId, player.DiscordAccount.GameId,
                                player.Status.IsLostLastTime));
                        }
                        else
                        {
                            var exists = hardKitty.LostSeries.Find(x => x.EnemyId == player.Status.IsLostLastTime);
                            if (exists == null)
                                hardKitty.LostSeries.Add(new HardKitty.DoebatsyaSubClass(player.Status.IsLostLastTime));
                            else
                                exists.Series++;
                        }

                        return;
                    }

                    var wonPlayer = hardKitty?.LostSeries.Find(x => x.EnemyId == player.Status.IsWonLastTime);
                    if (wonPlayer != null)
                    {
                        player.Status.AddRegularPoints(wonPlayer.Series);
                        if (wonPlayer.Series >= 2)
                        {
                            var player2 = game.PlayersList.Find(x =>
                                x.DiscordAccount.DiscordId == player.Status.IsWonLastTime);
                            player2.Character.AddPsyche(player2.Status, -1);
                            player2.MinusPsycheLog(game);
                        }

                        wonPlayer.Series = 0;
                        await _phrase.HardKittyDoebatsyaPhrase.SendLog(player);
                    }

                    // end Doebatsya
                    break;

                case "AWDKA":
                    //Произошел троллинг:


                    if (player.Status.IsWonLastTime != 0)
                    {
                        var awdka = _gameGlobal.AwdkaTrollingList.Find(x =>
                            x.GameId == player.DiscordAccount.GameId &&
                            x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        var player2 =
                            game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsWonLastTime);

                        if (player2.Status.PlaceAtLeaderBoard == 1)
                            if (awdka.Cooldown <= -1)
                            {
                                var pointsToGet = player2.Status.GetScore() / 4;
                                if (pointsToGet < 1) pointsToGet = 1;
                                player.Status.AddBonusPoints(pointsToGet);
                                awdka.Cooldown = 2;
                                await _phrase.AwdkaTrolling.SendLog(player);
                            }
                    }

                    //end Произошел троллинг:
                    break;

                case "Осьминожка":
                    if (player.Status.IsLostLastTime != 0)
                    {
                        var octo = _gameGlobal.OctopusInvulnerabilityList.Find(x =>
                            x.GameId == player.DiscordAccount.GameId &&
                            x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        if (octo == null)
                            _gameGlobal.OctopusInvulnerabilityList.Add(
                                new Octopus.InvulnerabilityClass(player.DiscordAccount.DiscordId, game.GameId));
                        else
                            octo.Count++;
                    }

                    break;

                case "Даркси":
                    var darscsi = _gameGlobal.DarksciLuckyList.Find(x =>
                        x.GameId == player.DiscordAccount.GameId &&
                        x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                    if (darscsi == null)
                    {
                        _gameGlobal.DarksciLuckyList.Add(new Darksci.LuckyClass(player.DiscordAccount.DiscordId,
                            game.GameId, playerIamAttacking.DiscordAccount.DiscordId));
                    }
                    else
                    {
                        if (!darscsi.TouchedPlayers.Contains(playerIamAttacking.DiscordAccount.DiscordId))
                            darscsi.TouchedPlayers.Add(playerIamAttacking.DiscordAccount.DiscordId);

                        if (darscsi.TouchedPlayers.Count == game.PlayersList.Count - 1)
                        {
                            player.Status.AddBonusPoints(player.Status.GetScore() * 3);

                            player.Character.AddPsyche(player.Status, 2);
                            darscsi.TouchedPlayers.Clear();
                            await _phrase.DarksciLucky.SendLog(player);
                        }
                    }

                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleNextRoundAfterSorting(GameClass game)
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
                                x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                            if (shark == null)
                            {
                                _gameGlobal.SharkJawsLeader.Add(new Sirinoks.FriendsClass(
                                    player.DiscordAccount.DiscordId, game.GameId,
                                    (ulong) player.Status.PlaceAtLeaderBoard));
                                player.Character.AddSpeed(player.Status );
                            }
                            else
                            {
                                if (!shark.FriendList.Contains((ulong) player.Status.PlaceAtLeaderBoard))
                                {
                                    shark.FriendList.Add((ulong) player.Status.PlaceAtLeaderBoard);
                                    player.Character.AddSpeed(player.Status);
                                }
                            }

                        }

                        //end Челюсти:
                        break;

                    case "Тигр":
                        //Тигр топ, а ты холоп: 
                        if (player.Status.PlaceAtLeaderBoard == 1 && game.RoundNo > 1)
                        {
                            player.Character.AddPsyche(player.Status);
                            await _phrase.TigrTop.SendLog(player);
                        }

                        //end Тигр топ, а ты холоп: 
                        break;

                    case "Mitsuki":
                        //Много выебывается:
                        if (player.Status.PlaceAtLeaderBoard == 1 && game.RoundNo > 1)
                        {
                            player.Status.AddRegularPoints();
                            await _phrase.MitsukiTooMuchFucking.SendLog(player);
                        }
                        //end Много выебывается:

                        //Запах мусора:

                        if (game.RoundNo > 10)
                        {
                            var mitsuki = _gameGlobal.MitsukiGarbageList.Find(x =>
                                x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);
                            if (mitsuki != null)
                            {
                                var count = 0;
                                foreach (var t in mitsuki.Training)
                                {
                                    var player2 = game.PlayersList.Find(x =>
                                        x.DiscordAccount.DiscordId == t);
                                    if (player2 != null)
                                    {
                                        player2.Status.AddBonusPoints(-2);
                                        //  player.Status.AddBonusPoints(2);
                                        await _phrase.MitsukiGarbageSmell.SendLog(player2);
                                        count++;
                                    }
                                }

                                game.PreviousGameLogs += $"Mitsuki отнял в общей сумме {count*2} очков.";
                            }
                        }

                        //end Запах мусора:
                        break;

                    case "Осьминожка":
                        var octo = _gameGlobal.OctopusTentaclesList.Find(x =>
                            x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        if (octo == null)
                        {
                            _gameGlobal.OctopusTentaclesList.Add(new Octopus.TentaclesClass(
                                player.DiscordAccount.DiscordId, game.GameId, player.Status.PlaceAtLeaderBoard));
                        }
                        else
                        {
                            if (octo.UniqePlacesList.All(x => x.LeaderboardPlace != player.Status.PlaceAtLeaderBoard))
                                octo.UniqePlacesList.Add(
                                    new Octopus.TentaclesSubClass(player.Status.PlaceAtLeaderBoard));
                        }

                        break;
                    case "AWDKA":
                        var awdka = _gameGlobal.AwdkaTrollingList.Find(x =>
                            x.GameId == player.DiscordAccount.GameId &&
                            x.PlayerDiscordId == player.DiscordAccount.DiscordId);
                        if (awdka != null)
                        {
                            awdka.Cooldown--;
                            if (awdka.Cooldown <= -1) await _phrase.AwdkaTrollingReady.SendLog(player);
                        }

                        break;

                    case "Толя":
                        
                        var tolya = _gameGlobal.TolyaCount.Find(x =>
                            x.GameId == player.DiscordAccount.GameId &&
                            x.PlayerDiscordId == player.DiscordAccount.DiscordId);
                        if (tolya != null)
                        {
                            tolya.Cooldown--;
                            if (tolya.Cooldown <= 0) await _phrase.TolyaCountReadyPhrase.SendLog(player);
                        }
                        break;
                }
            }


            if (game.RoundNo > 10)
            {
                //TODO: implement end of the game, after turn 10.


                //handle Octo
                var octopusInk = _gameGlobal.OctopusInkList.Find(x => x.GameId == game.GameId);
                var octopusInv = _gameGlobal.OctopusInvulnerabilityList.Find(x => x.GameId == game.GameId);

                if (octopusInk != null)
                    foreach (var t in octopusInk.RealScoreList)
                    {
                        var pl = game.PlayersList.Find(x => x.DiscordAccount.DiscordId == t.PlayerId);
                        pl?.Status.AddBonusPoints(t.RealScore);
                    }

                if (octopusInv != null)
                {
                    var octoPlayer =
                        game.PlayersList.Find(x => x.DiscordAccount.DiscordId == octopusInv.PlayerDiscordId);
                    octoPlayer.Status.AddBonusPoints(octopusInv.Count);
                }

                //end handle Octo

                //sort
                game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
                for (var i = 0; i < game.PlayersList.Count; i++)
                {
                    game.PlayersList[i].Status.PlaceAtLeaderBoard = i + 1;
                }
                //end sorting


            }
        }

        public bool HandleOctopus(GameBridgeClass octopusPlayer, GameBridgeClass playerAttackedOctopus, GameClass game)
        {
            if (octopusPlayer.Character.Name != "Осьминожка") return true;


            game.GameLogs += $" ⟶ победил **{playerAttackedOctopus.DiscordAccount.DiscordUserName}**\n";
            game.PreviousGameLogs += $" ⟶ победил **{playerAttackedOctopus.DiscordAccount.DiscordUserName}**\n";

            //еврей
            var point = HandleJewPassive(playerAttackedOctopus, game);
            //end еврей

            playerAttackedOctopus.Status.AddRegularPoints(point.Result);

            playerAttackedOctopus.Status.WonTimes++;
            playerAttackedOctopus.Character.Justice.IsWonThisRound = true;

            octopusPlayer.Character.Justice.AddJusticeForNextRound();

            playerAttackedOctopus.Status.IsWonLastTime = octopusPlayer.DiscordAccount.DiscordId;
            octopusPlayer.Status.IsLostLastTime = playerAttackedOctopus.DiscordAccount.DiscordId;


            var octo = _gameGlobal.OctopusInkList.Find(x =>
                x.PlayerDiscordId == octopusPlayer.DiscordAccount.DiscordId &&
                x.GameId == game.GameId);

            if (octo == null)
            {
                _gameGlobal.OctopusInkList.Add(new Octopus.InkClass(octopusPlayer.DiscordAccount.DiscordId, game,
                    playerAttackedOctopus.DiscordAccount.DiscordId));
            }
            else
            {
                var enemyRealScore =
                    octo.RealScoreList.Find(x => x.PlayerId == playerAttackedOctopus.DiscordAccount.DiscordId);
                var octoRealScore = octo.RealScoreList.Find(x => x.PlayerId == octopusPlayer.DiscordAccount.DiscordId);

                if (enemyRealScore == null)
                {
                    octo.RealScoreList.Add(new Octopus.InkSubClass(playerAttackedOctopus.DiscordAccount.DiscordId,
                        game.RoundNo, -1));
                    octoRealScore.AddRealScore(game.RoundNo);
                }
                else
                {
                    enemyRealScore.AddRealScore(game.RoundNo, -1);
                    octoRealScore.AddRealScore(game.RoundNo);
                }
            }

            octopusPlayer.Status.AddRegularPoints();
            playerAttackedOctopus.Status.AddRegularPoints(-1);


            return false;
        }


        public async Task HandleCharacterWithKnownEnemyBeforeCalculations(GameBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    await _deepList.HandleDeepListTactics(player);
                    break;
            }


            await Task.CompletedTask;
        }
    }
}