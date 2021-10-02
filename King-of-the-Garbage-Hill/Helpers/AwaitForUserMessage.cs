using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace King_of_the_Garbage_Hill.Helpers
{
    public sealed class AwaitForUserMessage : IServiceSingleton
    {
        private readonly Global _global;

        public AwaitForUserMessage(Global global)
        {
            _global = global;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<SocketMessage> AwaitMessage(ulong userId, ulong channelId, int delayInSeconds)
        {
            SocketMessage response = null;
            var cancler = new CancellationTokenSource();
            var waiter = Task.Delay(delayInSeconds * 1000, cancler.Token);

            _global.Client.MessageReceived += OnMessageReceived;
            try
            {
                await waiter;
            }
            catch (TaskCanceledException)
            {
            }

            _global.Client.MessageReceived -= OnMessageReceived;

            return response;

            async Task OnMessageReceived(SocketMessage message)
            {
                if (message.Author.Id != userId || message.Channel.Id != channelId)
                    return;
                response = message;
                cancler.Cancel();
                await Task.CompletedTask;
            }
        }

        public async Task<bool> FinishTheGameQuestion(SocketMessageComponent button)
        {
            var mess = await button.Channel.SendMessageAsync("Are you sure, you want to **end** the game? say `y`");
            var res = await AwaitMessage(button.User.Id, button.Channel.Id, 5000);
            var response = res.Content.ToLower();

            await mess.DeleteAsync();


            return response.Contains("yes") || response.Contains("ye") || response.Contains("y") ||
                   response.Contains("у");
        }


        public async Task ReplyAndDeleteOvertime(string mess, int timeInSeconds, SocketReaction reaction)
        {
            var seconds = timeInSeconds * 1000;
            var message = await reaction.Channel.SendMessageAsync(mess);
            await Task.Delay(seconds);
            await message.DeleteAsync();
        }
    }
}