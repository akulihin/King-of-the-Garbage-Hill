using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game
{
    public class CalculateStage2 : IServiceSingleton
    {
        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        private readonly Global _global;
        private readonly GameUpdateMess _upd;
        private readonly SecureRandom _rand;

        public CalculateStage2(Global global, GameUpdateMess upd, SecureRandom rand)
        {
            _global = global;
            _upd = upd;
            _rand = rand;
        }

        public async Task DeepListMind(GameClass game)
        {
            Console.WriteLine($"calculating game #{game.GameId}...");

            var watch = new Stopwatch() ;
            watch.Start();

            game.TimePassed.Stop();
            game.GameStatus = 2;

            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                

                var pointsWined = 0;
                var player = game.PlayersList[i];
                if (player.Status.WhoToAttackThisTurn == 0 && player.Status.IsBlock == false)
                {
                    player.Status.IsBlock = true;
                }

                var randomForTooGood = 50;

                //if block => no one gets points, and no redundant playerAttacked variable
                if (player.Status.IsBlock) continue;

                var playerAttacked =
                    game.PlayersList.Find(x => x.Account.DiscordId == player.Status.WhoToAttackThisTurn);
                if (playerAttacked.Status.WhoToAttackThisTurn == 0 && playerAttacked.Status.IsBlock == false)
                {
                    playerAttacked.Status.IsBlock = true;
                }

                //if block => no one gets points
                if (playerAttacked.Status.IsBlock) continue;

                //round 1 (contr)

                //TODO: whoIsBetter
                var whoIsBetter = WhoIsBetter(player, playerAttacked);

                //main formula:
                var A = player.Character;
                var B = playerAttacked.Character;

                var strangeNumber = A.Intelligence - B.Intelligence
                                    + A.Strength - B.Strength
                                    + A.Speed - B.Speed
                                    + A.Psyche - B.Psyche;

                var pd = A.Psyche - B.Psyche;

                if (pd > 0 && pd < 4)
                    strangeNumber += 1;
                else if (pd > 0 && pd >= 4)
                    strangeNumber += 2;
                else if (pd < 0 && pd > -4)
                    strangeNumber -= 1;
                else if (pd < 0 && pd <= -4)
                    strangeNumber -= 2;

                if (whoIsBetter == player)
                    strangeNumber += 5;
                else if (whoIsBetter == playerAttacked) strangeNumber -= 5;

                if (strangeNumber >= 14) randomForTooGood = 63;
                if (strangeNumber <= -14) randomForTooGood = 37;

                if (strangeNumber > 0)
                {      
                    pointsWined++;
                }
                //end round 1

                //round 2 (Justice)
                if (player.Character.Justice.JusticeNow > playerAttacked.Character.Justice.JusticeNow || player.Character.Justice == playerAttacked.Character.Justice)
                {
                    pointsWined++;
                }
                //end round 2

                //round 3 (Random)
                if (pointsWined < 2)
                {
                    var randomNumber = _rand.Random(1, 100);
                    if (randomNumber <= randomForTooGood)
                    {
                        pointsWined++;
                    }
                }
                //end round 3

                //CheckIfWin to remove Justice
                if (pointsWined >= 2)
                {
                    player.Status.Score++;
                    player.Character.Justice.IsWonThisRound = true;

                    playerAttacked.Character.Justice.JusticeForNextRound++;
                }
                else
                {
                    playerAttacked.Status.Score++;
                    playerAttacked.Character.Justice.IsWonThisRound = true;

                    player.Character.Justice.JusticeForNextRound++;
                }
            }

            if (game.RoundNo % 2 == 0)
                game.TurnLengthInSecond += 10;
            else
                game.TurnLengthInSecond -= 10;

            if (game.RoundNo == 1) game.TurnLengthInSecond -= 75;

            var orderedPlayersList = game.PlayersList.OrderByDescending(x => x.Status.Score);
            game.PlayersList = orderedPlayersList.ToList();

            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                var player = game.PlayersList[i];

                player.Status.PlaceAtLeaderBoard = i + 1;
                player.Status.IsBlock = false;
                player.Status.IsAbleToTurn = true;
                player.Status.IsReady = false;
                player.Status.WhoToAttackThisTurn = 0;
                player.Status.MoveListPage = 1;

                if (player.Character.Justice.IsWonThisRound)
                {
                    player.Character.Justice.JusticeNow = 0;
                    player.Character.Justice.IsWonThisRound = false;
                }

                player.Character.Justice.JusticeNow += player.Character.Justice.JusticeForNextRound;
                player.Character.Justice.JusticeForNextRound = 0;

                if (player.Character.Justice.JusticeNow > 5)
                {
                    player.Character.Justice.JusticeNow = 5;
                }
            }


            game.GameStatus = 1;
            game.RoundNo++;

            for (var i = 0; i < game.PlayersList.Count; i++)
            {
              await _upd.UpdateMessage(game.PlayersList[i]);
            }

            game.TimePassed.Reset();
            game.TimePassed.Start();
            Console.WriteLine($"Finished calculating game #{game.GameId}. || {watch.Elapsed.TotalSeconds}s");
        }

        public GameBridgeClass WhoIsBetter(GameBridgeClass player1, GameBridgeClass player2)
        {
            bool toReturn;
            var c1 = player1.Character;
            var c2 = player1.Character;

            var sup1 = GetSuperiorStat(c1);
            var sup2 = GetSuperiorStat(c2);

            switch (sup1.Index)
            {
                //если ты Умный и он НЕ Сильный, ты победил
                case 1 when sup2.Index != 2:
                    toReturn = true;
                    break;
                //если Сильный и он НЕ Быстрый
                case 2 when sup2.Index != 3:
                    toReturn = true;
                    break;
                //если Быстрый и он НЕ Умный
                case 3 when sup2.Index != 1:
                    toReturn = true;
                    break;
                default:
                    // if 1-50 = player1
                    // if 51-100 = player2
                    var randomNumber2 = _rand.Random(1, 100);
                    toReturn = randomNumber2 <= 50;
                    break;
            }

            return toReturn ? player1 : player2;
        }

        public SuperiorStat GetSuperiorStat(CharacterClass c)
        {
            var allStatsList = new List<SuperiorStat>
            {
                new SuperiorStat(1, c.Intelligence),
                new SuperiorStat(2, c.Strength),
                new SuperiorStat(3, c.Speed)
            };

            var sortedList = allStatsList.OrderByDescending(x => x.Number).ToList();
            return sortedList[0];
        }

        public struct SuperiorStat
        {
            public int Index;

            public int Number;

            /*
                Интеллект: 1
                Сила: 2
                Скорость: 3
             */
            public SuperiorStat(int index, int number)
            {
                Index = index;
                Number = number;
            }
        }
    }
}