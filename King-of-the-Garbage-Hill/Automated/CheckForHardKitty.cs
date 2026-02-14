using System;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.LocalPersistentData.ServerAccounts;

namespace King_of_the_Garbage_Hill.Automated;

public class CheckForHardKitty : IServiceSingleton
{
    private readonly ServerAccounts _server;
    private readonly Global _global;
    private readonly LoginFromConsole _log;
    private Timer _loopingTimer;
    private bool _running;

    public CheckForHardKitty( Global global, LoginFromConsole log, ServerAccounts server)
    {
        _global = global;
        _log = log;
        _server = server;
        CheckTimer();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    internal Task CheckTimer()
    {
        _loopingTimer = new Timer
        {
            AutoReset = true,
            Interval = 30000,
            Enabled = true
        };

        _loopingTimer.Elapsed += CheckForHardKittyStatus;


        return Task.CompletedTask;
    }

    public async void CheckForHardKittyStatus(object sender, ElapsedEventArgs e)
    {
        return; //KEK
        if (System.Reflection.Assembly.GetExecutingAssembly().Location.Contains("D:\\git"))
        {
            return;
        }

        if (_running)
            return;
        _running = true;

        try
        {
            var server = _server.GetServerAccount(226941073778278400);
            var guild = _global.Client.GetGuild(226941073778278400);
            await guild.DownloadUsersAsync();
            var hardKitty = guild.GetUser(193473667286564865);

            foreach (var h in hardKitty.Activities)
                try
                {
                    var state = ((CustomStatusGame)h).State;
                    if (server.LastHardKittyStatus.Contains(state))
                        continue;

                    await guild.GetTextChannel(491152243928727552).SendMessageAsync($"**Новый статус у HardKitty!**\n{state}");
                    server.LastHardKittyStatus.Add(state);
                }
                catch
                {
                    //ignored
                }


        }
        catch (Exception error)
        {
            _log.Warning($"ERROR! CheckForHardKittyStatus: {error}");
        }

        _running = false;
        await Task.CompletedTask;
    }
}