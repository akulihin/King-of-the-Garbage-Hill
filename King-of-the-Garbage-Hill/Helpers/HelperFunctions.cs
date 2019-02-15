using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace King_of_the_Garbage_Hill.Helpers
{
    public sealed class HelperFunctions : IServiceSingleton
    {
        public Task InitializeAsync()
            => Task.CompletedTask;

        public async Task DeleteBotAndUserMessage(IUserMessage botMessage, SocketMessage userMessage,
            int timeInSeconds)
        {
            var seconds = timeInSeconds * 1000;
            await Task.Delay(seconds);
            await botMessage.DeleteAsync();
            await userMessage.DeleteAsync();
        }

        public async Task DeleteMessOverTime(IUserMessage message, int timeInSeconds)
        {
            var seconds = timeInSeconds * 1000;
            await Task.Delay(seconds);
            await message.DeleteAsync();
        }
    }
}