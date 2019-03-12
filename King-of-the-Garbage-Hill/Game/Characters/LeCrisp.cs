using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;


namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class LeCrisp : IServiceSingleton
    {

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleLeCrisp(GameBridgeClass player)
        {
          //  throw new System.NotImplementedException();
        }

        public void HandleLeCrispAfter(GameBridgeClass player)
        {
            player.Status.IsAbleToWin = true;
        }

        public class LeCrispImpactClass
        {
            public ulong DiscordId;
            public ulong GameId;
            public int RoundNo;

            public LeCrispImpactClass(ulong discordId, ulong gameId, int roundNo)
            {
                DiscordId = discordId;
                GameId = gameId;
                RoundNo = roundNo;
            }
        }
    }
}
