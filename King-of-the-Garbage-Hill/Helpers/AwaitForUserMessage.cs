using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework;

namespace King_of_the_Garbage_Hill.Helpers;

public sealed class AwaitForUserMessage : IServiceSingleton
{
    private readonly Global _global;
    private readonly LoginFromConsole _logs;

    public AwaitForUserMessage(Global global, LoginFromConsole logs)
    {
        _global = global;
        _logs = logs;
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
        catch (TaskCanceledException exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
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
}