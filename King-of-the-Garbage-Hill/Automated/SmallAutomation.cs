using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.Automated
{
    public class SmallAutomation : IServiceSingleton
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
