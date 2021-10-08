using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Darksci : IServiceSingleton
    {
        


        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task HandleDarksci(GamePlayerBridgeClass player, GameClass game)
        {
            await Task.CompletedTask;
        }

        public void HandleDarksiAfter(GamePlayerBridgeClass player, GameClass game)
        {
            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                //Не повезло
                //LOL GOD, EXAMPLE:
                /*
                if (game.PlayersList.All(x => x.Character.Name != "Бог ЛоЛа") || _gameGlobal.LolGodUdyrList.Any(
                        x =>
                            x.GameId == game.GameId && x.EnemyDiscordId == player.Status.PlayerId))
                {
                    player.Character.AddPsyche(player.Status, -1);
                    player.MinusPsycheLog(game);
                    await game.Phrases.DarksciNotLucky.SendLog(player);
                }
                else
                    await game.Phrases.ThirdСommandment.SendLog(player);*/
                player.Character.AddPsyche(player.Status, -1, "Не повезло: ");
                player.MinusPsycheLog(game);
                game.Phrases.DarksciNotLucky.SendLog(player, true);
                //end Не повезло
            }
        }


        public class LuckyClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<Guid> TouchedPlayers = new();

            public LuckyClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }
    }
}