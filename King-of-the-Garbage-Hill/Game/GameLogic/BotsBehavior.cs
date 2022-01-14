using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

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

        HandleBotMoral(player, game);
        if (player.Status.MoveListPage == 3) await HandleLvlUpBot(player, game);
        if (player.Status.MoveListPage == 1) await HandleBotAttack(player, game);
    }

    public void HandleBotMoral(GamePlayerBridgeClass bot, GameClass game)
    {
        var moral = bot.Character.GetMoral();
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            //если хардкитти или осьминожка - всегда ждет 15 морали
            if (bot.Character.Name is "HardKitty" or "Осьминожка")
                if (moral < 15)
                    return;

            if (bot.Character.Name is "Вампур")
                if (moral < 30)
                    return;

            //если авдка не в топ 3 - ждет 10
            if (bot.Character.Name == "AWDKA")
                if (bot.Status.PlaceAtLeaderBoard > 3 && moral < 10)
                    return;

            //если бот на 5м месте то ждет 10
            if (bot.Status.PlaceAtLeaderBoard == 5 && moral < 10)
                return;

            //если бот на последнем месте - ждет 15
            if (bot.Status.PlaceAtLeaderBoard == 6 && moral < 15)
                return;

            //обычные боты ждут 3 морали если они входят в топ 3, else ждут 5 морали.
            if (bot.Status.PlaceAtLeaderBoard > 3 && moral < 5)
                return;
        }
        //end логика до 10го раунда

        //прожать всю момаль
        if (moral >= 15)
        {
            bot.Character.AddMoral(bot.Status, -15, "Обмен Морали: ", false);
            bot.Character.AddBonusPointsFromMoral(10);
        }

        if (moral >= 10)
        {
            bot.Character.AddMoral(bot.Status, -10, "Обмен Морали: ", false);
            bot.Character.AddBonusPointsFromMoral(6);
        }

        if (moral >= 5)
        {
            bot.Character.AddMoral(bot.Status, -5, "Обмен Морали: ", false);
            bot.Character.AddBonusPointsFromMoral(2);
        }

        if (moral >= 3)
        {
            bot.Character.AddMoral(bot.Status, -3, "Обмен Морали: ", false);
            bot.Character.AddBonusPointsFromMoral(1);
        }
        /*
                                _help.SendMsgAndDeleteItAfterRound(player,
                                "У тебя недосточно морали, чтобы поменять ее на бонусные очки.\n" +
                                "3 морали =  1 бонусное очко\n" +
                                "5 морали = 2 бонусных очка\n" +
                                "10 морали = 8 бонусных очков\n" +
                                "15 морали = 15 бонусных очков");
         */
        //end прожать всю момаль
    }


    public async Task HandleBotAttack(GamePlayerBridgeClass bot, GameClass game)
    {
        //local variables
        var allPlayers = _gameGlobal.NanobotsList.Find(x => x.GameId == game.GameId);
        var maxRandomNumber = 0;
        var isBlock = 6;
        var minimumRandomNumberForBlock = 1;
        var maximumRandomNumberForBlock = 4;
        var mandatoryAttack = -1;
        //end local variables

        //calculation Tens
        foreach (var player in allPlayers.Nanobots)
        {
            //if justice is the same
            if (bot.Character.Justice.GetJusticeNow() == player.Player.Character.Justice.GetJusticeNow())
                player.AttackPreference -= 5;
            //if bot justice less than platers
            else if (bot.Character.Justice.GetJusticeNow() < player.Player.Character.Justice.GetJusticeNow())
                player.AttackPreference -= 6;

            //if player is first
            if (player.Player.Status.PlaceAtLeaderBoard == 1)
                player.AttackPreference -= 1;

            //if player is second when we are first
            if (bot.Status.PlaceAtLeaderBoard == 1 && player.Player.Status.PlaceAtLeaderBoard == 2)
                player.AttackPreference -= 1;

            //lost last round + toogood
            if (bot.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == player.Player.Status.PlayerId && x.IsTooGood))
                player.AttackPreference -= 7;
            //lost last round-1 toogood
            else if (bot.Status.WhoToLostEveryRound.Any(x =>
                         x.RoundNo == game.RoundNo - 2 && x.EnemyId == player.Player.Status.PlayerId && x.IsTooGood))
                player.AttackPreference -= 7;
            //lost last round 
            else if (bot.Status.WhoToLostEveryRound.Any(x =>
                         x.RoundNo == game.RoundNo - 1 && x.EnemyId == player.Player.Status.PlayerId))
                player.AttackPreference -= 5;
            //lost last round-1
            else if (bot.Status.WhoToLostEveryRound.Any(x =>
                         x.RoundNo == game.RoundNo - 2 && x.EnemyId == player.Player.Status.PlayerId))
                player.AttackPreference -= 5;


            //won and too good
            if (player.Player.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.Status.PlayerId && x.IsTooGood))
                player.AttackPreference += 4;


            //how many players are attacking the same player
            var count = game.PlayersList.FindAll(x => x.Status.WhoToAttackThisTurn == player.Player.Status.PlayerId)
                .Count;
            player.AttackPreference -= count;

            //custom bot behavior
            switch (bot.Character.Name)
            {
                case "Sirinoks":
                {
                    var siriFriends = _gameGlobal.SirinoksFriendsList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);

                    if (siriFriends != null)
                    {
                        //+5 к значению тех, кто еще не друг.
                        if (!siriFriends.FriendList.Contains(player.Player.Status.PlayerId) &&
                            player.AttackPreference > 0)
                            player.AttackPreference += 5;


                        //До начала 5го хода может нападать только на одну цель. Если значение цели 0 - то блок.
                        if (siriFriends.FriendList.Count == 1 && game.RoundNo < 5)
                        {
                            var sirisFried = game.PlayersList.Find(x => x.Status.PlayerId == siriFriends.FriendList[0]);

                            if (player.Player.Status.PlayerId != sirisFried.Status.PlayerId)
                            {
                                player.AttackPreference = 0;
                            }
                            else
                            {
                                if (player.AttackPreference > 3)
                                    mandatoryAttack = sirisFried.Status.PlaceAtLeaderBoard;
                                else
                                    player.AttackPreference = 0;
                            }
                        }


                        //Если кол-во оставшихся ходов == кол-во незапроканных друзей, то выбирает цель только из тех, кто еще не друг.
                        var nonFiendsLeft = 5 - siriFriends.FriendList.Count;
                        var roundsLeft = 10 - game.RoundNo;
                        var allNotFriends = game.PlayersList.FindAll(x =>
                            !siriFriends.FriendList.Contains(x.Status.PlayerId) &&
                            x.Status.PlayerId != bot.Status.PlayerId);

                        if (nonFiendsLeft == roundsLeft)
                            if (allNotFriends is { Count: > 0 })
                                mandatoryAttack = allNotFriends.FirstOrDefault().Status.PlaceAtLeaderBoard;

                        if (game.RoundNo == 10) isBlock = 99999;
                    }
                }
                    break;
                case "Толя":
                    //Jew
                    foreach (var v in game.PlayersList)
                        if (v.Status.WhoToAttackThisTurn == player.Player.Status.PlayerId)
                            player.AttackPreference += 6;
                    //end Jew
                    break;

                case "LeCrisp":
                    //Jew
                    foreach (var v in game.PlayersList)
                        if (v.Status.WhoToAttackThisTurn == player.Player.Status.PlayerId)
                            player.AttackPreference += 6;
                    //end Jew
                    break;

                case "Глеб":
                {
                    isBlock = 99999;

                    if (player.Player.Status.IsSkip)
                        player.AttackPreference = 0;

                    //Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.
                    var glebAcc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                        x.PlayerId == bot.Status.PlayerId && game.GameId == x.GameId);

                    if (glebAcc != null)
                        if (glebAcc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            if (bot.Status.WhoToLostEveryRound.Any(x =>
                                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == player.Player.Status.PlayerId &&
                                    x.IsTooGood))
                                player.AttackPreference += 7;
                            else if (bot.Status.WhoToLostEveryRound.Any(x =>
                                         x.RoundNo == game.RoundNo - 1 && x.EnemyId == player.Player.Status.PlayerId))
                                player.AttackPreference += 5;


                            //Под претендентом не ставит блок.
                            {
                                minimumRandomNumberForBlock = 0;
                                maximumRandomNumberForBlock = 0;
                            }


                            //Под претендентом автоматически выбирает цель с наибольшим значением. 
                            var sorted = allPlayers.Nanobots.OrderByDescending(x => x.AttackPreference).ToList();

                            mandatoryAttack = sorted[0].Player.Status.PlaceAtLeaderBoard;
                        }
                }
                    break;
                case "Загадочный Спартанец в маске":
                {
                    /*
                                            
                    
                                            //"Они позорят военное искусство"
                                            var Spartan = _gameGlobal.SpartanShame.Find(x =>
                                                x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                                            if (!Spartan.FriendList.Contains(p.Player.Status.PlayerId)) p.AttackPreference += 4;
                                            //end "Они позорят военное искусство"
                    
                                            //Первым ходом выбирает только тех, кого точно победит. (кто побеждает его по статам, тем 10 = 0)
                    
                    
                                            //end Первым ходом выбирает только тех, кого точно победит. (кто побеждает его по статам, тем 10 = 0)
                    
                    
                                            //holy fuck, how can I read it!?!?
                                            //автоматически выбирает целью помеченного МЕТКОЙ врага, если превосходит его по статам и НЕ УСТУПАЕТ в Справедливости. то есть > или =
                                            var Spartan1 = _gameGlobal.SpartanMark.Find(x =>
                                                x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                    
                                            if (Spartan1.FriendList.Contains(p.Player.Status.PlayerId))
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
            switch (player.Player.Character.Name)
            {
                case "Darksci":
                    if (game.RoundNo == 9 ||
                        game.RoundNo == 10 && game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                        player.AttackPreference = 0;
                    break;
            }
            //end custom enemy

            //self always = 0
            if (bot.Status.PlayerId == player.Player.Status.PlayerId) player.AttackPreference = 0;

            if (player.AttackPreference <= 0)
            {
                isBlock--;
                player.AttackPreference = 0;
            }

            maxRandomNumber += player.AttackPreference;
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
                var count = allPlayers.Nanobots.FindAll(x => x.AttackPreference >= 10).Count;
                if (count <= 0) minimumRandomNumberForBlock += 2;
                //end rammus
                break;


            case "LeCrisp":
                //block chances
                if (game.RoundNo is >= 2 and <= 4) minimumRandomNumberForBlock += 1;

                var assassinsCount = game.PlayersList
                    .FindAll(x => bot.Character.GetStrength() - x.Character.GetStrength() <= -2).Count;

                minimumRandomNumberForBlock += assassinsCount;

                //end block chances
                break;
        }
        //end custom behaviour After calculation Tens


        //mandatory attack
        var isAttacked = false;
        if (mandatoryAttack >= 0) isAttacked = await AttackPlayer(bot, mandatoryAttack);

        //block
        if (minimumRandomNumberForBlock > maximumRandomNumberForBlock)
            maximumRandomNumberForBlock = minimumRandomNumberForBlock;

        var isBlockCheck = _rand.Random(minimumRandomNumberForBlock, maximumRandomNumberForBlock);
        if (isBlockCheck > isBlock && !isAttacked)
        {
            //block
            await _gameReaction.HandleAttack(bot, null, -10);
            ResetTens(allPlayers);
            return;
        }

        //"random" attack
        var randomNumber = _rand.Random(1, maxRandomNumber);

        var player1 = allPlayers.Nanobots.Find(x => x.PlaceAtLeaderBoard == 1);
        var player2 = allPlayers.Nanobots.Find(x => x.PlaceAtLeaderBoard == 2);
        var player3 = allPlayers.Nanobots.Find(x => x.PlaceAtLeaderBoard == 3);
        var player4 = allPlayers.Nanobots.Find(x => x.PlaceAtLeaderBoard == 4);
        var player5 = allPlayers.Nanobots.Find(x => x.PlaceAtLeaderBoard == 5);
        var player6 = allPlayers.Nanobots.Find(x => x.PlaceAtLeaderBoard == 6);

        int whoToAttack;

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


        if (!isAttacked)
            await _gameReaction.HandleAttack(bot, null, -10);

        ResetTens(allPlayers);
    }

    public async Task<bool> AttackPlayer(GamePlayerBridgeClass bot, int whoToAttack)
    {
        return await _gameReaction.HandleAttack(bot, null, whoToAttack);
    }

    public void ResetTens(NanobotClass nanobots)
    {
        foreach (var p in nanobots.Nanobots) p.AttackPreference = 10;
    }


    public async Task HandleLvlUpBot(GamePlayerBridgeClass player, GameClass game)
    {
        do
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
        } while (player.Status.LvlUpPoints > 1);

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