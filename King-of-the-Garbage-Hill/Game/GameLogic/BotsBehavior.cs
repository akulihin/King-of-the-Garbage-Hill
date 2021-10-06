using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class BotsBehavior : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        private readonly GameReaction _gameReaction;
        private readonly SecureRandom _rand;

        public BotsBehavior(SecureRandom rand, GameReaction gameReaction, InGameGlobal gameGlobal)
        {
            _rand = rand;
            _gameReaction = gameReaction;
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task HandleBotBehavior(GamePlayerBridgeClass player, GameClass game)
        {
            var timeOffest = 0;
            if (!player.IsBot() || player.Status.IsReady && player.Status.MoveListPage != 3) return;
            var realPlayers = game.PlayersList.FindAll(x => !x.IsBot() && !x.Status.IsReady).ToList().Count;
            if (realPlayers > 0 && game.TimePassed.Elapsed.Seconds < game.TurnLengthInSecond - timeOffest) return;

            if (player.Status.MoveListPage == 1) await HandleAttack(player, game);

            if (player.Status.MoveListPage == 3) await HandleLvlUp(player, game);
        }

        public async Task HandleAttack(GamePlayerBridgeClass bot, GameClass game)
        {
            //local variables
            var nanobots = _gameGlobal.NanobotsList.Find(x => x.GameId == game.GameId);
            var maxRandomNumber = 0;
            var isBlock = 6;
            var minimumRandomNumberForBlock = 1;
            var maximumRandomNumberForBlock = 4;
            var mandatoryAttack = -1;
            //end local variables

            //calculation Tens
            foreach (var p in nanobots.Nanobots)
            {
                if (bot.Character.Justice.GetJusticeNow() == p.Player.Character.Justice.GetJusticeNow())
                    p.AttackPreference -= 5;
                else if (bot.Character.Justice.GetJusticeNow() < p.Player.Character.Justice.GetJusticeNow())
                    p.AttackPreference -= 6;

                if (bot.Status.PlaceAtLeaderBoard < p.Player.Status.PlaceAtLeaderBoard)
                    p.AttackPreference -= 2;

                if (bot.Status.PlaceAtLeaderBoard - p.Player.Status.PlaceAtLeaderBoard == 1)
                    p.AttackPreference -= 1;

                if (bot.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId && x.IsTooGood))
                    p.AttackPreference -= 7;
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId))
                    p.AttackPreference -= 5;


                if (p.Player.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.Status.PlayerId && x.IsTooGood))
                    p.AttackPreference += 3;


                var count = game.PlayersList.FindAll(x => x.Status.WhoToAttackThisTurn == p.Player.Status.PlayerId)
                    .Count;
                p.AttackPreference -= count;

                //custom bot behavior
                switch (bot.Character.Name)
                {
                    case "Sirinoks":
                    {
                        //+5 к значению тех, кто еще не друг.
                        var siriFriends = _gameGlobal.SirinoksFriendsList.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);

                        if (siriFriends != null)
                        {
                            if (!siriFriends.FriendList.Contains(p.Player.Status.PlayerId)) p.AttackPreference += 5;
                            //До начала 5го хода может нападать только на одну цель. Если значение цели 0 - то блок.
                            if (siriFriends.FriendList.Count == 1 && game.RoundNo < 5)
                            {
                                p.AttackPreference = 0;
                                mandatoryAttack = game.PlayersList
                                    .Find(x => x.Status.PlayerId == siriFriends.FriendList[0]).Status
                                    .PlaceAtLeaderBoard;
                            }
                            //end До начала 5го хода может нападать только на одну цель. Если значение цели 0 - то блок.

                            //Если кол-во оставшихся ходов = кол-во незапроканных друзей, то выбирает цель только из тех, кто еще не друг.
                            // 3 == 4
                            if (10 - game.RoundNo - (5 - siriFriends.FriendList.Count) <= 0)
                            {
                                var allNotFriends =
                                    game.PlayersList.FindAll(x => !siriFriends.FriendList.Contains(x.Status.PlayerId));

                                if (allNotFriends != null && allNotFriends.Count > 0)
                                    mandatoryAttack = allNotFriends.FirstOrDefault().Status.PlaceAtLeaderBoard;
                            }
                            //end Если кол-во оставшихся ходов = кол-во незапроканных друзей, то выбирает цель только из тех, кто еще не друг.
                        }
                        //end +5 к значению тех, кто еще не друг.
                    }
                        break;
                    case "Толя":
                        //Jew
                        foreach (var v in game.PlayersList)
                            if (v.Status.WhoToAttackThisTurn == p.Player.Status.PlayerId)
                                p.AttackPreference += 6;
                        //end Jew
                        break;

                    case "LeCrisp":
                        //Jew
                        foreach (var v in game.PlayersList)
                            if (v.Status.WhoToAttackThisTurn == p.Player.Status.PlayerId)
                                p.AttackPreference += 6;
                        //end Jew
                        break;

                    case "Глеб":
                    {
                        if (p.Player.Status.IsSkip) p.AttackPreference = 0;
                        //Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.
                        var glebAcc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.PlayerId == bot.Status.PlayerId && game.GameId == x.GameId);

                        if (glebAcc != null)
                            if (glebAcc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                if (bot.Status.WhoToLostEveryRound.Any(x =>
                                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId &&
                                    x.IsTooGood))
                                    p.AttackPreference += 7;
                                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId))
                                    p.AttackPreference += 5;
                                //end Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.

                                //Под претендентом не ставит блок.
                                {
                                    minimumRandomNumberForBlock = 0;
                                    maximumRandomNumberForBlock = 0;
                                }
                                //end Под претендентом не ставит блок.

                                //Под претендентом автоматически выбирает цель с наибольшим значением. 
                                var sorted = nanobots.Nanobots.OrderByDescending(x => x.AttackPreference).ToList();

                                mandatoryAttack = sorted[0].Player.Status.PlaceAtLeaderBoard;
                                //end Под претендентом автоматически выбирает цель с наибольшим значением. 
                            }
                    }
                        break;
                    case "Загадочный Спартанец в маске":
                    {
                        /*
                                                
                        
                                                //"Они позорят военное искусство"
                                                var panth = _gameGlobal.PanthShame.Find(x =>
                                                    x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                                                if (!panth.FriendList.Contains(p.Player.Status.PlayerId)) p.AttackPreference += 4;
                                                //end "Они позорят военное искусство"
                        
                                                //Первым ходом выбирает только тех, кого точно победит. (кто побеждает его по статам, тем 10 = 0)
                        
                        
                                                //end Первым ходом выбирает только тех, кого точно победит. (кто побеждает его по статам, тем 10 = 0)
                        
                        
                                                //holy fuck, how can I read it!?!?
                                                //автоматически выбирает целью помеченного МЕТКОЙ врага, если превосходит его по статам и НЕ УСТУПАЕТ в Справедливости. то есть > или =
                                                var panth1 = _gameGlobal.PanthMark.Find(x =>
                                                    x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                        
                                                if (panth1.FriendList.Contains(p.Player.Status.PlayerId))
                                                    if (p.Player.Character.GetSpeed() + p.Player.Character.GetStrength() +
                                                        p.Player.Character.GetIntelligence() + p.Player.Character.GetPsyche()
                                                        -
                                                        bot.Character.GetStrength() - bot.Character.GetSpeed() -
                                                        bot.Character.GetIntelligence() - bot.Character.GetPsyche() < 0)
                                                        if (bot.Character.Justice.GetJusticeNow() >= p.Player.Character.Justice.GetJusticeNow())
                                                            mandatoryAttack = p.Player.Status.PlaceAtLeaderBoard;
                                                //end автоматически выбирает целью помеченного МЕТКОЙ врага, если превосходит его по статам и НЕ УСТУПАЕТ в Справедливости. то есть > или =
                                           */
                    }
                        break;
                }
                //end custom bot behavior


                //custom enemy
                switch (p.Player.Character.Name)
                {
                    case "Darksci":
                        if (game.RoundNo == 9 ||
                            game.RoundNo == 10 && game.GetAllGameLogs().Contains("Нахуй эту игру"))
                            p.AttackPreference = 0;
                        break;
                }
                //end custom enemy

                //self always = 0
                if (bot.Status.PlayerId == p.Player.Status.PlayerId) p.AttackPreference = 0;

                if (p.AttackPreference <= 0)
                {
                    isBlock--;
                    p.AttackPreference = 0;
                }

                maxRandomNumber += p.AttackPreference;
            }
            //end calculation Tens

            //custom behaviour After calculation Tens
            switch (bot.Character.Name)
            {
                case "Загадочный Спартанец в маске":
                    //на последнем ходу блок -2 (от 3 до 5)
                    if (game.RoundNo == 10) minimumRandomNumberForBlock += 2;
                    // end на последнем ходу блок -2 (от 3 до 5)
                    break;

                case "Толя":
                    //rammus
                    var count = nanobots.Nanobots.FindAll(x => x.AttackPreference >= 10).Count;
                    if (count <= 0) minimumRandomNumberForBlock += 2;
                    //end rammus
                    break;


                case "LeCrisp":
                    //block chances
                    if (game.RoundNo >= 2 && game.RoundNo <= 5) minimumRandomNumberForBlock += 1;

                    var assassinsCount = game.PlayersList
                        .FindAll(x => bot.Character.GetStrength() - x.Character.GetStrength() <= -2).Count;

                    minimumRandomNumberForBlock += assassinsCount;

                    //end block chances
                    break;
            }
            //end custom behaviour After calculation Tens

            if (minimumRandomNumberForBlock > maximumRandomNumberForBlock)
                maximumRandomNumberForBlock = minimumRandomNumberForBlock;

            var isBlockCheck = _rand.Random(minimumRandomNumberForBlock, maximumRandomNumberForBlock);
            if (isBlockCheck > isBlock)
            {
                //block
                await _gameReaction.HandleAttack(bot, null, -10);
                ResetTens(nanobots);
                return;
            }

            var randomNumber = _rand.Random(1, maxRandomNumber);


            var player1 = nanobots.Nanobots.Find(x => x.PlaceAtLeaderBoard == 1);
            var player2 = nanobots.Nanobots.Find(x => x.PlaceAtLeaderBoard == 2);
            var player3 = nanobots.Nanobots.Find(x => x.PlaceAtLeaderBoard == 3);
            var player4 = nanobots.Nanobots.Find(x => x.PlaceAtLeaderBoard == 4);
            var player5 = nanobots.Nanobots.Find(x => x.PlaceAtLeaderBoard == 5);
            var player6 = nanobots.Nanobots.Find(x => x.PlaceAtLeaderBoard == 6);

            var whoToAttack = 0;
            var isAttacked = false;


            if (mandatoryAttack >= 0) isAttacked = await AttackPlayer(bot, mandatoryAttack);

            if (randomNumber <= player1.AttackPreference && !isAttacked)
            {
                whoToAttack = player1.Player.Status.PlaceAtLeaderBoard;
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }

            if (randomNumber <= player1.AttackPreference + player2.AttackPreference && !isAttacked)
            {
                whoToAttack = player2.Player.Status.PlaceAtLeaderBoard;
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }

            if (randomNumber <= player1.AttackPreference + player2.AttackPreference + player3.AttackPreference &&
                !isAttacked)
            {
                whoToAttack = player3.Player.Status.PlaceAtLeaderBoard;
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }

            if (randomNumber <= player1.AttackPreference + player2.AttackPreference + player3.AttackPreference +
                player4.AttackPreference && !isAttacked)
            {
                whoToAttack = player4.Player.Status.PlaceAtLeaderBoard;
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }

            if (randomNumber <= player1.AttackPreference + player2.AttackPreference + player3.AttackPreference +
                player4.AttackPreference + player5.AttackPreference && !isAttacked)
            {
                whoToAttack = player5.Player.Status.PlaceAtLeaderBoard;
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }

            if (randomNumber <= player1.AttackPreference + player2.AttackPreference + player3.AttackPreference +
                player4.AttackPreference + player5.AttackPreference + player6.AttackPreference && !isAttacked)
            {
                whoToAttack = player6.Player.Status.PlaceAtLeaderBoard;
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }


            if (whoToAttack == 0 || !isAttacked)
                await _gameReaction.HandleAttack(bot, null, -10);

            ResetTens(nanobots);
        }

        public async Task<bool> AttackPlayer(GamePlayerBridgeClass bot, int whoToAttack)
        {
            return await _gameReaction.HandleAttack(bot, null, whoToAttack);
        }

        public void ResetTens(NanobotClass nanobots)
        {
            foreach (var p in nanobots.Nanobots) p.AttackPreference = 10;
        }


        public async Task HandleLvlUp(GamePlayerBridgeClass player, GameClass game)
        {
            int skillNumber;

            var intelligence = player.Character.GetIntelligence();
            var strength = player.Character.GetStrength();
            var speed = player.Character.GetSpeed();
            var psyche = player.Character.GetPsyche();

            var stats = new List<BiggestStatClass>
            {
                new(1, intelligence),
                new(2, strength),
                new(3, speed),
                new(4, psyche)
            };

            stats = stats.OrderByDescending(x => x.StatCount).ToList();

            if (stats[1].StatCount < 7)
            {
                skillNumber = stats[1].StatIndex;
            }
            else if (stats[0].StatCount < 10)
            {
                skillNumber = stats[0].StatIndex;
            }
            else if (stats[1].StatCount < 10)
            {
                skillNumber = stats[1].StatIndex;
            }
            else if (stats[2].StatCount < 10)
            {
                skillNumber = stats[2].StatIndex;
            }
            else if (stats[3].StatCount < 10)
            {
                skillNumber = stats[3].StatIndex;
            }
            else
            {
                player.Status.MoveListPage = 1;
                return;
            }

            if (player.Character.Name == "LeCrisp" && strength < 10) skillNumber = 2;
            if (player.Character.Name == "Darksci" && psyche < 10) skillNumber = 4;
            if (player.Character.Name == "Тигр" && psyche < 10 && game.RoundNo <= 6) skillNumber = 4;

            await _gameReaction.HandleLvlUp(player, null, skillNumber);
            player.Status.MoveListPage = 1;
        }

        public class BiggestStatClass
        {
            public int StatCount;
            public int StatIndex;

            public BiggestStatClass(int statIndex, int statCount)
            {
                StatIndex = statIndex;
                StatCount = statCount;
            }
        }

        public class Nanobot
        {
            public int AttackPreference;
            public int PlaceAtLeaderBoard;
            public GamePlayerBridgeClass Player;


            public Nanobot(GamePlayerBridgeClass player)
            {
                Player = player;
                AttackPreference = 10;
                PlaceAtLeaderBoard = player.Status.PlaceAtLeaderBoard;
            }
        }

        public class NanobotClass
        {
            public ulong GameId;
            public List<Nanobot> Nanobots = new();

            public NanobotClass(IReadOnlyList<GamePlayerBridgeClass> players)
            {
                GameId = players[0].GameId;
                foreach (var t in players) Nanobots.Add(new Nanobot(t));
            }
        }
    }
}