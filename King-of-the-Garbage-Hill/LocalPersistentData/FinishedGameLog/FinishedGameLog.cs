using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.LocalPersistentData.FinishedGameLog;

public sealed class FinishedGameLog : IServiceSingleton
{
    private readonly List<GameLogsClass> _gameLogs;
    private readonly FinishedGameLogDataStorage _usersDataStorage;

    public FinishedGameLog(FinishedGameLogDataStorage usersDataStorage)
    {
        _usersDataStorage = usersDataStorage;
        _gameLogs = _usersDataStorage.LoadLogs();
    }


    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }


    public void SaveLogs()
    {
        _usersDataStorage.SaveLogs(_gameLogs);
    }

    public void AddLog(GameLogsClass logs)
    {
        _gameLogs.Add(logs);
        SaveLogs();
    }

    public void CreateNewLog(GameClass game)
    {
        var playerList = new List<GameLogsPlayer>();

        foreach (var player in game.PlayersList)
            playerList.Add(new GameLogsPlayer(player.DiscordId,
                player.DiscordUsername, player.Character.Name,
                player.Status.GetScore(), player.Character.GetIntelligence(), player.Character.GetStrength(),
                player.Character.GetSpeed(), player.Character.GetPsyche(), player.Status.InGamePersonalLogsAll));

       // var log = new GameLogsClass(game.GameId, game.WhoWon, playerList, game.GetAllGlobalLogs());
        //AddLog(log);
    }
}