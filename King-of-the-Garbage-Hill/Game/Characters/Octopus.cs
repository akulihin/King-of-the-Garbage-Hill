using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Octopus : IServiceSingleton
    {
   
        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleOctopus(GamePlayerBridgeClass player)
        {
        //    throw new System.NotImplementedException();
        }

        public void HandleOctopusAfter(GamePlayerBridgeClass player)
        {
        //    throw new System.NotImplementedException();
        }

        public class InvulnerabilityClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public int Count;


            public InvulnerabilityClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Count = 1;
            }   
        }



        public class InkClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<InkSubClass> RealScoreList = new List<InkSubClass>();

            public InkClass(Guid playerId, GameClass game, Guid enemyPlayerId )
            {
                PlayerId = playerId;
                GameId = game.GameId;
                RealScoreList.Add(new InkSubClass(enemyPlayerId, game.RoundNo, -1));
                RealScoreList.Add(new InkSubClass(playerId,  game.RoundNo, 1));
            }   
        }

        public class InkSubClass
        {
            public Guid PlayerId;
            public int RealScore;


            public InkSubClass(Guid playerDiscordId, int roundNo, int realScore)
            {
                if (roundNo <= 4)
                    realScore = realScore * 1; // Why????????????????????????
                else if (roundNo <= 9)
                    realScore = realScore * 2;
                else if (roundNo == 10) 
                    realScore = realScore * 4;

                PlayerId = playerDiscordId;
                RealScore = realScore;
          
            }

            public void AddRealScore(int roundNo, int realScore = 1)
            {
                if (roundNo <= 4)
                    realScore = realScore * 1; // Why????????????????????????
                else if (roundNo <= 9)
                    realScore = realScore * 2;
                else if (roundNo == 10) 
                    realScore = realScore * 4;

                RealScore += realScore;
            }
        }

        public class TentaclesClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<TentaclesSubClass> UniqePlacesList = new List<TentaclesSubClass>();

            public TentaclesClass(Guid playerId, ulong gameId, int place)
            {
                PlayerId = playerId;
                GameId = gameId;
                UniqePlacesList.Add(new TentaclesSubClass(place));
            }   
        }

        public class TentaclesSubClass
        {
            public int LeaderboardPlace;
            public bool IsActivated;

            public TentaclesSubClass(int leaderboardPlace)
            {
                LeaderboardPlace = leaderboardPlace;
                IsActivated = false;
            }

        }

    }
}
