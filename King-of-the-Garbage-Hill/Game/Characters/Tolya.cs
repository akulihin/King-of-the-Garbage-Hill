using System;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Tolya : IServiceSingleton
    {
        private readonly InGameGlobal _inGameGlobal;

        public Tolya(InGameGlobal inGameGlobal)
        {
            _inGameGlobal = inGameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleTolya(GamePlayerBridgeClass player)
        {
            //  throw new System.NotImplementedException();
        }

        public void HandleTolyaAfter(GamePlayerBridgeClass player, GameClass game)
        {
            if (player.Status.IsBlock && player.Status.IsWonThisCalculation != Guid.Empty)
                game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation).Status
                    .IsAbleToWin = true;


            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                var tolya = _inGameGlobal.TolyaCount.Find(x =>
                    x.PlayerId == player.Status.PlayerId && x.GameId == player.DiscordAccount.GameId);
                if (tolya == null)
                {
                    _inGameGlobal.TolyaCount.Add(new TolyaCountClass(player.DiscordAccount.GameId,
                        player.Status.PlayerId, player.Status.IsLostThisCalculation));
                    return;
                }

                if (tolya.IsActive)
                {
                    tolya.IsActive = false;
                    tolya.WhoToLostLastTimeId = player.Status.IsLostThisCalculation;
                }
                else
                {
                    tolya.IsActive = true;
                    tolya.WhoToLostLastTimeId = Guid.Empty;
                }
            }

            //  throw new System.NotImplementedException();
        }

        public class TolyaCountClass
        {
            public int Cooldown;
            public ulong GameId;
            public bool IsActive;
            public Guid PlayerId;
            public Guid WhoToLostLastTimeId;

            public TolyaCountClass(ulong gameId, Guid playerId, Guid whoToLostLastTimeId)
            {
                GameId = gameId;
                PlayerId = playerId;
                WhoToLostLastTimeId = whoToLostLastTimeId;
                IsActive = true;
                Cooldown = 2;
            }
        }
    }
}