using System;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game
{
   public class CalculateStage2 : IServiceSingleton
    {
        public async Task InitializeAsync() => await Task.CompletedTask;

        private readonly Global _global;

        public CalculateStage2(Global global)
        {
            _global = global;
        }


        public void DeepListMind(GameClass game)
        {
            Console.WriteLine("calculating...");
        }
    }
}
