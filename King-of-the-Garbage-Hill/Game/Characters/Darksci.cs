using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Darksci : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        private readonly CharactersUniquePhrase _phrase;

        public Darksci(CharactersUniquePhrase phrase, InGameGlobal gameGlobal)
        {
            _phrase = phrase;
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleDarksci(GamePlayerBridgeClass player)
        {
            //  throw new System.NotImplementedException();
        }

        public async Task HandleDarksiAfter(GamePlayerBridgeClass player, GameClass game)
        {
            if (player.Status.IsLostThisCalculation != 0)
            {
                //Не повезло
                if (game.PlayersList.All(x => x.Character.Name != "Бог ЛоЛа") || _gameGlobal.LolGodUdyrList.Any(
                        x =>
                            x.GameId == game.GameId && x.EnemyDiscordId == player.DiscordAccount.DiscordId))
                {
                    player.Character.AddPsyche(player.Status, -1);
                    player.MinusPsycheLog(game);
                    await _phrase.DarksciNotLucky.SendLog(player);
                }
                else
                    await _phrase.ThirdСommandment.SendLog(player);
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