using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game
{
   public class CalculateStage2 : IServiceSingleton
    {
        public async Task InitializeAsync() => await Task.CompletedTask;

        private readonly Global _global;
        private readonly GameUpdateMess _upd;
        private readonly SecureRandom _rand;

        public CalculateStage2(Global global, GameUpdateMess upd, SecureRandom rand)
        {
            _global = global;
            _upd = upd;
            _rand = rand;
        }

        //TODO: check, if everytthing is actually saved!!!!!
        //Вроде unity test работает
        public void DeepListMind(GameClass game)
        {        
            Console.WriteLine($"calculating game #{game.GameId}...");

            game.TimePassed.Stop();
            game.GameStatus = 2;

            for (var i = 0; i < game.PlayersList.Count; i++)
            {


                var pointsWined = 0;
                var player = game.PlayersList[i];

                //if block => no one gets points, and no redundant playerAttacked variable
                if (player.Status.IsBlock) {continue;}

                var playerAttacked =
                    game.PlayersList.Find(x => x.Account.DiscordId == player.Status.WhoToAttackThisTurn);

                //if block => no one gets points
                if (playerAttacked.Status.IsBlock) {continue;}

                //round 1 (contr)

                //TODO: whoIsBetter
                var whoIsBetter = WhoIsBetter(player, playerAttacked);

                //main formula:
                var A = player.Character;
                var B = playerAttacked.Character;

               var strangeNumber = A.Intelligence - B.Intelligence 
                    + A.Strength - B.Strength 
                    + A.Strength - B.Speed
                    + A.Psyche - B.Psyche;
               //TODO: что значит +-?
// +-1  if  психика А - психика В < +-5   или +-2 if  >=+-5
                if (A.Psyche - B.Psyche < 5)
                {
                    strangeNumber += 1;
                }
                if (A.Psyche - B.Psyche >= 5)
                {
                    strangeNumber += 5;
                }

                if (whoIsBetter == player)
                {
                    strangeNumber += 5;
                }
                else
                {
                    strangeNumber -= 5;
                }
                
                if (strangeNumber > 0)
                {
                    if (whoIsBetter == player)
                    {
                        player.Status.Score++;
                        pointsWined++;
                    }
                    else
                    {
                        playerAttacked.Status.Score++;
                    }
                }

                //end round 1

                //round 2 (Justice)
                if (player.Character.Justice > playerAttacked.Character.Justice)
                {
                    player.Status.Score++;
                    pointsWined++;
                }
                else if (player.Character.Justice < playerAttacked.Character.Justice)
                {
                    playerAttacked.Status.Score++;
                }
                else if (player.Character.Justice == playerAttacked.Character.Justice)
                {
                    // if 1-50 = player.Score++;
                    // if 51-100 = playerAttacked.Score++;
                    var randomNumber2 = _rand.Random(1, 100);
                    if (randomNumber2 <= 50)
                    {
                        player.Status.Score++;
                        pointsWined++;
                    }
                    else
                    {
                        playerAttacked.Status.Score++;
                    }
                }
                //end round 2

                //round 3 (Random)
                // if 1-50 = player.Score++;
                // if 51-100 = playerAttacked.Score++;
                var randomNumber = _rand.Random(1, 100);
                if (randomNumber <= 50)
                {
                    player.Status.Score++;
                    pointsWined++;
                }
                else
                {
                    playerAttacked.Status.Score++;
                }
                //end round 3

                //CheckIfWin to remove Justice
                if (pointsWined >= 2)
                {
                    player.Character.Justice = 0;
                    playerAttacked.Character.Justice++;

                    if (playerAttacked.Character.Justice >= 6)
                    {
                        playerAttacked.Character.Justice = 5;
                    }
                }
                else
                {
                    playerAttacked.Character.Justice = 0;
                    player.Character.Justice++;

                    if (player.Character.Justice >= 6)
                    {
                        player.Character.Justice = 5;
                    }
                }
            }

            if (game.RoundNo % 2 == 0)
            {
                game.TurnLengthInSecond += 10;
            }
            else
            {
                game.TurnLengthInSecond -= 10;
            }

            if (game.RoundNo == 1)
            {
                game.TurnLengthInSecond -= 75;
            }


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
            }

            game.GameStatus = 1;
            game.RoundNo++;
            game.TimePassed.Reset();
            game.TimePassed.Start();
        }

        public GameBridgeClass WhoIsBetter(GameBridgeClass player1, GameBridgeClass player2)
        {
            var toReturn = false;
            var c1 = player1.Character;
            var c2 = player1.Character;

            var sup1 = GetSuperiorStat(c1);
           // var sup2 = GetSuperiorStat(c2);

            switch (sup1.Index)
            {
                case 1:
                    if (c1.Intelligence > c2.Speed && c1.Intelligence > c2.Intelligence)
                        toReturn = true;
                    break;
                case 2:
                    if (c1.Strength > c2.Intelligence && c1.Strength > c2.Strength)
                        toReturn = true;
                    break;
                case 3:
                    if (c1.Speed > c2.Strength && c1.Speed > c2.Speed)
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
                new SuperiorStat(3, c.Speed),
                new SuperiorStat(4, c.Psyche)
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
