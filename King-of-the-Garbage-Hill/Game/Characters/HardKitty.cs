using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class HardKitty : IServiceSingleton
    {
        private readonly InGameGlobal _inGameGlobal;
        private readonly CharactersUniquePhrase _phrase;

        public HardKitty(InGameGlobal inGameGlobal, CharactersUniquePhrase phrase)
        {
            _inGameGlobal = inGameGlobal;
            _phrase = phrase;
        }
        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleHardKitty(GameBridgeClass player)
        {
         //   throw new System.NotImplementedException();
        }

        public void HandleHardKittyAfter(GameBridgeClass player, GameClass game)
        {
          //

        }

        public class DoebatsyaClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<DoebatsyaSubClass> LostSeries = new List<DoebatsyaSubClass>();

            public DoebatsyaClass(ulong playerDiscordId, ulong gameId, ulong enemyId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                LostSeries.Add(new DoebatsyaSubClass(enemyId));
            }
        }

        public class DoebatsyaSubClass
        {
            public ulong EnemyId;
            public int Series;

            public DoebatsyaSubClass(ulong enemyId)
            {
                EnemyId = enemyId;
                Series = 1;
            }

        }


        public class MuteClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<ulong> UniquePlayers = new List<ulong>();

            public MuteClass(ulong playerDiscordId, ulong gameId, ulong enemyId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                UniquePlayers.Add(enemyId);
            }
        }


    }
}
