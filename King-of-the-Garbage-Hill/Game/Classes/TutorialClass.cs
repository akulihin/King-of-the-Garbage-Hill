using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.ReactionHandling;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class TutorialClass : IServiceSingleton
    {
        public readonly List<TutorialReactions.TutorialGame> Tutorials = new();

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
