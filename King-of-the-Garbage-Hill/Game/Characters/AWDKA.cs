using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Awdka : IServiceSingleton
    {
     
        private readonly SecureRandom _rand;

        private readonly GameUpdateMess _upd;

        public Awdka(GameUpdateMess upd, SecureRandom rand)
        {
            _upd = upd;
            _rand = rand;

        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleAWDKA(GameBridgeClass player)
        {
            throw new System.NotImplementedException();
        }

        public void HandleAWDKAAfter(GameBridgeClass player)
        {
            throw new System.NotImplementedException();
        }
    }
}
