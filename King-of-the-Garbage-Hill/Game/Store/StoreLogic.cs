using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Store
{
    public class StoreLogic : IServiceSingleton
    {
        private readonly SecureRandom _random;

        public StoreLogic(SecureRandom random)
        {
            _random = random;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }
    }
}
