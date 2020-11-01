using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class HardKitty : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        private readonly CharactersUniquePhrase _phrase;

        public HardKitty(InGameGlobal gameGlobal, CharactersUniquePhrase phrase)
        {
            _gameGlobal = gameGlobal;
            _phrase = phrase;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleHardKitty(GamePlayerBridgeClass player)
        {
            //   throw new System.NotImplementedException();
        }

        public void HandleHardKittyAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Doebatsya
            var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                x.GameId == player.DiscordAccount.GameId &&
                x.PlayerId == player.Status.PlayerId);
            //can be null

            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                if (hardKitty == null)
                {
                    _gameGlobal.HardKittyDoebatsya.Add(new DoebatsyaClass(
                        player.Status.PlayerId, player.DiscordAccount.GameId,
                        player.Status.IsLostThisCalculation));
                }
                else
                {
                    var exists =
                        hardKitty.LostSeries.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);
                    if (exists == null)
                        hardKitty.LostSeries.Add(
                            new DoebatsyaSubClass(player.Status.IsLostThisCalculation));
                    else
                        exists.Series++;
                }

                return;
            }

            var wonPlayer =
                hardKitty?.LostSeries.Find(x => x.EnemyPlayerId == player.Status.IsWonThisCalculation);
            if (wonPlayer != null)
            {
                player.Status.AddRegularPoints(wonPlayer.Series);
                if (wonPlayer.Series >= 2)
                {
                    var player2 = game.PlayersList.Find(x =>
                        x.Status.PlayerId == player.Status.IsWonThisCalculation);

                    player2.Character.AddPsyche(player2.Status, -1, true, "Доебаться: ");
                    player2.MinusPsycheLog(game);
                }

                wonPlayer.Series = 0;


                _phrase.HardKittyDoebatsyaPhrase.SendLog(player);
            }

            // end Doebatsya
        }

        public class DoebatsyaClass
        {
            public ulong GameId;
            public List<DoebatsyaSubClass> LostSeries = new List<DoebatsyaSubClass>();
            public Guid PlayerId;

            public DoebatsyaClass(Guid playerId, ulong gameId, Guid enemyId)
            {
                PlayerId = playerId;
                GameId = gameId;
                LostSeries.Add(new DoebatsyaSubClass(enemyId));
            }
        }

        public class DoebatsyaSubClass
        {
            public Guid EnemyPlayerId;
            public int Series;

            public DoebatsyaSubClass(Guid enemyPlayerId)
            {
                EnemyPlayerId = enemyPlayerId;
                Series = 1;
            }
        }


        public class MuteClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<Guid> UniquePlayers = new List<Guid>();

            public MuteClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }
    }
}