using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.BotFramework;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class CharacterPassives : IServiceSingleton
    {
        //helpers
        private readonly HelperFunctions _help;
        private readonly SecureRandom _rand;
        private readonly InGameGlobal _gameGlobal;
        private readonly LoginFromConsole _log;
        private readonly GameUpdateMess _gameUpdateMess;
        private readonly CharactersUniquePhrase _phrase;

        private readonly Global _global;
        //end helpers

        //chars
        private readonly Awdka _awdka;
        private readonly DeepList _deepList;
        private readonly DeepList2 _deepList2;
        private readonly Gleb _gleb;
        private readonly HardKitty _hardKitty;
        private readonly LeCrisp _leCrisp;
        private readonly Mitsuki _mitsuki;
        private readonly Mylorik _mylorik;
        private readonly Octopus _octopus;
        private readonly Shark _shark;
        private readonly Sirinoks _sirinoks;
        private readonly Tigr _tigr;
        private readonly Tolya _tolya;

        private readonly Darksci _darksci;
        //end chars

        public CharacterPassives(SecureRandom rand, HelperFunctions help, Awdka awdka, DeepList deepList,
            DeepList2 deepList2, Gleb gleb, HardKitty hardKitty, Mitsuki mitsuki, LeCrisp leCrisp, Mylorik mylorik,
            Octopus octopus, Shark shark, Sirinoks sirinoks, Tigr tigr, Tolya tolya, InGameGlobal gameGlobal,
            Darksci darksci, CharactersUniquePhrase phrase, LoginFromConsole log, GameUpdateMess gameUpdateMess, Global global)
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
            _global = global;
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
                case "Глеб":
                    var rand = _rand.Random(1, 8);
                    if (rand == 1)
                    {
                        var acc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.DiscordId == playerIamAttacking.DiscordAccount.DiscordId &&
                            playerIamAttacking.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                                return;

                        if (!playerIamAttacking.Status.IsSkip)
                        {
                            playerIamAttacking.Status.IsSkip = true;
                            _gameGlobal.GlebSkipList.Add(
                                new Gleb.GlebSkipClass(playerIamAttacking.DiscordAccount.DiscordId, game.GameId));
                        }
                    }

                    break;
                case "LeCrisp":
                    //гребанные ассасисны
                    if (playerAttackFrom.Character.Strength - playerIamAttacking.Character.Strength >= 2)
                    {
                        playerIamAttacking.Status.IsAbleToWin = false;
                        await _phrase.LeCrispAssassinsPhrase.SendLog(playerIamAttacking);
                    }
                    //end гребанные ассасисны

                    //Импакт: 
                    var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                        x.DiscordId == playerIamAttacking.DiscordAccount.DiscordId && x.GameId == game.GameId);

                    if (lePuska == null)
                        _gameGlobal.LeCrispImpact.Add(
                            new LeCrisp.LeCrispImpactClass(playerIamAttacking.DiscordAccount.DiscordId, game.GameId,
                                game.RoundNo));
                    // end Импакт: 
                    break;

                case "Толя":
                    if (playerIamAttacking.Status.IsBlock)
                    {
                        playerIamAttacking.Status.IsBlock = false;
                        playerAttackFrom.Status.IsAbleToWin = false;

                        await _phrase.TolyaRammusPhrase.SendLog(playerIamAttacking);
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
                        _gameGlobal.MitsukiGarbageList.Add(new Mitsuki.GarbageClass(playerIamAttacking.DiscordAccount.DiscordId, game.GameId, playerAttackFrom.DiscordAccount.DiscordId));
                    }
                    else
                    {
                        if (!mitsuki.Training.Contains(playerAttackFrom.DiscordAccount.DiscordId))
                        {
                            mitsuki.Training.Add(playerAttackFrom.DiscordAccount.DiscordId);
                        }
                    }
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleEveryAttackFromMe(GameBridgeClass player1, GameBridgeClass playerIamAttacking, GameClass game)
        {
            var characterName = player1.Character.Name;

            switch (characterName)
            {
                case "Глеб":
                    // Я за чаем:
                    var rand = _rand.Random(1, 10);
                    if (rand == 1)
                    {
                        _gameGlobal.AllSkipTriggeredWhen.Add(new WhenToTriggerClass(player1.Status.WhoToAttackThisTurn,
                            game.GameId,
                            game.RoundNo + 1));
                        player1.Status.AddRegularPoints();
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
                    {
                        playerIamAttacking.Status.IsBlock = false;
                    }
                    //Friends end
                    break;

                case "AWDKA":

                    //Научите играть
                    var awdka = _gameGlobal.AwdkaTeachToPlay.Find(x =>
                        x.GameId == game.GameId && x.PlayerDiscordId == player1.DiscordAccount.DiscordId);

                    var player2Stats = new List<Sirinoks.TrainingSubClass>
                    {
                        new Sirinoks.TrainingSubClass(1, playerIamAttacking.Character.Intelligence),
                        new Sirinoks.TrainingSubClass(2, playerIamAttacking.Character.Strength),
                        new Sirinoks.TrainingSubClass(3, playerIamAttacking.Character.Speed),
                        new Sirinoks.TrainingSubClass(4, playerIamAttacking.Character.Psyche)
                    };
                    var sup = player2Stats.OrderByDescending(x => x.StatNumber).ToList()[0];
                    if (awdka == null)
                    {
         
                    _gameGlobal.AwdkaTeachToPlay.Add(new Sirinoks.TrainingClass(player1.DiscordAccount.DiscordId, game.GameId, sup.StatIndex, sup.StatNumber));
                    }
                    else
                    {
                        awdka.Training.Add(new Sirinoks.TrainingSubClass(sup.StatIndex, sup.StatNumber));
                    }

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
            var characterName = player.Character.Name;

            //tolya count

            if (player.Status.IsWonLastTime != 0 && player.Character.Name != "Толя" &&
                game.PlayersList.Any(x => x.Character.Name == "Толя"))
            {
                var tolya = _gameGlobal.TolyaCount.Find(x =>
                    x.PlayerDiscordId == player.DiscordAccount.DiscordId && x.GameId == player.DiscordAccount.GameId);

                if (tolya != null)
                    if (player.Status.IsWonLastTime == tolya.WhoToLostLastTime)
                    {
                        var tolyaAcc = game.PlayersList.Find(x =>
                            x.DiscordAccount.DiscordId == player.DiscordAccount.DiscordId &&
                            x.DiscordAccount.GameId == player.DiscordAccount.GameId);
                        tolyaAcc.Status.AddRegularPoints();
                        await _phrase.TolyaCountPhrase.SendLog(player);
                    }
            }

            //tolya count end

            switch (characterName)
            {
                case "DeepList":
                    _deepList.HandleDeepListAfter(player, game);
                    break;
                case "mylorik":
                    _mylorik.HandleMylorikAfter(player);
                    break;
                case "Глеб":
                    _gleb.HandleGlebAfter(player);
                    break;
                case "LeCrisp":
                    _leCrisp.HandleLeCrispAfter(player);
                    break;
                case "Толя":
                    _tolya.HandleTolyaAfter(player);
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
                 await   _darksci.HandleDarksiAfter(player, game);
                    break;
                case "Тигр":
                    _tigr.HandleTigrAfter(player);
                    break;
                case "Братишка":
                    _shark.HandleSharkAfter(player);
                    break;
                case "????":
                    _deepList2.HandleDeepList2After(player);
                    break;
            }


            if (player.Status.WhoToAttackThisTurn == 0 && player.Status.IsBlock == false)
            {
                player.Status.IsBlock = true;
            }
            await Task.CompletedTask;
        }

        public void CalculatePassiveChances(GameClass game)
        {
            foreach (var player in game.PlayersList)
            {
                var characterName = player.Character.Name;
                WhenToTriggerClass when;
                switch (characterName)
                {
                    case "DeepList":

                        when = _gameGlobal.GetWhenToTrigger(player, true, 7, 1);
                        _gameGlobal.DeepListSupermindTriggeredWhen.Add(when);
                        when = _gameGlobal.GetWhenToTrigger(player, false, 10, 2);
                        _gameGlobal.DeepListMadnessTriggeredWhen.Add(when);

                        break;
                    case "mylorik":
                        when = _gameGlobal.GetWhenToTrigger(player, false, 10, 2);
                        _gameGlobal.MylorikBooleTriggeredWhen.Add(when);
                        break;
                    case "AWDKA":
                        when = _gameGlobal.GetWhenToTrigger(player, false, 10, 1);
                        _gameGlobal.AwdkaAfkTriggeredWhen.Add(when);
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
                        int[] temp = {-1, -1, -1, -1, -1};
                        if (when.WhenToTrigger.Count >= 1)
                            temp[0] = when.WhenToTrigger[0];
                        if (when.WhenToTrigger.Count >= 2)
                            temp[1] = when.WhenToTrigger[1];
                        if (when.WhenToTrigger.Count >= 3)
                            temp[2] = when.WhenToTrigger[2];
                        if (when.WhenToTrigger.Count >= 4)
                            temp[3] = when.WhenToTrigger[3];
                        if (when.WhenToTrigger.Count >= 5)
                            temp[4] = when.WhenToTrigger[4];
                        do
                        {
                            when = _gameGlobal.GetWhenToTrigger(player, true, 12, 3, true);
                        } while (when.WhenToTrigger.All(x => x == temp[0] || x == temp[1] || x == temp[2]));

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
                                if (!player.IsBot())
                                {
                                    var mess = await player.Status.SocketMessageFromBot.Channel
                                        .SendMessageAsync("Ты буль.");
#pragma warning disable 4014
                                    _help.DeleteMessOverTime(mess, 15);
#pragma warning restore 4014
                                }

                            }

                        break;

                    case"Даркси":
                        //Да всё нахуй эту игру:
                        if (player.Character.Psyche <= 0)
                        {
                            player.Status.IsSkip = true;
                            player.Status.IsBlock = false;
                            player.Status.IsAbleToTurn = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = 0;
                        await  _phrase.DarksciFuckThisGame.SendLog(player);

                        if (game.RoundNo >= 9)
                        {
                            game.GameLogs += $"**{player.DiscordAccount.DiscordUserName}:** Нахуй эту игру...";
                        }
                        }
                        //end Да всё нахуй эту игру:

                        //Дизмораль
                        if (game.RoundNo == 9)
                        {
                            player.Character.Psyche -= 4;
                            await  _phrase.DarksciDysmoral.SendLog(player);
                        }
                        //end Дизмораль
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

                             await   _phrase.MitsukiSchoolboy.SendLog(player);
                            }

                        break;
                    case "AWDKA":

                        //АФКА
                        
                        var awdkaaa = _gameGlobal.AwdkaAfkTriggeredWhen.Find(x =>
                            x.GameId == player.DiscordAccount.GameId && x.DiscordId == player.DiscordAccount.DiscordId);

                        if (awdkaaa != null)
                        {
                            if (awdkaaa.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = false;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = 0;

                                await   _phrase.AwdkaAfk.SendLog(player);
                            }
                        }
                        //end АФКА

                        //Я пытаюсь!:
                        var awdkaa = _gameGlobal.AwdkaTryingList.Find(x =>
                            x.GameId == player.DiscordAccount.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        if(awdkaa != null)
                        {
                         
                            foreach (var enemy in  awdkaa.TryingList)
                            {
                                if (enemy != null)
                                {
                                    if (enemy.Times >= 2 && enemy.IsUnique == false)
                                    {
                                        player.Status.MoveListPage += 4;
                                      await  _gameUpdateMess.UpdateMessage(player);
                                        enemy.IsUnique = true;
                                        await  _phrase.AwdkaTrying.SendLog(player);
                                    }
                                }
                            }
 
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

                            var intel = player.Character.Intelligence - madStats.Intel;
                            var str = player.Character.Strength - madStats.Str;
                            var speed = player.Character.Speed - madStats.Speed;
                            var psy = player.Character.Psyche - madStats.Psyche;

                            player.Character.Intelligence = regularStats.Intel + intel;
                            player.Character.Strength = regularStats.Str + str;
                            player.Character.Speed = regularStats.Speed + speed;
                            player.Character.Psyche = regularStats.Psyche + psy;

                            _gameGlobal.AwdkaTeachToPlayTempStats.Remove(awdkaTempStats);
                      
                        }
                        //end remove stats

                        //if there is no one have been attacked from awdka
                        if (awdka == null)
                        {
                            return;
                        }
                        //end if there..

                        //crazy shit
                        _gameGlobal.AwdkaTeachToPlayTempStats.Add(new DeepList.Madness(player.DiscordAccount.DiscordId,
                            game.GameId, game.RoundNo));
                        awdkaTempStats = _gameGlobal.AwdkaTeachToPlayTempStats.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                        awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.Intelligence,
                            player.Character.Strength, player.Character.Speed, player.Character.Psyche));
                        //end crazy shit

                            //find out  the biggest stat
                        var bestSkill = awdka.Training.OrderByDescending(x => x.StatNumber).ToList()[0];

                        var intel1 = player.Character.Intelligence;
                        var str1 = player.Character.Strength;
                        var speed1 = player.Character.Speed;
                        var pshy1 = player.Character.Psyche;

                        switch (bestSkill.StatIndex)
                        {
                            case 1 :
                                intel1 = bestSkill.StatNumber;
                                break;
                            case 2 :
                                str1 = bestSkill.StatNumber;
                                break;
                            case 3 :
                                speed1 = bestSkill.StatNumber;
                                break;
                            case 4 :
                                pshy1 = bestSkill.StatNumber;
                                break;
                        }

                        player.Character.Intelligence = intel1;
                        player.Character.Strength = str1;
                        player.Character.Speed = speed1;
                        player.Character.Psyche = pshy1;
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

                                if (!player.IsBot())
                                {
                                    var mess =
                                        await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("Zzz");
#pragma warning disable 4014
                                    _help.DeleteMessOverTime(mess, 15);
#pragma warning restore 4014
                                }

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
                                gleb.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.Intelligence,
                                    player.Character.Strength, player.Character.Speed, player.Character.Psyche));

                                //  var randomNumber =  _rand.Random(1, 100);

                                var intel = player.Character.Intelligence >= 10 ? 10 : 9;
                                var str = player.Character.Strength >= 10 ? 10 : 9;
                                var speed = player.Character.Speed >= 10 ? 10 : 9;
                                var pshy = player.Character.Psyche >= 10 ? 10 : 9;


                                player.Character.Intelligence = intel;
                                player.Character.Strength = str;
                                player.Character.Speed = speed;
                                player.Character.Psyche = pshy;


                                gleb.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));

                                await _phrase.GlebChallengerPhrase.SendLog(player);
                            }

                        break;
                    case "DeepList":

                        //Сверхразум
                        var currentDeepList = _gameGlobal.DeepListSupermindTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && game.GameId == x.GameId);

                        if (currentDeepList != null)
                        {
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
                                } while (randPlayer.DiscordAccount.DiscordId != player.DiscordAccount.DiscordId);

                                var check = _gameGlobal.DeepListSupermindKnown.Find(x =>
                                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                                if (check == null)
                                    _gameGlobal.DeepListSupermindKnown.Add(new DeepList.SuperMindKnown(
                                        player.DiscordAccount.DiscordId, game.GameId,
                                        randPlayer.DiscordAccount.DiscordId));
                                else
                                    check.KnownPlayers.Add(randPlayer.DiscordAccount.DiscordId);

                                await _phrase.DeepListSuperMindPhrase.SendLog(player);
                            }
                        }
                        else
                        {
                            Console.WriteLine("DEEP LIST SUPERMIND PASSIVE ERRORR!!!!!!!!!!!!!!!!!");
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
                                curr.MadnessList.Add(new DeepList.MadnessSub(1, player.Character.Intelligence,
                                    player.Character.Strength, player.Character.Speed, player.Character.Psyche));


                                //  var randomNumber =  _rand.Random(1, 100);
                                var intel = 0;
                                var str = 0;
                                var speed = 0;
                                var pshy = 0;


                                curr.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));
                            }
                        //end madness

                        break;

                    case "Sirinoks":

                        if (game.RoundNo == 10)
                        {
                            player.Character.Intelligence = 10;
                            player.Character.Strength = 10;
                            player.Character.Speed = 10;
                            player.Character.Psyche = 10;

                            player.Status.AddBonusPoints(10);

                            var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                                x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                            if (siri != null)
                            {
                                for (var i = player.Status.PlaceAtLeaderBoard+1; i < 7; i++)
                                {
                                    var player2 = game.PlayersList[i-1];
                                    if (siri.FriendList.Contains(player2.DiscordAccount.DiscordId))
                                    {
                                        player.Status.AddBonusPoints(-1);
                                    }
                                }
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
                    if (!player.IsBot())
                    {
                        var mess = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                            "Тебя заставили пропустить этот ход");
#pragma warning disable 4014
                        _help.DeleteMessOverTime(mess, 15);
#pragma warning restore 4014
                    }

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
                    case "DeepList":

                        //madness
                        var madd = _gameGlobal.DeepListMadnessList.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId &&
                            x.RoundItTriggered == game.RoundNo);

                        if (madd != null)
                        {
                            var regularStats = madd.MadnessList.Find(x => x.Index == 1);
                            var madStats = madd.MadnessList.Find(x => x.Index == 2);


                            var intel = player.Character.Intelligence - madStats.Intel;
                            var str = player.Character.Strength - madStats.Str;
                            var speed = player.Character.Speed - madStats.Speed;
                            var psy = player.Character.Psyche - madStats.Psyche;


                            player.Character.Intelligence = regularStats.Intel + intel;
                            player.Character.Strength = regularStats.Str + str;
                            player.Character.Speed = regularStats.Speed + speed;
                            player.Character.Psyche = regularStats.Psyche + psy;
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


                            var intel = player.Character.Intelligence - madStats.Intel;
                            var str = player.Character.Strength - madStats.Str;
                            var speed = player.Character.Speed - madStats.Speed;
                            var psy = player.Character.Psyche - madStats.Psyche;


                            player.Character.Intelligence = regularStats.Intel + intel;
                            player.Character.Strength = regularStats.Str + str;
                            player.Character.Speed = regularStats.Speed + speed;
                            player.Character.Psyche = regularStats.Psyche + psy;
                            _gameGlobal.GlebChallengerList.Remove(madd);
                        }

                        break;
                    case "LeCrisp":
                        //impact

                        var leImpact = _gameGlobal.LeCrispImpact.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);
                        if (leImpact != null)
                            _gameGlobal.LeCrispImpact.Remove(leImpact);
                        else
                            player.Status.AddBonusPoints(1);

                        //end impact

                        break;

                    case "Толя":
                        if (game.RoundNo >= 3 && game.RoundNo <= 6)
                        {
                            var randNum = _rand.Random(1, 5);
                            if (randNum == 1)
                            {
                                var randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Capacity - 1)];
                                game.GameLogs +=
                                    $"Толя запизделся и спалил, что {randomPlayer.DiscordAccount.DiscordUserName} - {randomPlayer.Character.Name}";
                            }
                        }

                        break;

                    case "Осьминожка":
                        var octo = _gameGlobal.OctopusTentaclesList.Find(x =>
                            x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        if (octo != null)
                        {
                            for (var i = 0; i < octo.UniqePlacesList.Count; i++)
                            {
                                var uni = octo.UniqePlacesList[i];

                                if (!uni.IsActivated)
                                {
                                    player.Status.AddRegularPoints();
                                    uni.IsActivated = true;
                                }
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
                                    player.Character.Intelligence++;
                                    break;
                                case 2:
                                    player.Character.Strength++;
                                    break;
                                case 3:
                                    player.Character.Speed++;
                                    break;
                                case 4:
                                    player.Character.Psyche++;
                                    break;
                            }


                            siri.Training.Clear();
                        }

                        //end training
                        break;

                    case "Mitsuki":

                        //Дерзкая школота:
                        if (game.RoundNo == 1)
                        {
                          await  _phrase.MitsukiCheekyBriki.SendLog(player);
                        }

                        var randStat1 = _rand.Random(1, 4);
                        var randStat2 = _rand.Random(1, 4);
                        switch (randStat1)
                        {
                            case 1:
                                player.Character.Intelligence--;
                                break;
                            case 2:
                                player.Character.Strength--;
                                break;
                            case 3:
                                player.Character.Speed--;
                                break;
                            case 4:
                                player.Character.Psyche--;
                                break;
                        }
                        switch (randStat2)
                        {
                            case 1:
                                player.Character.Intelligence--;
                                break;
                            case 2:
                                player.Character.Strength--;
                                break;
                            case 3:
                                player.Character.Speed--;
                                break;
                            case 4:
                                player.Character.Psyche--;
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
                                    await leCrisp.Status.SocketMessageFromBot.Channel.SendMessageAsync("МЫ жрём деньги!");
#pragma warning disable 4014
                                _help.DeleteMessOverTime(mess, 10);
#pragma warning restore 4014
                            }
                            if (!tolya.IsBot())
                            {
                              var  mess =
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
                        await _phrase.LeCrispJewPhrase.SendLog(player);
                        return 0;
                    }

                if (tolya != null)
                    if (tolya.Status.WhoToAttackThisTurn == player.Status.WhoToAttackThisTurn)
                    {
                        tolya.Status.AddRegularPoints();
                        await _phrase.TolyaJewPhrase.SendLog(player);
                        return 0;
                    }
            }

            return 1;
        }

        /*
        public async Task<string> HandleBlock(GameBridgeClass octopusPlayer, GameBridgeClass playerIamAttacking, GameClass game)
        {
            switch (playerIamAttacking.Character.Name)
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
                            await _phrase.HardKittyMutedPhrase.SendLog(player);
                        }
                        else
                        {
                            if (!hardKitty.UniquePlayers.Contains(player.DiscordAccount.DiscordId))
                            {
                                hardKitty.UniquePlayers.Add(player.DiscordAccount.DiscordId);
                                player.Status.AddRegularPoints();
                                await _phrase.HardKittyMutedPhrase.SendLog(player);
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
                            player2.Character.Psyche--;
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
                        var player2 =
                            game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsWonLastTime);

                        if (player2.Status.PlaceAtLeaderBoard == 1)
                        {
                            int pointsToGet = player2.Status.GetScore() / 4;
                            if (pointsToGet < 1)
                            {
                                pointsToGet = 1;
                            }

                            player.Status.AddBonusPoints(pointsToGet);
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
                        {
                            _gameGlobal.OctopusInvulnerabilityList.Add(new Octopus.InvulnerabilityClass(player.DiscordAccount.DiscordId, game.GameId));
                        }
                        else
                        {
                            octo.Count++;
                        }
                    }
                    break;

                case "Даркси":
                    var darscsi = _gameGlobal.DarksciLuckyList.Find(x =>
                        x.GameId == player.DiscordAccount.GameId &&
                        x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                    if (darscsi == null)
                    {
                        _gameGlobal.DarksciLuckyList.Add(new Darksci.LuckyClass(player.DiscordAccount.DiscordId, game.GameId, playerIamAttacking.DiscordAccount.DiscordId));
                    }
                    else
                    {

                        if (!darscsi.TouchedPlayers.Contains(playerIamAttacking.DiscordAccount.DiscordId))
                        {
                            darscsi.TouchedPlayers.Add(playerIamAttacking.DiscordAccount.DiscordId);
                        }

                        if (darscsi.TouchedPlayers.Count == game.PlayersList.Count - 1)
                        {
                          player.Status.AddBonusPoints(player.Status.GetScore()*3);
                          player.Character.Psyche += 2;
                          if (player.Character.Psyche > 10)
                          {
                              player.Character.Psyche = 10;
                          }
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
                    case "Mitsuki":
                        //Много выебывается:
                        if (player.Status.PlaceAtLeaderBoard == 1)
                        {
                            player.Status.AddRegularPoints();
                         await   _phrase.MitsukiTooMuchFucking.SendLog(player);
                        }
                        //end Много выебывается:

                        //Запах мусора:

                        if (game.RoundNo > 10)
                        {
                            var mitsuki = _gameGlobal.MitsukiGarbageList.Find(x =>
                                x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);
                            if (mitsuki != null)
                            {
                                for (var i = 0; i < mitsuki.Training.Count; i++)
                                {
                                    var player2 = game.PlayersList.Find(x =>
                                        x.DiscordAccount.DiscordId == mitsuki.Training[i]);
                                    if (player2 != null)
                                    {
                                        player2.Status.AddBonusPoints(-1);
                                      await  _phrase.MitsukiGarbageSmell.SendLog(player2);
                                    }
                                }
                            }
                        }

                        //end Запах мусора:
                        break;

                    case "Осьминожка" :
                        var octo = _gameGlobal.OctopusTentaclesList.Find(x =>
                            x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                        if (octo == null)
                        {
                            _gameGlobal.OctopusTentaclesList.Add(new Octopus.TentaclesClass( player.DiscordAccount.DiscordId, game.GameId, player.Status.PlaceAtLeaderBoard));
                        }
                        else
                        {
                            if (octo.UniqePlacesList.All(x => x.LeaderboardPlace != player.Status.PlaceAtLeaderBoard))
                            {
                                octo.UniqePlacesList.Add(new Octopus.TentaclesSubClass(player.Status.PlaceAtLeaderBoard));
                            }
                        }
                        break;
                }

            }

            if (game.RoundNo > 10)
            {
                //TODO: implement end of the game, after turn 10.

              
                var ll = 0;

            //handle Octo
            var octopusInk = _gameGlobal.OctopusInkList.Find(x => x.GameId == game.GameId);
            var octopusInv = _gameGlobal.OctopusInvulnerabilityList.Find(x => x.GameId == game.GameId);

            if (octopusInk != null)
            {
                foreach (var t in octopusInk.RealScoreList)
                {
                    var pl = game.PlayersList.Find(x => x.DiscordAccount.DiscordId == t.PlayerId);
                    pl?.Status.AddBonusPoints(t.RealScore);
                }
            }

            if (octopusInv != null)
            {
                var octoPlayer = game.PlayersList.Find(x => x.DiscordAccount.DiscordId == octopusInv.PlayerDiscordId);
                octoPlayer.Status.AddBonusPoints(octopusInv.Count);
            }

            //end handle Octo

            foreach (var play in game.PlayersList)
            {
                play.DiscordAccount.IsPlaying = false;
                await _gameUpdateMess.UpdateMessage(play);
                if (!play.IsBot())
                {
                    await play.Status.SocketMessageFromBot.Channel.SendMessageAsync("gg wp ");
                }

            }

            _global.GamesList.Remove(game);
            Console.WriteLine("____________________________");
            }
        }

        public bool HandleOctopus(GameBridgeClass octopusPlayer, GameBridgeClass playerAttackedOctopus, GameClass game)
        {

            if (octopusPlayer.Character.Name != "Осьминожка")
            {
                return true;
            }
         

            game.GameLogs += $" ⟶ **{playerAttackedOctopus.DiscordAccount.DiscordUserName}** победил \n";
            game.PreviousGameLogs += $" ⟶ **{playerAttackedOctopus.DiscordAccount.DiscordUserName}** победил \n";

            //еврей
            var point = HandleJewPassive(playerAttackedOctopus, game);
            //end еврей

            playerAttackedOctopus.Status.AddRegularPoints(point.Result);

            playerAttackedOctopus.Status.WonTimes++;
            playerAttackedOctopus.Character.Justice.IsWonThisRound = true;

            octopusPlayer.Character.Justice.JusticeForNextRound++;

            playerAttackedOctopus.Status.IsWonLastTime = octopusPlayer.DiscordAccount.DiscordId;
            octopusPlayer.Status.IsLostLastTime = playerAttackedOctopus.DiscordAccount.DiscordId;



                var octo = _gameGlobal.OctopusInkList.Find(x =>
                    x.PlayerDiscordId == octopusPlayer.DiscordAccount.DiscordId &&
                    x.GameId == game.GameId);

                if (octo == null)
                {
                    _gameGlobal.OctopusInkList.Add(new Octopus.InkClass(octopusPlayer.DiscordAccount.DiscordId, game, playerAttackedOctopus.DiscordAccount.DiscordId));

                }
                else
                {
                    var enemyRealScore = octo.RealScoreList.Find(x => x.PlayerId == playerAttackedOctopus.DiscordAccount.DiscordId);
                    var octoRealScore = octo.RealScoreList.Find(x => x.PlayerId == octopusPlayer.DiscordAccount.DiscordId);

                    if (enemyRealScore == null)
                    {
                        octo.RealScoreList.Add(new Octopus.InkSubClass( playerAttackedOctopus.DiscordAccount.DiscordId, game.RoundNo, -1));
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
    }
}
