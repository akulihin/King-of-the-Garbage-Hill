
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class CharacterPassives : IServiceSingleton
    {
        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleCharacter(string CharacterName)
        {

        }
    }
}
