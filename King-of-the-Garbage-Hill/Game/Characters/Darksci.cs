
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Darksci: IServiceSingleton
    {
        
        private readonly SecureRandom _rand;
        private readonly CharactersUniquePhrase _phrase;
        private readonly GameUpdateMess _upd;

        public Darksci(GameUpdateMess upd, SecureRandom rand, CharactersUniquePhrase phrase)
        {
            _upd = upd;
            _rand = rand;
            _phrase = phrase;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleDarksci(GamePlayerBridgeClass player)
        {
          //  throw new System.NotImplementedException();
        }

        public async Task HandleDarksiAfter(GamePlayerBridgeClass player, GameClass game)
        {
            if (player.Status.IsLostLastTime != 0)
            {
                //Не повезло
                player.Character.AddPsyche(player.Status, -1);
                player.MinusPsycheLog(game);
                await  _phrase.DarksciNotLucky.SendLog(player);
                //end Не повезло
            }

        }


        public class LuckyClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<ulong> TouchedPlayers = new List<ulong>();

            public LuckyClass(ulong playerDiscordId, ulong gameId, ulong enemyId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                TouchedPlayers.Add(enemyId);
            }
        }
    }
}
