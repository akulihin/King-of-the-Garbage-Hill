using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Mylorik : IServiceSingleton
    {
     
        private readonly SecureRandom _rand;
        private readonly CharactersUniquePhrase _phrase;
        private readonly InGameGlobal _gameGlobal;

        public Mylorik(SecureRandom rand, InGameGlobal gameGlobal, CharactersUniquePhrase phrase)
        {
            _rand = rand;
            _gameGlobal = gameGlobal;
            _phrase = phrase;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        
        public async Task HandleMylorikRevenge(GamePlayerBridgeClass player, ulong enemyIdLostTo, ulong gameId)
        {
            //enemyIdLostTo may be 0

            var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                x.GameId == gameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

            if (enemyIdLostTo != 0)
            {
                //check if very first lost
                if (mylorik == null)
                {
                    _gameGlobal.MylorikRevenge.Add(new MylorikRevengeClass(player.DiscordAccount.DiscordId, gameId,
                        enemyIdLostTo));
                    await _phrase.MylorikRevengeLostPhrase.SendLog(player);
                }
                else
                {
                    if (mylorik.EnemyListDiscordId.All(x => x.EnemyDiscordId != enemyIdLostTo))
                    {
                        mylorik.EnemyListDiscordId.Add(new MylorikRevengeClassSub(enemyIdLostTo));
                        await _phrase.MylorikRevengeLostPhrase.SendLog(player);
                    }
                }
          
            }
            else
            {
                var find = mylorik?.EnemyListDiscordId.Find(x =>
                    x.EnemyDiscordId == player.Status.IsWonLastTime && x.IsUnique);

                if (find != null)
                {
                    player.Status.AddRegularPoints(2);
                    find.IsUnique = false;
                    await _phrase.MylorikRevengeVictoryPhrase.SendLog(player);
                }
            }
        }

        public async Task HandleMylorik(GamePlayerBridgeClass player, GameClass game)
        {
            //Boole


            await Task.CompletedTask;
            //end Boole
        }


        public async Task HandleMylorikAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Revenge
     
              await  HandleMylorikRevenge(player, player.Status.IsLostLastTime, player.DiscordAccount.GameId);
            //end Revenge

            //Spanish
            if (player.Status.IsLostLastTime != 0)
            {
                var rand = _rand.Random(1, 3);

                if (rand == 1)
                {
                    player.Character.AddPsyche(player.Status, -1);
                    player.MinusPsycheLog(game);
                    await _phrase.MylorikSpanishPhrase.SendLog(player);
                }
             
            }

            //end Spanish
        }

        public class MylorikRevengeClass
        {
            public List<MylorikRevengeClassSub> EnemyListDiscordId;
            public ulong GameId;
            public ulong PlayerDiscordId;

            public MylorikRevengeClass(ulong playerDiscordId, ulong gameId, ulong firstLost)
            {
                PlayerDiscordId = playerDiscordId;
                EnemyListDiscordId = new List<MylorikRevengeClassSub> {new MylorikRevengeClassSub(firstLost)};
                GameId = gameId;
            }
        }

        public class MylorikRevengeClassSub
        {
            public ulong EnemyDiscordId;
            public bool IsUnique;

            public MylorikRevengeClassSub(ulong enemyDiscordId)
            {
                EnemyDiscordId = enemyDiscordId;
                IsUnique = true;
            }
        }

    }
}
