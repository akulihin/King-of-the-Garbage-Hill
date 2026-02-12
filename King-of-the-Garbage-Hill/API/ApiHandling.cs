using System;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.LocalPersistentData.ServerAccounts;
using King_of_the_Garbage_Hill.DiscordFramework;

namespace King_of_the_Garbage_Hill.API;

/// <summary>
/// Legacy API handling class. The HttpListener-based API has been replaced by
/// ASP.NET Core (Kestrel + SignalR) started in Program.cs.
/// This class is kept to satisfy the IServiceSingleton interface and DI registration.
/// </summary>
public class ApiHandling : IServiceSingleton
{
    private readonly Global _global;
    private readonly LoginFromConsole _log;
    private readonly ServerAccounts _server;
    private readonly CheckIfReady _checkIfReady;

    public ApiHandling(Global global, LoginFromConsole log, ServerAccounts server, CheckIfReady checkIfReady)
    {
        _global = global;
        _log = log;
        _server = server;
        _checkIfReady = checkIfReady;
    }

    public Task InitializeAsync()
    {
        // The web API is now started in Program.cs via ASP.NET Core.
        // No need to start HttpListener here.
        Console.WriteLine("[ApiHandling] Web API is managed by ASP.NET Core in Program.cs");
        return Task.CompletedTask;
    }
}
