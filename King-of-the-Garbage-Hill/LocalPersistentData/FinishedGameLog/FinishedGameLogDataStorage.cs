using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.LocalPersistentData.FinishedGameLog
{
    public sealed class FinishedGameLogDataStorage : IServiceSingleton
    {
        //Save all DiscordAccountClass

        private readonly LoginFromConsole _log;

        public FinishedGameLogDataStorage(LoginFromConsole log)
        {
            _log = log;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }


        public void SaveLogs(IEnumerable<GameLogsClass> accounts)
        {
            try
            {
                var filePath = @"DataBase/GameLogs.json";

                var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch
            {
               _log.Critical("Failed To ReadFile(SaveAccountSettings). Will ty in 5 sec.");
            }
        }

        public List<GameLogsClass> LoadLogs()
        {
            var filePath = @"DataBase/GameLogs.json";

            if (!File.Exists(filePath))
            {
                var newList = new List<GameLogsClass>();
                SaveLogs(newList);
                return newList;
            }

            var json = File.ReadAllText(filePath);

            try
            {
                return JsonConvert.DeserializeObject<List<GameLogsClass>>(json);
            }
            catch (Exception e)
            {
                _log.Critical($"LoadAccountSettings, BACK UP CREATED: {e}");
            }

            return new List<GameLogsClass>();
        }
    }
}