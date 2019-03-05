using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace King_of_the_Garbage_Hill.BotFramework.Extensions
{
   public class CommandsInMemory : IServiceSingleton
    {


        //not data
   
        public List<CommandRam> CommandList { get; set; } = new List<CommandRam>();
        public uint MaximumCommandsInRam = 1000;
        
        public class CommandRam
        {

            public ulong MessageUserId;
            public IUserMessage BotSocketMsg;
            

            public CommandRam(IUserMessage userSocketMsg, IUserMessage botSocketMsg)
            {
          
                MessageUserId = userSocketMsg.Id;
                BotSocketMsg = botSocketMsg;
            }
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }
    }
}
