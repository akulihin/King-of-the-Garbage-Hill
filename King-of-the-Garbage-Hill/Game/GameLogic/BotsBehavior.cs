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
            if (!player.IsBot() || player.Status.IsReady && player.Status.MoveListPage != 3) return;
            var realPlayers = game.PlayersList.FindAll(x => !x.IsBot() && !x.Status.IsReady).ToList().Count;
            if(realPlayers > 0 && game.TimePassed.Elapsed.Seconds < game.TurnLengthInSecond - 15) return;

            if (player.Status.MoveListPage == 1) await HandleAttack(player, game);

            if (player.Status.MoveListPage == 3) await HandleLvlUp(player, game);
        }

        public async Task HandleAttack(GamePlayerBridgeClass bot, GameClass game)
        {
            //local variables
            var nanobots = _gameGlobal.NanobotsList.Find(x => x.GameId == game.GameId);
            var sum = 0;
            var isBlock = 6;
            var minimumRandomNumberForBlock = 1;
            var maximumRandomNumberForBlock = 4;
            var mandatoryAttack = -1;
            //end local variables

            //calculation Tens
            foreach (var p in nanobots.Nanobots)
            {
             

                if (bot.Character.Justice.GetJusticeNow() == p.Player.Character.Justice.GetJusticeNow())
                    p.Ten -= 5;
                else if (bot.Character.Justice.GetJusticeNow() < p.Player.Character.Justice.GetJusticeNow())
                    p.Ten -= 6;

                if (bot.Status.PlaceAtLeaderBoard < p.Player.Status.PlaceAtLeaderBoard)
                    p.Ten -= 2;

                if (bot.Status.PlaceAtLeaderBoard - p.Player.Status.PlaceAtLeaderBoard == 1)
                    p.Ten -= 1;

                if (bot.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId && x.IsTooGood))
                    p.Ten -= 7;
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId))
                    p.Ten -= 5;


                if (p.Player.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.Status.PlayerId && x.IsTooGood)) p.Ten += 3;


                var count = game.PlayersList.FindAll(x => x.Status.WhoToAttackThisTurn == p.Player.Status.PlayerId)
                    .Count;
                p.Ten -= count;

                //custom behavior
                switch (bot.Character.Name)
                {
                    case "Sirinoks":
                    {
                        //+5 к значению тех, кто еще не друг.
                        var siriFriends = _gameGlobal.SirinoksFriendsList.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);

                        if (siriFriends != null)
                        {
                            if (!siriFriends.FriendList.Contains(p.Player.Status.PlayerId))
                            {
                                p.Ten += 5;
                            }
                            //До начала 5го хода может нападать только на одну цель. Если значение цели 0 - то блок.
                            if (siriFriends.FriendList.Count == 1 && game.RoundNo < 5)
                            {
                                p.Ten = 0;
                                mandatoryAttack = game.PlayersList.Find(x => x.Status.PlayerId == siriFriends.FriendList[0]).Status.PlaceAtLeaderBoard;
                            }
                            //end До начала 5го хода может нападать только на одну цель. Если значение цели 0 - то блок.

                        //Если кол-во оставшихся ходов = кол-во незапроканных друзей, то выбирает цель только из тех, кто еще не друг.
                        // 3 == 4
                        if ((10 - game.RoundNo) - (5 - siriFriends.FriendList.Count) <= 0)
                        {
                            var allNotFriends =
                                game.PlayersList.FindAll(x => !siriFriends.FriendList.Contains(x.Status.PlayerId));

                            if (allNotFriends != null && allNotFriends.Count > 0)
                            {
                                mandatoryAttack = allNotFriends.FirstOrDefault().Status.PlaceAtLeaderBoard;
                            }
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
                                p.Ten += 6;
                        //end Jew
                        break;

                    case "LeCrisp":
                        //Jew
                        foreach (var v in game.PlayersList)
                            if (v.Status.WhoToAttackThisTurn == p.Player.Status.PlayerId)
                                p.Ten += 6;
                        //end Jew
                        break;

                    case "Глеб":
                    {
                        if (p.Player.Status.IsSkip) p.Ten = 0;
                        //Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.
                        var glebAcc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.PlayerId == bot.Status.PlayerId && game.GameId == x.GameId);

                        if (glebAcc != null)
                            if (glebAcc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                if (bot.Status.WhoToLostEveryRound.Any(x =>
                                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId &&
                                    x.IsTooGood))
                                    p.Ten += 7;
                                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId))
                                    p.Ten += 5;
                                //end Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.

                                //Под претендентом не ставит блок.
                                {
                                    minimumRandomNumberForBlock = 0;
                                    maximumRandomNumberForBlock = 0;
                                }
                                //end Под претендентом не ставит блок.

                                //Под претендентом автоматически выбирает цель с наибольшим значением. 
                                var sorted = nanobots.Nanobots.OrderByDescending(x => x.Ten).ToList();

                                mandatoryAttack = sorted[0].Player.Status.PlaceAtLeaderBoard;
                                //end Под претендентом автоматически выбирает цель с наибольшим значением. 
                            }
                    }
                        break;
                    case "Загадочный Спартанец в маске":
                    {
                        //"Они позорят военное искусство"
                        var panth = _gameGlobal.PanthShame.Find(x =>
                            x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                        if (!panth.FriendList.Contains(p.Player.Status.PlayerId)) p.Ten += 4;
                        //end "Они позорят военное искусство"

                        //Первым ходом выбирает только тех, кого точно победит. (кто побеждает его по статам, тем 10 = 0)

                        /*
                        if (game.RoundNo == 1)
                            if (p.Player.Character.GetSpeed() + p.Player.Character.GetStrength() +
                                p.Player.Character.GetIntelligence() + p.Player.Character.GetPsyche()
                                -
                                bot.Character.GetStrength() - bot.Character.GetSpeed() -
                                bot.Character.GetIntelligence() - bot.Character.GetPsyche() > 0)
                                p.Ten = 0;
                                */

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
                    }
                        break;
                }
                //end custom behavior

                //self always = 0
                if (bot.Status.PlayerId == p.Player.Status.PlayerId) p.Ten = 0;

                if (p.Ten <= 0)
                {
                    isBlock--;
                    p.Ten = 0;
                }

                sum += p.Ten;
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
                    var count = nanobots.Nanobots.FindAll(x => x.Ten >= 10).Count;
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
                await _gameReaction.HandleAttackOrLvlUp(bot, null, -10);
                ResetTens(nanobots);
                return;
            }

            var rand = _rand.Random(1, sum);


            var a = nanobots.Nanobots.Find(x => x.Index == 1);
            var b = nanobots.Nanobots.Find(x => x.Index == 2);
            var c = nanobots.Nanobots.Find(x => x.Index == 3);
            var d = nanobots.Nanobots.Find(x => x.Index == 4);
            var e = nanobots.Nanobots.Find(x => x.Index == 5);
            var f = nanobots.Nanobots.Find(x => x.Index == 6);

            var whoToAttack = 0;
            var flag = false;


            if (mandatoryAttack >= 0) flag = await AttackPlayer(bot, mandatoryAttack);

            if (rand <= a.Ten && !flag)
            {
                whoToAttack = a.Player.Status.PlaceAtLeaderBoard;
                flag = await AttackPlayer(bot, whoToAttack);
            }

            if (rand <= a.Ten + b.Ten && !flag)
            {
                whoToAttack = b.Player.Status.PlaceAtLeaderBoard;
                flag = await AttackPlayer(bot, whoToAttack);
            }

            if (rand <= a.Ten + b.Ten + c.Ten && !flag)
            {
                whoToAttack = c.Player.Status.PlaceAtLeaderBoard;
                flag = await AttackPlayer(bot, whoToAttack);
            }

            if (rand <= a.Ten + b.Ten + c.Ten + d.Ten && !flag)
            {
                whoToAttack = d.Player.Status.PlaceAtLeaderBoard;
                flag = await AttackPlayer(bot, whoToAttack);
            }

            if (rand <= a.Ten + b.Ten + c.Ten + d.Ten + e.Ten && !flag)
            {
                whoToAttack = e.Player.Status.PlaceAtLeaderBoard;
                flag = await AttackPlayer(bot, whoToAttack);
            }

            if (rand <= a.Ten + b.Ten + c.Ten + d.Ten + e.Ten + f.Ten && !flag)
            {
                whoToAttack = f.Player.Status.PlaceAtLeaderBoard;
                flag = await AttackPlayer(bot, whoToAttack);
            }


            if (whoToAttack == 0 || !flag)
                await _gameReaction.HandleAttackOrLvlUp(bot, null, -10);

            ResetTens(nanobots);
        }

        public async Task<bool> AttackPlayer(GamePlayerBridgeClass bot, int whoToAttack)
        {
            return await _gameReaction.HandleAttackOrLvlUp(bot, null, whoToAttack);
        }

        public void ResetTens(NanobotClass nanobots)
        {
            foreach (var p in nanobots.Nanobots) p.Ten = 10;
        }


        public async Task HandleLvlUp(GamePlayerBridgeClass player, GameClass game)
        {
            int skillNu;

            var intel = player.Character.GetIntelligence();
            var str = player.Character.GetStrength();
            var speed = player.Character.GetSpeed();
            var psy = player.Character.GetPsyche();

            var stats = new List<BiggestStatClass>
            {
                new BiggestStatClass(1, intel),
                new BiggestStatClass(2, str),
                new BiggestStatClass(3, speed),
                new BiggestStatClass(4, psy)
            };

            stats = stats.OrderByDescending(x => x.StatCount).ToList();

            if (stats[1].StatCount < 7)
            {
                skillNu = stats[1].StatIndex;
            }
            else if (stats[0].StatCount < 10)
            {
                skillNu = stats[0].StatIndex;
            }
            else if (stats[1].StatCount < 10)
            {
                skillNu = stats[1].StatIndex;
            }
            else if (stats[2].StatCount < 10)
            {
                skillNu = stats[2].StatIndex;
            }
            else if (stats[3].StatCount < 10)
            {
                skillNu = stats[3].StatIndex;
            }
            else
            {
                player.Status.MoveListPage = 1;
                return;
            }

            if (player.Character.Name == "LeCrisp" && str < 10) skillNu = 2;
            if (player.Character.Name == "Darksci" && psy < 10) skillNu = 4;
            if (player.Character.Name == "Тигр" && psy < 10 && game.RoundNo <= 6) skillNu = 4;

            await _gameReaction.HandleAttackOrLvlUp(player, null, skillNu);
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
            public int Index;
            public GamePlayerBridgeClass Player;
            public int Ten;


            public Nanobot(GamePlayerBridgeClass player)
            {
                Player = player;
                Ten = 10;
                Index = player.Status.PlaceAtLeaderBoard;
            }
        }

        public class NanobotClass
        {
            public ulong GameId;
            public List<Nanobot> Nanobots = new List<Nanobot>();

            public NanobotClass(IReadOnlyList<GamePlayerBridgeClass> players)
            {
                GameId = players[0].DiscordAccount.GameId;
                foreach (var t in players) Nanobots.Add(new Nanobot(t));
            }
        }
    }
}