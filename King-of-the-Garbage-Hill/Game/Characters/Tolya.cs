using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Tolya : IServiceSingleton
    {

        private readonly SecureRandom _rand;
        private readonly InGameGlobal _inGameGlobal;
        private readonly GameUpdateMess _upd;

        public Tolya(GameUpdateMess upd, SecureRandom rand, InGameGlobal inGameGlobal)
        {
            _upd = upd;
            _rand = rand;
            _inGameGlobal = inGameGlobal;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleTolya(GameBridgeClass player)
        {
          //  throw new System.NotImplementedException();
        }

        public void HandleTolyaAfter(GameBridgeClass player)
        {
            if (player.Status.IsLostLastTime != 0)
            {
                var tolya = _inGameGlobal.TolyaCount.Find(x =>
                    x.PlayerDiscordId == player.DiscordAccount.DiscordId && x.GameId == player.DiscordAccount.GameId);
                if (tolya == null)
                {
                    _inGameGlobal.TolyaCount.Add(new TolyaCountClass(player.DiscordAccount.GameId, player.DiscordAccount.DiscordId, player.Status.IsLostLastTime));
                    return;
                }

                if (tolya.IsActive)
                {
                    tolya.IsActive = false;
                    tolya.WhoToLostLastTime = player.Status.IsLostLastTime;
                    return;
                }
                else
                {
                    tolya.IsActive = true;
                    tolya.WhoToLostLastTime = 0;
                }
            }
           //  throw new System.NotImplementedException();
        }

        public class TolyaCountClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public ulong WhoToLostLastTime;
            public bool IsActive;
            public TolyaCountClass(ulong gameId, ulong playerDiscordId, ulong whoToLostLastTime)
            {
                GameId = gameId;
                PlayerDiscordId = playerDiscordId;
                WhoToLostLastTime = whoToLostLastTime;
                IsActive = true;
            }
        }
    }
}
