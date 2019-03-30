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
            public ulong PlayerDiscordId;
            public int Count;


            public InvulnerabilityClass(ulong playerDiscordId, ulong gameId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                Count = 1;
            }   
        }



        public class InkClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<InkSubClass> RealScoreList = new List<InkSubClass>();

            public InkClass(ulong playerDiscordId, GameClass game, ulong enemyId )
            {
                PlayerDiscordId = playerDiscordId;
                GameId = game.GameId;
                RealScoreList.Add(new InkSubClass(enemyId, game.RoundNo, -1));
                RealScoreList.Add(new InkSubClass(playerDiscordId,  game.RoundNo, 1));
            }   
        }

        public class InkSubClass
        {
            public ulong PlayerId;
            public int RealScore;


            public InkSubClass(ulong playerDiscordId, int roundNo, int realScore)
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
            public ulong PlayerDiscordId;
            public List<TentaclesSubClass> UniqePlacesList = new List<TentaclesSubClass>();

            public TentaclesClass(ulong playerDiscordId, ulong gameId, int place)
            {
                PlayerDiscordId = playerDiscordId;
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
